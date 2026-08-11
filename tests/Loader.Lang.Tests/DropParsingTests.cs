using Loader.Lang.Statements;

namespace Loader.Lang.Tests;

public sealed class DropParsingTests
{
    [Test]
    [Arguments("DROP orders;", "orders")]
    [Arguments("drop orders;", "orders")]
    [Arguments("DROP [orders 2026];", "orders 2026")]
    [Arguments(@"DROP [folder\]orders];", "folder]orders")]
    [Arguments("DROP [where];", "where")]
    [DisplayName("DROP разбирает обычное и blocked имя таблицы")]
    public async Task Drop_parses_table_name(string text, string expectedName)
    {
        var result = Statement.Parse(text);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTypeOf<DropStatement>();
        var drop = (DropStatement)result.Value!;
        await Assert.That(drop.Name).IsEqualTo(expectedName);
        await Assert.That(drop.NameSpan.StartRow).IsGreaterThan(0u);
    }

    [Test]
    [DisplayName("DROP не принимает keyword как обычное имя таблицы")]
    public async Task Drop_rejects_keyword_name_without_blocking()
    {
        var result = Statement.Parse("DROP where;");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<LangError>();
    }

    [Test]
    [DisplayName("Script.Parse разбирает LOAD и DROP statement по порядку")]
    public async Task Script_parse_preserves_load_drop_order()
    {
        var result = Script.Parse(
            """
            orders: LOAD * FROM Csv(path='orders.csv');
            DROP orders;
            """);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Statements).Count().IsEqualTo(2);
        await Assert.That(result.Value.Statements[0]).IsTypeOf<LoadStatement>();
        await Assert.That(result.Value.Statements[1]).IsTypeOf<DropStatement>();
        await Assert.That(((DropStatement)result.Value.Statements[1]).Name).IsEqualTo("orders");
    }
}
