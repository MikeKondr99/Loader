namespace Loader.Core.Tests;

public sealed class DataFieldNameDeduplicatorTests
{
    [Test]
    public async Task Deduplicate_preserves_original_names_before_generated_suffixes()
    {
        var names = DataFieldNameDeduplicator.Deduplicate(["x", "x", "x_2", "x_3", "x"]);

        await Assert.That(names)
            .IsEquivalentTo(["x", "x_4", "x_2", "x_3", "x_5"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
