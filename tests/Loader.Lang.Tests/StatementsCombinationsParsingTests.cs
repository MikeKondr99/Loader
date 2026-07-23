using Loader.Lang.Statements;

namespace Loader.Lang.Tests;

public sealed class StatementsCombinationsParsingTests
{
    [Test]
    [DisplayName("Script.Parse разбирает несколько LOAD statement и сохраняет порядок")]
    public async Task Parse_multiple_load_statements_preserves_order()
    {
        var result = Script.Parse(
            """
            LOAD * FROM [orders.csv] (csv);
            LOAD id, amount AS amount FROM [customers.xlsx] (excel, sheet='Sheet1');
            """);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Statements).Count().IsEqualTo(2);

        var first = (LoadStatement)result.Value.Statements[0];
        var second = (LoadStatement)result.Value.Statements[1];

        await Assert.That(first.Source).IsEqualTo("orders.csv");
        await Assert.That(first.Fields).IsNull();
        await Assert.That(second.Source).IsEqualTo("customers.xlsx");
        await Assert.That(second.Fields).IsNotNull();
        await Assert.That(second.Fields!).Count().IsEqualTo(2);
    }

    [Test]
    [DisplayName("Script.Parse допускает комментарии и переносы между statement")]
    public async Task Parse_allows_comments_and_whitespace_between_statements()
    {
        var result = Script.Parse(
            """
            // first source
            LOAD id FROM [orders.csv];

            /*
              second source
            */
            LOAD name FROM [users.csv];
            """);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Statements).Count().IsEqualTo(2);
        await Assert.That(((LoadStatement)result.Value.Statements[0]).Source).IsEqualTo("orders.csv");
        await Assert.That(((LoadStatement)result.Value.Statements[1]).Source).IsEqualTo("users.csv");
    }

    [Test]
    [DisplayName("Script.Parse пустой скрипт считает ошибкой")]
    public async Task Parse_rejects_empty_script()
    {
        var result = Script.Parse("");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Message).IsNotEmpty();
    }

    [Test]
    [DisplayName("Script.Parse если один statement невалиден возвращает LangError")]
    public async Task Parse_returns_error_when_any_statement_is_invalid()
    {
        var result = Script.Parse(
            """
            LOAD id FROM [orders.csv];
            LOAD id FROM [broken.csv]
            LOAD name FROM [users.csv];
            """);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<LangError>();
        await Assert.That(result.Error.Message).IsNotEmpty();
    }
}
