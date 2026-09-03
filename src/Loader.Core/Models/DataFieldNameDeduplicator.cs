namespace Loader.Core.Models;

internal static class DataFieldNameDeduplicator
{
    public static string[] Deduplicate(IReadOnlyList<string> names)
    {
        var reservedNames = new HashSet<string>(names, StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var nextSuffixByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new string[names.Count];

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (usedNames.Add(name))
            {
                result[i] = name;
                continue;
            }

            var suffix = nextSuffixByName.GetValueOrDefault(name, 2);
            string candidate;
            do
            {
                candidate = $"{name}_{suffix}";
                suffix++;
            }
            while (reservedNames.Contains(candidate) || !usedNames.Add(candidate));

            nextSuffixByName[name] = suffix;
            result[i] = candidate;
        }

        return result;
    }
}
