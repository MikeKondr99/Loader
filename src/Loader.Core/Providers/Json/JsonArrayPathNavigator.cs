using System.Globalization;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Потоковый навигатор до массива-таблицы по строгому JSON path.
///
/// Навигатор не ищет segment "где-нибудь ниже". Каждый segment применяется только к текущему
/// контейнеру: в object это имя property, в array это zero-based index. Например путь
/// ["tables", "0", "data"] означает:
///
/// 1. В root object найти property "tables".
/// 2. В массиве tables взять элемент с индексом 0.
/// 3. В выбранном object найти property "data".
/// 4. Если value у data - StartArray, этот массив и есть таблица.
///
/// Если на каком-то шаге текущий контейнер не соответствует segment-у, массив считается
/// не найденным. Ошибку provider уровня формирует вызывающий код после завершения навигации.
/// </summary>
internal sealed class JsonArrayPathNavigator
{
    private readonly IReadOnlyList<string> _arrayPath;
    private JsonContainerKind? _currentContainerKind;
    private int _currentContainerDepth = -1;
    private int _matchedSegments;
    private int _nextArrayItemIndex;
    private int? _pendingPropertyMatchedSegments;
    private int? _ignoredContainerDepth;
    private bool _failed;

    public JsonArrayPathNavigator(IReadOnlyList<string> arrayPath)
    {
        _arrayPath = arrayPath;
    }

    public bool Found { get; private set; }

    public int ArrayDepth { get; private set; } = -1;

    public void ProcessToken(JsonTokenType tokenType, int depth, string? propertyName)
    {
        if (Found || _failed)
        {
            return;
        }

        if (TryFinishIgnoredContainer(tokenType, depth))
        {
            return;
        }

        if (_pendingPropertyMatchedSegments is not null)
        {
            ProcessMatchedValue(tokenType, depth, _pendingPropertyMatchedSegments.Value);
            _pendingPropertyMatchedSegments = null;
            return;
        }

        if (_currentContainerKind is null)
        {
            ProcessRootToken(tokenType, depth);
            return;
        }

        switch (_currentContainerKind)
        {
            case JsonContainerKind.Object:
                ProcessObjectToken(tokenType, depth, propertyName);
                break;

            case JsonContainerKind.Array:
                ProcessArrayToken(tokenType, depth);
                break;
        }
    }

    private bool TryFinishIgnoredContainer(JsonTokenType tokenType, int depth)
    {
        if (_ignoredContainerDepth is null)
        {
            return false;
        }

        if (IsEndContainer(tokenType) && depth == _ignoredContainerDepth.Value)
        {
            _ignoredContainerDepth = null;
        }

        return true;
    }

    private void ProcessRootToken(JsonTokenType tokenType, int depth)
    {
        if (depth != 0)
        {
            return;
        }

        if (_arrayPath.Count == 0)
        {
            if (tokenType == JsonTokenType.StartArray)
            {
                MarkFound(depth);
                return;
            }

            _failed = true;
            return;
        }

        switch (tokenType)
        {
            case JsonTokenType.StartObject:
                EnterContainer(JsonContainerKind.Object, depth, matchedSegments: 0);
                break;

            case JsonTokenType.StartArray:
                EnterContainer(JsonContainerKind.Array, depth, matchedSegments: 0);
                break;

            default:
                _failed = true;
                break;
        }
    }

    private void ProcessObjectToken(JsonTokenType tokenType, int depth, string? propertyName)
    {
        if (tokenType == JsonTokenType.EndObject && depth == _currentContainerDepth)
        {
            _failed = true;
            return;
        }

        if (tokenType != JsonTokenType.PropertyName || depth != _currentContainerDepth + 1)
        {
            return;
        }

        if (_matchedSegments >= _arrayPath.Count ||
            !string.Equals(_arrayPath[_matchedSegments], propertyName ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        _pendingPropertyMatchedSegments = _matchedSegments + 1;
    }

    private void ProcessArrayToken(JsonTokenType tokenType, int depth)
    {
        if (tokenType == JsonTokenType.EndArray && depth == _currentContainerDepth)
        {
            _failed = true;
            return;
        }

        if (depth != _currentContainerDepth + 1)
        {
            return;
        }

        if (!TryGetExpectedArrayIndex(out var expectedIndex))
        {
            _failed = true;
            return;
        }

        if (_nextArrayItemIndex > expectedIndex)
        {
            _failed = true;
            return;
        }

        if (_nextArrayItemIndex < expectedIndex)
        {
            IgnoreArrayItem(tokenType, depth);
            _nextArrayItemIndex++;
            return;
        }

        _nextArrayItemIndex++;
        ProcessMatchedValue(tokenType, depth, _matchedSegments + 1);
    }

    private void ProcessMatchedValue(JsonTokenType tokenType, int depth, int matchedSegments)
    {
        if (matchedSegments == _arrayPath.Count && tokenType == JsonTokenType.StartArray)
        {
            MarkFound(depth);
            return;
        }

        if (matchedSegments >= _arrayPath.Count)
        {
            _failed = true;
            return;
        }

        switch (tokenType)
        {
            case JsonTokenType.StartObject:
                EnterContainer(JsonContainerKind.Object, depth, matchedSegments);
                break;

            case JsonTokenType.StartArray:
                EnterContainer(JsonContainerKind.Array, depth, matchedSegments);
                break;

            default:
                _failed = true;
                break;
        }
    }

    private bool TryGetExpectedArrayIndex(out int index)
    {
        index = -1;
        return _matchedSegments < _arrayPath.Count &&
               int.TryParse(_arrayPath[_matchedSegments], NumberStyles.None, CultureInfo.InvariantCulture, out index) &&
               index >= 0;
    }

    private void IgnoreArrayItem(JsonTokenType tokenType, int depth)
    {
        if (tokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            _ignoredContainerDepth = depth;
        }
    }

    private void EnterContainer(JsonContainerKind kind, int depth, int matchedSegments)
    {
        _currentContainerKind = kind;
        _currentContainerDepth = depth;
        _matchedSegments = matchedSegments;
        _nextArrayItemIndex = 0;
        _pendingPropertyMatchedSegments = null;
    }

    private void MarkFound(int depth)
    {
        Found = true;
        ArrayDepth = depth;
        _currentContainerKind = null;
        _pendingPropertyMatchedSegments = null;
        _ignoredContainerDepth = null;
    }

    private static bool IsEndContainer(JsonTokenType tokenType)
    {
        return tokenType is JsonTokenType.EndObject or JsonTokenType.EndArray;
    }

    private enum JsonContainerKind
    {
        Object,
        Array
    }
}
