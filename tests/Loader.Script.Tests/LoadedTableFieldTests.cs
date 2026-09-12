using Loader.Core.Models;

namespace Loader.Script.Tests;

public sealed class LoadedTableFieldTests
{
    [Test]
    public async Task Field_requires_only_name_and_data_type_metadata_can_be_absent()
    {
        var field = new LoadedTableField
        {
            Name = "amount",
            DataType = DataType.Number
        };

        await Assert.That(field.Name).IsEqualTo("amount");
        await Assert.That(field.DataType).IsEqualTo(DataType.Number);
        await Assert.That(field.Cardinality).IsNull();
        await Assert.That(field.Density).IsNull();
        await Assert.That(field.CanBeNull).IsFalse();
        await Assert.That(field.Min).IsNull();
        await Assert.That(field.Max).IsNull();
    }

    [Test]
    public async Task Field_stores_optional_cardinality_density_nullability_and_bounds()
    {
        var field = new LoadedTableField
        {
            Name = "city",
            DataType = DataType.Text,
            Cardinality = 12,
            Density = 90,
            CanBeNull = true,
            Min = "Amsterdam",
            Max = "Zurich"
        };

        await Assert.That(field.Cardinality).IsEqualTo(12);
        await Assert.That(field.Density).IsEqualTo(90);
        await Assert.That(field.CanBeNull).IsTrue();
        await Assert.That(field.Min).IsEqualTo("Amsterdam");
        await Assert.That(field.Max).IsEqualTo("Zurich");
    }
}
