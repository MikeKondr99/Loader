namespace Loader.Core.Decorators;

internal static class AbstractColumnNames
{
    private static readonly Lock SyncRoot = new();
    private static string[] _names = CreateNames(30);
    private static string[][] _sets = CreateSets(_names);

    public static string[] Get(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Column count must be greater than or equal to zero.");
        }

        var sets = _sets;
        if (count < sets.Length && sets[count] is not null)
        {
            return sets[count];
        }

        lock (SyncRoot)
        {
            if (count >= _sets.Length)
            {
                _names = GrowNames(count);
                _sets = GrowSets(_names, count);
            }

            return _sets[count];
        }
    }

    private static string[] GrowNames(int count)
    {
        if (count <= _names.Length)
        {
            return _names;
        }

        var names = new string[count];
        Array.Copy(_names, names, _names.Length);
        for (var i = _names.Length; i < names.Length; i++)
        {
            names[i] = $"column{i + 1}";
        }

        return names;
    }

    private static string[][] GrowSets(string[] names, int count)
    {
        var sets = new string[count + 1][];
        Array.Copy(_sets, sets, _sets.Length);
        for (var i = _sets.Length; i < sets.Length; i++)
        {
            sets[i] = names[..i];
        }

        return sets;
    }

    private static string[] CreateNames(int count)
    {
        var names = new string[count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = $"column{i + 1}";
        }

        return names;
    }

    private static string[][] CreateSets(string[] names)
    {
        var sets = new string[names.Length + 1][];
        for (var i = 0; i < sets.Length; i++)
        {
            sets[i] = names[..i];
        }

        return sets;
    }
}
