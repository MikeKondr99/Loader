namespace Loader.Lang;

public static class NameSuggestion
{
    public static string? FindClosest(string value, IEnumerable<string> candidates)
    {
        var normalizedValue = Normalize(value);
        if (normalizedValue.Length == 0)
        {
            return null;
        }

        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Distance(normalizedValue, Normalize(candidate));
            if (distance >= bestDistance)
            {
                continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        return bestDistance <= MaxDistance(normalizedValue.Length)
            ? best
            : null;
    }

    public static string AppendSuggestion(string message, string value, IEnumerable<string> candidates)
    {
        var suggestion = FindClosest(value, candidates);
        return suggestion is null
            ? message
            : $"{message} Возможно вы имели в виду '{suggestion}'.";
    }

    private static int MaxDistance(int length)
    {
        return length switch
        {
            <= 4 => 1,
            <= 8 => 2,
            _ => 3
        };
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static int Distance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        Span<int> previous = stackalloc int[right.Length + 1];
        Span<int> current = stackalloc int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var cost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
            }

            var temp = previous;
            previous = current;
            current = temp;
        }

        return previous[right.Length];
    }
}
