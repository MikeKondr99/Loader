using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Универсальный reader JSON-строки для dot-path схемы.
/// Он поддерживает вложенные объекты, whole-row колонки и JSON-текст для object/array значений.
/// </summary>
internal sealed class JsonNestedPathRowReader
{
    private readonly IReadOnlyList<JsonColumnBinding> _columns;

    public JsonNestedPathRowReader(IReadOnlyList<JsonColumnBinding> columns)
    {
        _columns = columns;
    }

    public void Read(ReadOnlySpan<byte> rowBytes, ref Utf8JsonReader reader, object[] values)
    {
        var stack = new List<string>();
        string? propertyName = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    propertyName = reader.GetString() ?? string.Empty;
                    break;

                case JsonTokenType.StartObject:
                    ReadObjectValue(rowBytes, ref reader, values, stack, ref propertyName);
                    break;

                case JsonTokenType.StartArray:
                    ReadArrayValue(rowBytes, ref reader, values, stack, ref propertyName);
                    break;

                case JsonTokenType.EndObject:
                    if (stack.Count == 0)
                    {
                        return;
                    }

                    stack.RemoveAt(stack.Count - 1);
                    break;

                default:
                    ReadPrimitiveValue(rowBytes, reader, values, stack, propertyName);
                    propertyName = null;
                    break;
            }
        }
    }

    public void SetWholeRowColumns(ReadOnlySpan<byte> rowBytes, Utf8JsonReader reader, object[] values)
    {
        foreach (var column in _columns)
        {
            if (column.IsWholeRow)
            {
                values[column.Ordinal] = JsonRowValueReader.ReadValue(rowBytes, reader);
            }
        }
    }

    private void ReadObjectValue(
        ReadOnlySpan<byte> rowBytes,
        ref Utf8JsonReader reader,
        object[] values,
        List<string> stack,
        ref string? propertyName)
    {
        if (propertyName is null)
        {
            return;
        }

        // 1. Если сама колонка указывает на объект, возвращаем объект JSON-текстом.
        SetMatchingColumns(rowBytes, reader, values, stack, propertyName);

        // 2. Если есть более глубокие dot-path колонки, продолжаем читать объект.
        if (HasChildColumns(stack, propertyName))
        {
            stack.Add(propertyName);
            propertyName = null;
            return;
        }

        // 3. Иначе объект целиком не нужен: пропускаем его поддерево.
        reader.Skip();
        propertyName = null;
    }

    private void ReadArrayValue(
        ReadOnlySpan<byte> rowBytes,
        ref Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<string> stack,
        ref string? propertyName)
    {
        if (propertyName is not null)
        {
            // 1. Массивы не flatten-ятся, но могут быть явной JSON-текстовой колонкой.
            SetMatchingColumns(rowBytes, reader, values, stack, propertyName);
            propertyName = null;
        }

        // 2. Путь внутрь массива пока не поддерживаем: пропускаем весь массив.
        reader.Skip();
    }

    private void ReadPrimitiveValue(
        ReadOnlySpan<byte> rowBytes,
        Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<string> stack,
        string? propertyName)
    {
        if (propertyName is null)
        {
            return;
        }

        SetMatchingColumns(rowBytes, reader, values, stack, propertyName);
    }

    private void SetMatchingColumns(
        ReadOnlySpan<byte> rowBytes,
        Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<string> stack,
        string propertyName)
    {
        foreach (var column in _columns)
        {
            if (!column.IsWholeRow && IsExactPath(column.Segments, stack, propertyName))
            {
                values[column.Ordinal] = JsonRowValueReader.ReadValue(rowBytes, reader);
            }
        }
    }

    private static bool IsExactPath(IReadOnlyList<string> columnSegments, IReadOnlyList<string> stack, string propertyName)
    {
        if (columnSegments.Count != stack.Count + 1)
        {
            return false;
        }

        for (var i = 0; i < stack.Count; i++)
        {
            if (!string.Equals(columnSegments[i], stack[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(columnSegments[^1], propertyName, StringComparison.Ordinal);
    }

    private bool HasChildColumns(IReadOnlyList<string> stack, string propertyName)
    {
        foreach (var column in _columns)
        {
            if (column.Segments.Count <= stack.Count + 1)
            {
                continue;
            }

            if (!IsPathPrefix(column.Segments, stack, propertyName))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsPathPrefix(IReadOnlyList<string> columnSegments, IReadOnlyList<string> stack, string propertyName)
    {
        for (var i = 0; i < stack.Count; i++)
        {
            if (!string.Equals(columnSegments[i], stack[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(columnSegments[stack.Count], propertyName, StringComparison.Ordinal);
    }
}
