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
        private readonly JsonArrayPathNavigator _arrayPathNavigator;
        private readonly JsonArraySchemaCollector _schemaCollector;

        public StreamingSchemaAnalyzer(IReadOnlyList<string> arrayPath, bool flattenObjects)
        {
            _arrayPathNavigator = new JsonArrayPathNavigator(arrayPath);
            _schemaCollector = new JsonArraySchemaCollector(flattenObjects);
        }

        public bool FoundArray => _arrayPathNavigator.Found;

        public void ProcessToken(Utf8JsonReader reader)
        {
            // 1. До найденного массива занимаемся только навигацией по абсолютному ArrayPath.
            if (!_arrayPathNavigator.Found)
            {
                _arrayPathNavigator.ProcessToken(
                    reader.TokenType,
                    reader.CurrentDepth,
                    reader.TokenType == JsonTokenType.PropertyName ? reader.GetString() ?? string.Empty : null);
                return;
            }

            // 2. После найденного массива читаем только его содержимое; абсолютный путь уже не нужен.
            _schemaCollector.ProcessToken(reader, _arrayPathNavigator.ArrayDepth);
        }

        public JsonTableSchema ToSchema()
        {
            return _schemaCollector.ToSchema();
        }
    }

    private sealed class JsonArraySchemaCollector
    {
        private readonly bool _flattenObjects;
        private readonly List<JsonColumnSchema> _columns = [];
        private readonly HashSet<string> _knownPaths = new(StringComparer.Ordinal);
        private readonly List<KnownFlatColumn> _knownFlatColumns = [];
        private readonly List<JsonContext> _rowContexts = [];
        private bool _insideRow;
        private int _rowDepth = -1;

        public JsonArraySchemaCollector(bool flattenObjects)
        {
            _flattenObjects = flattenObjects;
        }

        public void ProcessToken(Utf8JsonReader reader, int arrayDepth)
        {
            // 1. Каждый токен меняет стек JSON-контекстов или добавляет колонку схемы.
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    HandleStartObject(reader.CurrentDepth, arrayDepth);
                    break;

                case JsonTokenType.EndObject:
                    HandleEndObject(reader.CurrentDepth);
                    break;

                case JsonTokenType.StartArray:
                    HandleStartArray();
                    break;

                case JsonTokenType.EndArray:
                    HandleEndArray(reader.CurrentDepth, arrayDepth);
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

        private void HandleStartObject(int depth, int arrayDepth)
        {
            // 1. Объект на один уровень ниже найденного массива считаем строкой таблицы.
            if (!_insideRow && depth == arrayDepth + 1)
            {
                _insideRow = true;
                _rowDepth = depth;
                _rowContexts.Clear();
                _rowContexts.Add(new JsonContext(null, collectChildren: true));
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
                        _rowContexts.Add(new JsonContext(propertyName, collectChildren: true));
                    }
                    else
                    {
                        AddCurrentPathColumn(propertyName);
                        _rowContexts.Add(new JsonContext(propertyName, collectChildren: false));
                    }

                    ClearCurrentProperty();
                    return;
                }
            }

            // 3. Все остальные объекты нужны только как навигационный контекст.
            _rowContexts.Add(new JsonContext(CurrentProperty(), collectChildren: false));
            ClearCurrentProperty();
        }

        private void HandleEndObject(int depth)
        {
            // 1. Закрываем текущий объект и, если это была строка таблицы, выходим из row mode.
            if (_insideRow && depth == _rowDepth)
            {
                _rowContexts.Clear();
                _insideRow = false;
                _rowDepth = -1;
                return;
            }

            PopContext();
        }

        private void HandleStartArray()
        {
            // 1. Массив внутри строки не раскрываем, а считаем одной JSON-колонкой.
            var propertyName = CurrentProperty();
            if (_insideRow && propertyName is not null && CanCollectCurrentPath())
            {
                AddCurrentPathColumn(propertyName);
            }

            // 2. Сам массив кладем в стек, чтобы пропустить его вложенные токены как колонки.
            _rowContexts.Add(new JsonContext(propertyName, collectChildren: false));
            ClearCurrentProperty();
        }

        private void HandleEndArray(int depth, int arrayDepth)
        {
            // 1. Закрываем массив и, если это была таблица, выходим из table mode.
            if (depth == arrayDepth)
            {
                _rowContexts.Clear();
                return;
            }

            PopContext();
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
            return _insideRow
                && _rowContexts.Count == 1
                && _rowContexts[0].Segment is null
                && _rowContexts[0].CollectChildren;
        }

        private string BuildRowPath(string leaf)
        {
            var segments = _rowContexts
                .Where(static context => context.Segment is not null)
                .Select(static context => context.Segment!)
                .Append(leaf);

            return string.Join('.', segments);
        }

        private bool CanCollectCurrentPath()
        {
            return _insideRow
                && _rowContexts.Count > 0
                && _rowContexts.All(static context => context.CollectChildren);
        }

        private string? CurrentProperty()
        {
            return _rowContexts.Count == 0 ? null : _rowContexts[^1].CurrentProperty;
        }

        private void SetCurrentProperty(string name)
        {
            if (_rowContexts.Count == 0)
            {
                _rowContexts.Add(new JsonContext(null, collectChildren: true));
            }

            _rowContexts[^1].CurrentProperty = name;
        }

        private void ClearCurrentProperty()
        {
            if (_rowContexts.Count > 0)
            {
                _rowContexts[^1].CurrentProperty = null;
            }
        }

        private void PopContext()
        {
            if (_rowContexts.Count > 0)
            {
                _rowContexts.RemoveAt(_rowContexts.Count - 1);
            }
        }
    }

    private sealed class JsonContext
    {
        public JsonContext(string? segment, bool collectChildren)
        {
            Segment = segment;
            CollectChildren = collectChildren;
        }

        public string? Segment { get; }

        public bool CollectChildren { get; }

        public string? CurrentProperty { get; set; }
    }

    private sealed record KnownFlatColumn(string Name, byte[] Utf8Name);
}
