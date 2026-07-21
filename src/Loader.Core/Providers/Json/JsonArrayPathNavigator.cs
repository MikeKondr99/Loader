using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// State-machine, которая двигается по JSON только до массива таблицы.
///
/// Навигатор не собирает абсолютный путь вида a.b.c. Вместо этого он хранит для каждого открытого
/// контейнера число уже совпавших сегментов ArrayPath. Если ветка не совпала, в стек кладется -1,
/// и все вложенные property в этой ветке игнорируются до закрытия контейнера.
///
/// Пример для ArrayPath ["response", "items"]:
/// 1. В root object ищем property "response".
/// 2. Если следующий token входит в контейнер response, внутри него ищем property "items".
/// 3. Если value у "items" оказался StartArray, считаем этот массив найденным.
/// </summary>
internal sealed class JsonArrayPathNavigator
{
    private const int RejectedBranch = -1;

    private readonly IReadOnlyList<string> _arrayPath;
    private readonly List<int> _matchedSegmentsByContainer = [];
    private int? _pendingPropertyMatchedSegments;

    public JsonArrayPathNavigator(IReadOnlyList<string> arrayPath)
    {
        _arrayPath = arrayPath;
    }

    public bool Found { get; private set; }

    public int ArrayDepth { get; private set; } = -1;

    public void ProcessToken(JsonTokenType tokenType, int depth, string? propertyName)
    {
        if (Found)
        {
            return;
        }

        switch (tokenType)
        {
            case JsonTokenType.PropertyName:
                ProcessProperty(propertyName ?? string.Empty);
                break;

            case JsonTokenType.StartArray:
                ProcessStartArray(depth);
                break;

            case JsonTokenType.StartObject:
                EnterContainer();
                break;

            case JsonTokenType.EndArray:
            case JsonTokenType.EndObject:
                ExitContainer();
                break;

            default:
                // Primitive value consumes current property; it cannot move us deeper into ArrayPath.
                _pendingPropertyMatchedSegments = null;
                break;
        }
    }

    private void ProcessProperty(string propertyName)
    {
        var matchedSegments = CurrentMatchedSegments();
        if (matchedSegments == RejectedBranch || matchedSegments >= _arrayPath.Count)
        {
            _pendingPropertyMatchedSegments = RejectedBranch;
            return;
        }

        _pendingPropertyMatchedSegments = string.Equals(
            _arrayPath[matchedSegments],
            propertyName,
            StringComparison.Ordinal)
                ? matchedSegments + 1
                : RejectedBranch;
    }

    private void ProcessStartArray(int depth)
    {
        // 1. Пустой ArrayPath означает root array.
        if (_arrayPath.Count == 0 && _matchedSegmentsByContainer.Count == 0)
        {
            MarkFound(depth);
            return;
        }

        // 2. Если текущий property закрыл весь ArrayPath, его array value и есть таблица.
        if (_pendingPropertyMatchedSegments == _arrayPath.Count)
        {
            MarkFound(depth);
            return;
        }

        // 3. Иначе массив просто продолжает текущую ветку или уводит нас в rejected branch.
        EnterContainer();
    }

    private void EnterContainer()
    {
        _matchedSegmentsByContainer.Add(_pendingPropertyMatchedSegments ?? CurrentMatchedSegments());
        _pendingPropertyMatchedSegments = null;
    }

    private void ExitContainer()
    {
        _pendingPropertyMatchedSegments = null;
        if (_matchedSegmentsByContainer.Count > 0)
        {
            _matchedSegmentsByContainer.RemoveAt(_matchedSegmentsByContainer.Count - 1);
        }
    }

    private int CurrentMatchedSegments()
    {
        return _matchedSegmentsByContainer.Count == 0 ? 0 : _matchedSegmentsByContainer[^1];
    }

    private void MarkFound(int depth)
    {
        Found = true;
        ArrayDepth = depth;
        _matchedSegmentsByContainer.Clear();
        _pendingPropertyMatchedSegments = null;
    }
}
