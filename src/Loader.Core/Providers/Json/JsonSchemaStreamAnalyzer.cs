using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Потоковый анализатор схемы JSON-таблицы.
/// 
/// Важная деталь: <see cref="Utf8JsonReader"/> сам не читает <see cref="Stream"/> и не владеет
/// внутренним буфером. Это ref struct parser над уже переданным <see cref="ReadOnlySpan{T}"/>.
/// Поэтому класс сам читает файл кусками, хранит <see cref="JsonReaderState"/> между кусками и
/// переносит неполный хвост токена в начало буфера перед следующим чтением.
///
/// Алгоритм:
/// 1. Читаем UTF-8 bytes из stream в pooled buffer.
/// 2. Прогоняем по буферу Utf8JsonReader и передаем каждый полный токен в state-machine.
/// 3. State-machine ищет массив по ArrayPath.
/// 4. После входа в массив объект на один уровень ниже массива считается строкой таблицы.
/// 5. По объектам строк собирается union колонок в порядке первого появления.
/// 6. Вложенные объекты раскрываются только если flattenObjects = true; массивы остаются одной колонкой.
/// </summary>
internal static class JsonSchemaStreamAnalyzer
{
    private const int InitialBufferSize = 64 * 1024;

    public static async ValueTask<JsonTableSchema> AnalyzeAsync(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        bool flattenObjects,
        CancellationToken cancellationToken)
    {
        var analyzer = new StreamingSchemaAnalyzer(arrayPath, flattenObjects);
        await ReadUtf8JsonAsync(stream, analyzer, cancellationToken).ConfigureAwait(false);

        if (!analyzer.FoundArray)
        {
            throw new JsonArrayPathNotFoundProviderException(fileName, arrayPath);
        }

        return analyzer.ToSchema();
    }

    private static async ValueTask ReadUtf8JsonAsync(
        Stream stream,
        StreamingSchemaAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        var bytesInBuffer = 0;
        var state = new JsonReaderState();

        try
        {
            while (true)
            {
                // 1. Если один JSON-токен оказался больше текущего буфера, расширяем буфер.
                if (bytesInBuffer == buffer.Length)
                {
                    var grown = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    Buffer.BlockCopy(buffer, 0, grown, 0, bytesInBuffer);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = grown;
                }

                // 2. Дочитываем следующий кусок файла после необработанного остатка.
                var read = await stream
                    .ReadAsync(buffer.AsMemory(bytesInBuffer, buffer.Length - bytesInBuffer), cancellationToken)
                    .ConfigureAwait(false);

                // 3. Передаем Utf8JsonReader весь доступный кусок и состояние предыдущего прохода.
                var isFinalBlock = read == 0;
                var available = bytesInBuffer + read;
                var reader = new Utf8JsonReader(buffer.AsSpan(0, available), isFinalBlock, state);

                // 4. Обрабатываем все полные JSON-токены, которые есть в текущем буфере.
                while (reader.Read())
                {
                    analyzer.ProcessToken(reader);
                }

                // 5. Неполный хвост токена переносим в начало буфера для следующего чтения.
                var consumed = (int)reader.BytesConsumed;
                var remaining = available - consumed;
                if (remaining > 0)
                {
                    Buffer.BlockCopy(buffer, consumed, buffer, 0, remaining);
                }

                // 6. Сохраняем состояние reader-а, чтобы следующий кусок продолжил тот же JSON.
                bytesInBuffer = remaining;
                state = reader.CurrentState;

                if (isFinalBlock)
                {
                    return;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed class StreamingSchemaAnalyzer
    {
        private readonly IReadOnlyList<string> _arrayPath;
        private readonly bool _flattenObjects;
        private readonly List<JsonColumnSchema> _columns = [];
        private readonly HashSet<string> _knownPaths = new(StringComparer.Ordinal);
        private readonly List<KnownFlatColumn> _knownFlatColumns = [];
        private readonly List<JsonContext> _contexts = [];
        private bool _insideRow;
        private bool _insideTargetArray;
        private int _rowDepth = -1;
        private int _targetArrayDepth = -1;

        public StreamingSchemaAnalyzer(IReadOnlyList<string> arrayPath, bool flattenObjects)
        {
            _arrayPath = arrayPath;
            _flattenObjects = flattenObjects;
        }

        public bool FoundArray { get; private set; }

        public void ProcessToken(Utf8JsonReader reader)
        {
            // 1. Каждый токен меняет стек JSON-контекстов или добавляет колонку схемы.
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    HandleStartObject(reader.CurrentDepth);
                    break;

                case JsonTokenType.EndObject:
                    HandleEndObject(reader.CurrentDepth);
                    break;

                case JsonTokenType.StartArray:
                    HandleStartArray(reader.CurrentDepth);
                    break;

                case JsonTokenType.EndArray:
                    HandleEndArray(reader.CurrentDepth);
                    break;

                case JsonTokenType.PropertyName:
                    HandlePropertyName(reader);
                    break;

                default:
                    HandleValue();
                    break;
            }
        }

        private void HandlePropertyName(Utf8JsonReader reader)
        {
            // 1. Для hot path flat JSON-таблицы не создаем string, если имя колонки уже известно.
            if (TrySetKnownFlatProperty(reader))
            {
                return;
            }

            // 2. Новое или nested имя свойства пока нужно сохранить как string для общей state-machine.
            SetCurrentProperty(reader.GetString() ?? string.Empty);
        }

        public JsonTableSchema ToSchema()
        {
            return new JsonTableSchema
            {
                Columns = _columns
            };
        }

        private void HandleStartObject(int depth)
        {
            // 1. Объект на один уровень ниже найденного массива считаем строкой таблицы.
            if (_insideTargetArray && !_insideRow && depth == _targetArrayDepth + 1)
            {
                _insideRow = true;
                _rowDepth = depth;
                _contexts.Add(new JsonContext(null, collectChildren: true, isRowRoot: true));
                return;
            }

            // 2. Внутри строки вложенный объект либо раскрываем, либо оставляем одной колонкой.
            if (_insideRow)
            {
                var propertyName = CurrentProperty();
                if (propertyName is not null && CanCollectCurrentPath())
                {
                    if (_flattenObjects)
                    {
                        _contexts.Add(new JsonContext(propertyName, collectChildren: true, isRowRoot: false));
                    }
                    else
                    {
                        AddCurrentPathColumn(propertyName);
                        _contexts.Add(new JsonContext(propertyName, collectChildren: false, isRowRoot: false));
                    }

                    ClearCurrentProperty();
                    return;
                }
            }

            // 3. Все остальные объекты нужны только как навигационный контекст.
            _contexts.Add(new JsonContext(CurrentProperty(), collectChildren: false, isRowRoot: false));
            ClearCurrentProperty();
        }

        private void HandleEndObject(int depth)
        {
            // 1. Закрываем текущий объект и, если это была строка таблицы, выходим из row mode.
            PopContext();

            if (_insideRow && depth == _rowDepth)
            {
                _insideRow = false;
                _rowDepth = -1;
            }
        }

        private void HandleStartArray(int depth)
        {
            // 1. Проверяем, не является ли текущий массив таблицей по ArrayPath.
            var propertyName = CurrentProperty();
            if (!_insideTargetArray && IsArrayPath(BuildDocumentPath(propertyName)))
            {
                FoundArray = true;
                _insideTargetArray = true;
                _targetArrayDepth = depth;
                _contexts.Add(new JsonContext(propertyName, collectChildren: false, isRowRoot: false));
                ClearCurrentProperty();
                return;
            }

            // 2. Массив внутри строки не раскрываем, а считаем одной JSON-колонкой.
            if (_insideRow && propertyName is not null && CanCollectCurrentPath())
            {
                AddCurrentPathColumn(propertyName);
            }

            // 3. Сам массив кладем в стек, чтобы пропустить его вложенные токены как колонки.
            _contexts.Add(new JsonContext(propertyName, collectChildren: false, isRowRoot: false));
            ClearCurrentProperty();
        }

        private void HandleEndArray(int depth)
        {
            // 1. Закрываем массив и, если это была таблица, выходим из table mode.
            PopContext();

            if (_insideTargetArray && depth == _targetArrayDepth)
            {
                _insideTargetArray = false;
                _targetArrayDepth = -1;
            }
        }

        private void HandleValue()
        {
            // 1. Примитивное значение внутри строки становится колонкой схемы.
            var propertyName = CurrentProperty();
            if (_insideRow && propertyName is not null && CanCollectCurrentPath())
            {
                AddCurrentPathColumn(propertyName);
            }

            ClearCurrentProperty();
        }

        private void AddCurrentPathColumn(string propertyName)
        {
            // 1. Для flat строки не строим dot-path через LINQ/string.Join на каждое значение.
            if (IsFlatRowPath())
            {
                AddFlatColumn(propertyName);
                return;
            }

            // 2. Nested path пока остается общим медленным путем.
            AddColumn(BuildRowPath(propertyName));
        }

        private void AddFlatColumn(string propertyName)
        {
            foreach (var column in _knownFlatColumns)
            {
                if (string.Equals(column.Name, propertyName, StringComparison.Ordinal))
                {
                    return;
                }
            }

            AddColumn(propertyName);
        }

        private void AddColumn(string path)
        {
            if (!_knownPaths.Add(path))
            {
                return;
            }

            _columns.Add(new JsonColumnSchema
            {
                Name = path,
                Path = path
            });

            if (IsFlatRowPath())
            {
                _knownFlatColumns.Add(new KnownFlatColumn(path, Encoding.UTF8.GetBytes(path)));
            }
        }

        private bool TrySetKnownFlatProperty(Utf8JsonReader reader)
        {
            if (!_insideRow || !IsFlatRowPath())
            {
                return false;
            }

            foreach (var column in _knownFlatColumns)
            {
                if (reader.ValueTextEquals(column.Utf8Name))
                {
                    SetCurrentProperty(column.Name);
                    return true;
                }
            }

            return false;
        }

        private bool IsFlatRowPath()
        {
            return _insideRow && _contexts.Count > 0 && _contexts[^1].IsRowRoot;
        }

        private string BuildRowPath(string leaf)
        {
            var rowRootIndex = RowRootIndex();
            var segments = _contexts
                .Skip(rowRootIndex + 1)
                .Where(static context => !context.IsRowRoot && context.Segment is not null)
                .Select(static context => context.Segment!)
                .Append(leaf);

            return string.Join('.', segments);
        }

        private IReadOnlyList<string> BuildDocumentPath(string? leaf)
        {
            var segments = _contexts
                .Where(static context => context.Segment is not null)
                .Select(static context => context.Segment!)
                .ToList();

            if (leaf is not null)
            {
                segments.Add(leaf);
            }

            return segments;
        }

        private bool IsArrayPath(IReadOnlyList<string> path)
        {
            return path.SequenceEqual(_arrayPath, StringComparer.Ordinal);
        }

        private bool CanCollectCurrentPath()
        {
            var rowRootIndex = RowRootIndex();
            return rowRootIndex >= 0 && _contexts.Skip(rowRootIndex).All(static context => context.CollectChildren);
        }

        private int RowRootIndex()
        {
            for (var i = _contexts.Count - 1; i >= 0; i--)
            {
                if (_contexts[i].IsRowRoot)
                {
                    return i;
                }
            }

            return -1;
        }

        private string? CurrentProperty()
        {
            return _contexts.Count == 0 ? null : _contexts[^1].CurrentProperty;
        }

        private void SetCurrentProperty(string name)
        {
            if (_contexts.Count == 0)
            {
                _contexts.Add(new JsonContext(null, collectChildren: false, isRowRoot: false));
            }

            _contexts[^1].CurrentProperty = name;
        }

        private void ClearCurrentProperty()
        {
            if (_contexts.Count > 0)
            {
                _contexts[^1].CurrentProperty = null;
            }
        }

        private void PopContext()
        {
            if (_contexts.Count > 0)
            {
                _contexts.RemoveAt(_contexts.Count - 1);
            }
        }
    }

    private sealed class JsonContext
    {
        public JsonContext(string? segment, bool collectChildren, bool isRowRoot)
        {
            Segment = segment;
            CollectChildren = collectChildren;
            IsRowRoot = isRowRoot;
        }

        public string? Segment { get; }

        public bool CollectChildren { get; }

        public bool IsRowRoot { get; }

        public string? CurrentProperty { get; set; }
    }

    private sealed record KnownFlatColumn(string Name, byte[] Utf8Name);
}
