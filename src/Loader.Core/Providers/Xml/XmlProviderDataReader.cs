using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Xml;
using Loader.Core.Exceptions;

namespace Loader.Core.Providers.Xml;

/// <summary>
/// Однопроходный reader плоской XML-таблицы.
///
/// Алгоритм не строит дерево XML: основной <see cref="XmlReader"/> последовательно ищет элементы
/// <c>TableName</c>, а затем читает только атрибуты и прямых детей текущей строки. В памяти находится
/// один массив значений размером с заданную схему. Текст одного поля берется напрямую из XmlReader;
/// <see cref="StringBuilder"/> создается только если XML parser разделил текст на несколько узлов.
/// Вложенный элемент не является flat-значением и возвращается как <see cref="DBNull"/>.
/// </summary>
internal sealed class XmlProviderDataReader : DbDataReader
{
    private const string XmlSchemaInstanceNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    private readonly string _fileName;
    private readonly string _tableName;
    private readonly XmlReader _reader;
    private readonly XmlTableSchema _schema;
    private readonly IReadOnlyDictionary<string, int> _attributesByName;
    private readonly IReadOnlyDictionary<string, int> _elementsByName;
    private readonly IReadOnlyDictionary<string, int> _ordinalsByName;
    private readonly string?[] _values;
    private bool _hasPrefetchedRow;
    private bool _hasCurrentRow;
    private bool _hasRows;
    private bool _isClosed;

    private XmlProviderDataReader(Stream stream, string fileName, string tableName, XmlTableSchema schema)
    {
        _fileName = fileName;
        _tableName = tableName;
        _schema = schema;
        _reader = XmlReaderFactory.Create(stream);
        _values = new string?[schema.Columns.Count];
        _attributesByName = CompilePaths(schema, attributes: true);
        _elementsByName = CompilePaths(schema, attributes: false);
        _ordinalsByName = schema.Columns
            .Select(static (column, ordinal) => (column.Name, Ordinal: ordinal))
            .ToDictionary(
                static item => item.Name,
                static item => item.Ordinal,
                StringComparer.Ordinal);
    }

    public static async ValueTask<XmlProviderDataReader> CreateAsync(
        Stream stream,
        string fileName,
        string tableName,
        XmlTableSchema schema,
        CancellationToken cancellationToken)
    {
        var reader = new XmlProviderDataReader(stream, fileName, tableName, schema);
        try
        {
            // 1. Предзагружаем только первую строку: документ остается потоковым, а HasRows точен.
            reader._hasPrefetchedRow = await reader
                .ReadNextRowAsync(cancellationToken)
                .ConfigureAwait(false);
            reader._hasRows = reader._hasPrefetchedRow;

            if (!reader._hasPrefetchedRow)
            {
                throw new XmlTableNotFoundProviderException(fileName, tableName);
            }

            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int Depth => 0;

    public override int FieldCount => _schema.Columns.Count;

    public override bool HasRows => _hasRows;

    public override bool IsClosed => _isClosed;

    public override int RecordsAffected => -1;

    public override bool Read()
    {
        try
        {
            // 1. Первая строка уже была прочитана при открытии provider-а.
            if (_hasPrefetchedRow)
            {
                _hasPrefetchedRow = false;
                _hasCurrentRow = true;
                return true;
            }

            // 2. Следующие вызовы двигают XmlReader до очередного элемента таблицы.
            _hasCurrentRow = ReadNextRow();
            return _hasCurrentRow;
        }
        catch (XmlException ex)
        {
            throw new XmlFileOpenProviderException(_fileName, ex);
        }
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 1. Для предзагруженной строки дополнительного IO не требуется.
            if (_hasPrefetchedRow)
            {
                _hasPrefetchedRow = false;
                _hasCurrentRow = true;
                return true;
            }

            // 2. Остальной XML читается через асинхронный путь XmlReader.
            _hasCurrentRow = await ReadNextRowAsync(cancellationToken).ConfigureAwait(false);
            return _hasCurrentRow;
        }
        catch (XmlException ex)
        {
            throw new XmlFileOpenProviderException(_fileName, ex);
        }
    }

    public override bool NextResult() => false;

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return _values[ordinal] is { } value ? value : DBNull.Value;
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public override string GetName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _schema.Columns[ordinal].Name;
    }

    public override int GetOrdinal(string name)
    {
        if (_ordinalsByName.TryGetValue(name, out var ordinal))
        {
            return ordinal;
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return "String";
    }

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return typeof(string);
    }

    public override bool IsDBNull(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return _values[ordinal] is null;
    }

    public override IEnumerator GetEnumerator()
    {
        while (Read())
        {
            yield return this;
        }
    }

    public override bool GetBoolean(int ordinal) => bool.Parse(GetString(ordinal));

    public override byte GetByte(int ordinal) => byte.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override char GetChar(int ordinal) => GetString(ordinal)[0];

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override DateTime GetDateTime(int ordinal) =>
        DateTime.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override decimal GetDecimal(int ordinal) =>
        decimal.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override double GetDouble(int ordinal) =>
        double.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override float GetFloat(int ordinal) =>
        float.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override Guid GetGuid(int ordinal) => Guid.Parse(GetString(ordinal));

    public override short GetInt16(int ordinal) =>
        short.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override int GetInt32(int ordinal) =>
        int.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override long GetInt64(int ordinal) =>
        long.Parse(GetString(ordinal), CultureInfo.InvariantCulture);

    public override string GetString(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return _values[ordinal]
            ?? throw new InvalidCastException($"Column '{GetName(ordinal)}' contains DBNull.");
    }

    public override T GetFieldValue<T>(int ordinal)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)GetString(ordinal);
        }

        if (typeof(T) == typeof(object))
        {
            return (T)GetValue(ordinal);
        }

        return base.GetFieldValue<T>(ordinal);
    }

    public override void Close()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _reader.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    private bool ReadNextRow()
    {
        Array.Fill(_values, null);

        // 1. Ищем следующий элемент таблицы, не сохраняя XML вне выбранных строк.
        while (_reader.Read())
        {
            if (IsTableElement())
            {
                ReadCurrentRow();
                return true;
            }
        }

        return false;
    }

    private async ValueTask<bool> ReadNextRowAsync(CancellationToken cancellationToken)
    {
        Array.Fill(_values, null);

        // 1. Ищем следующий элемент таблицы через async IO.
        while (await ReadXmlAsync(cancellationToken).ConfigureAwait(false))
        {
            if (IsTableElement())
            {
                await ReadCurrentRowAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private void ReadCurrentRow()
    {
        var rowDepth = _reader.Depth;
        ReadAttributes();

        if (_reader.IsEmptyElement)
        {
            return;
        }

        // 1. Читаем только прямых детей строки; незаявленные и nested элементы просто пропускаются.
        while (_reader.Read())
        {
            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == rowDepth)
            {
                return;
            }

            if (_reader.NodeType != XmlNodeType.Element || _reader.Depth != rowDepth + 1)
            {
                continue;
            }

            var ordinal = FindElementOrdinal(_reader.LocalName);
            if (ordinal < 0)
            {
                // Неизвестное схеме поле пропускаем без создания строки с его содержимым.
                SkipCurrentElement();
                continue;
            }

            var value = ReadFlatElementValue();
            _values[ordinal] = value;
        }

        throw new XmlException("Unexpected end of XML while reading a table row.");
    }

    private async ValueTask ReadCurrentRowAsync(CancellationToken cancellationToken)
    {
        var rowDepth = _reader.Depth;
        ReadAttributes();

        if (_reader.IsEmptyElement)
        {
            return;
        }

        // 1. Async-вариант повторяет тот же flat-контракт без синхронного чтения из stream.
        while (await ReadXmlAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == rowDepth)
            {
                return;
            }

            if (_reader.NodeType != XmlNodeType.Element || _reader.Depth != rowDepth + 1)
            {
                continue;
            }

            var ordinal = FindElementOrdinal(_reader.LocalName);
            if (ordinal < 0)
            {
                // Async path также не материализует значения, отсутствующие в заданной схеме.
                await SkipCurrentElementAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var value = await ReadFlatElementValueAsync(cancellationToken).ConfigureAwait(false);
            _values[ordinal] = value;
        }

        throw new XmlException("Unexpected end of XML while reading a table row.");
    }

    private void ReadAttributes()
    {
        if (!_reader.MoveToFirstAttribute())
        {
            return;
        }

        do
        {
            if (_attributesByName.TryGetValue(_reader.LocalName, out var ordinal))
            {
                _values[ordinal] = _reader.Value;
            }
        }
        while (_reader.MoveToNextAttribute());

        _reader.MoveToElement();
    }

    private string? ReadFlatElementValue()
    {
        if (IsNilElement())
        {
            SkipCurrentElement();
            return null;
        }

        if (_reader.IsEmptyElement)
        {
            return string.Empty;
        }

        var elementDepth = _reader.Depth;
        string? firstPart = null;
        StringBuilder? builder = null;
        var hasNestedElement = false;

        // 1. Собираем текст без промежуточного builder-а для обычного одноузлового значения.
        while (_reader.Read())
        {
            if (_reader.NodeType == XmlNodeType.Element && _reader.Depth > elementDepth)
            {
                hasNestedElement = true;
            }
            else if (IsTextNode(_reader.NodeType) && _reader.Depth == elementDepth + 1)
            {
                AppendText(_reader.Value, ref firstPart, ref builder);
            }

            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == elementDepth)
            {
                return hasNestedElement ? null : builder?.ToString() ?? firstPart ?? string.Empty;
            }
        }

        throw new XmlException("Unexpected end of XML while reading an element value.");
    }

    private async ValueTask<string?> ReadFlatElementValueAsync(CancellationToken cancellationToken)
    {
        if (IsNilElement())
        {
            await SkipCurrentElementAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (_reader.IsEmptyElement)
        {
            return string.Empty;
        }

        var elementDepth = _reader.Depth;
        string? firstPart = null;
        StringBuilder? builder = null;
        var hasNestedElement = false;

        // 1. Асинхронно читаем узлы значения; память зависит от одного поля, а не от файла.
        while (await ReadXmlAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_reader.NodeType == XmlNodeType.Element && _reader.Depth > elementDepth)
            {
                hasNestedElement = true;
            }
            else if (IsTextNode(_reader.NodeType) && _reader.Depth == elementDepth + 1)
            {
                AppendText(_reader.Value, ref firstPart, ref builder);
            }

            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == elementDepth)
            {
                return hasNestedElement ? null : builder?.ToString() ?? firstPart ?? string.Empty;
            }
        }

        throw new XmlException("Unexpected end of XML while reading an element value.");
    }

    private void SkipCurrentElement()
    {
        if (_reader.IsEmptyElement)
        {
            return;
        }

        var depth = _reader.Depth;
        while (_reader.Read())
        {
            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == depth)
            {
                return;
            }
        }
    }

    private async ValueTask SkipCurrentElementAsync(CancellationToken cancellationToken)
    {
        if (_reader.IsEmptyElement)
        {
            return;
        }

        var depth = _reader.Depth;
        while (await ReadXmlAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_reader.NodeType == XmlNodeType.EndElement && _reader.Depth == depth)
            {
                return;
            }
        }
    }

    private bool IsTableElement()
    {
        return _reader.NodeType == XmlNodeType.Element &&
            string.Equals(_reader.LocalName, _tableName, StringComparison.Ordinal);
    }

    private bool IsNilElement()
    {
        var value = _reader.GetAttribute("nil", XmlSchemaInstanceNamespace);
        return value is "true" or "1";
    }

    private int FindElementOrdinal(string name)
    {
        return _elementsByName.TryGetValue(name, out var ordinal) ? ordinal : -1;
    }

    private async ValueTask<bool> ReadXmlAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _reader.ReadAsync().ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, int> CompilePaths(XmlTableSchema schema, bool attributes)
    {
        var paths = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < schema.Columns.Count; ordinal++)
        {
            var path = schema.Columns[ordinal].Path;
            if (path.StartsWith('@') == attributes)
            {
                paths.Add(attributes ? path[1..] : path, ordinal);
            }
        }

        return paths;
    }

    private static bool IsTextNode(XmlNodeType nodeType)
    {
        return nodeType is XmlNodeType.Text
            or XmlNodeType.CDATA
            or XmlNodeType.Whitespace
            or XmlNodeType.SignificantWhitespace;
    }

    private static void AppendText(string value, ref string? firstPart, ref StringBuilder? builder)
    {
        if (firstPart is null)
        {
            firstPart = value;
            return;
        }

        builder ??= new StringBuilder(firstPart);
        builder.Append(value);
    }

    private void EnsureReadableRow()
    {
        if (!_hasCurrentRow)
        {
            throw new InvalidOperationException("Reader is not positioned on a row.");
        }
    }

    private void EnsureOrdinal(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new IndexOutOfRangeException($"Column ordinal {ordinal} is out of range.");
        }
    }
}
