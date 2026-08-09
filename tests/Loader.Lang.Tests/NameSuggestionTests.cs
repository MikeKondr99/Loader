namespace Loader.Lang.Tests;

public sealed class NameSuggestionTests
{
    [Test]
    [DisplayName("NameSuggestion находит ближайшее имя по небольшой опечатке")]
    public async Task Find_closest_returns_nearest_name_for_typo()
    {
        var suggestion = NameSuggestion.FindClosest("Postgre", ["Csv", "Postgres", "ClickHouse"]);

        await Assert.That(suggestion).IsEqualTo("Postgres");
    }

    [Test]
    [DisplayName("NameSuggestion не предлагает имя если совпадение слишком слабое")]
    public async Task Find_closest_returns_null_for_weak_match()
    {
        var suggestion = NameSuggestion.FindClosest("*", ["Csv", "Postgres", "ClickHouse"]);

        await Assert.That(suggestion).IsNull();
    }
}
