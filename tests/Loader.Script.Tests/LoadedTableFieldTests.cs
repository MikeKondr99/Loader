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
        await Assert.That(field.StringMaxLength).IsNull();
    }

    [Test]
    public async Task Field_stores_optional_cardinality_density_nullability_and_string_length()
    {
        var field = new LoadedTableField
        {
            Name = "city",
            DataType = DataType.Text,
            Cardinality = 12,
            Density = 90,
            CanBeNull = true,
            StringMaxLength = 128
        };

        await Assert.That(field.Cardinality).IsEqualTo(12);
        await Assert.That(field.Density).IsEqualTo(90);
        await Assert.That(field.CanBeNull).IsTrue();
        await Assert.That(field.StringMaxLength).IsEqualTo(128);
    }

    [Test]
    public async Task Field_typed_min_max_returns_values_when_type_matches()
    {
        var date = new DateOnly(2026, 1, 2);
        var dateTime = new DateTime(2026, 1, 2, 3, 4, 5);
        var decimalField = CreateField("amount", DataType.Number, 10.5m, 99.9m);
        var longField = CreateField("id", DataType.Integer, 1L, 10L);
        var stringField = CreateField("city", DataType.Text, "Amsterdam", "Zurich");
        var dateField = CreateField("date", DataType.Date, date, date);
        var dateTimeField = CreateField("created_at", DataType.DateTime, dateTime, dateTime);

        await Assert.That(decimalField.GetMin<decimal>()).IsEqualTo(10.5m);
        await Assert.That(decimalField.GetMax<decimal>()).IsEqualTo(99.9m);
        await Assert.That(longField.GetMin<long>()).IsEqualTo(1L);
        await Assert.That(longField.GetMax<long>()).IsEqualTo(10L);
        await Assert.That(stringField.GetMin<string>()).IsEqualTo("Amsterdam");
        await Assert.That(stringField.GetMax<string>()).IsEqualTo("Zurich");
        await Assert.That(dateField.GetMin<DateOnly>()).IsEqualTo(date);
        await Assert.That(dateField.GetMax<DateOnly>()).IsEqualTo(date);
        await Assert.That(dateTimeField.GetMin<DateTime>()).IsEqualTo(dateTime);
        await Assert.That(dateTimeField.GetMax<DateTime>()).IsEqualTo(dateTime);
    }

    [Test]
    public async Task Field_typed_min_max_returns_null_when_value_absent()
    {
        var field = new LoadedTableField
        {
            Name = "amount",
            DataType = DataType.Number
        };

        await Assert.That(field.GetMin<decimal>()).IsNull();
        await Assert.That(field.GetMax<decimal>()).IsNull();
    }

    [Test]
    public async Task Field_typed_min_max_throws_when_type_does_not_match()
    {
        var field = CreateField("amount", DataType.Number, 10.5m, 99.9m);

        await Assert.That(() => field.GetMin<long>())
            .ThrowsExactly<InvalidCastException>();
        await Assert.That(() => field.GetMax<long>())
            .ThrowsExactly<InvalidCastException>();
    }

    private static LoadedTableField CreateField(string name, DataType dataType, object min, object max)
    {
        return new LoadedTableField
        {
            Name = name,
            DataType = dataType,
            Min = min,
            Max = max
        };
    }
}
