using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Lang.Tests;

public sealed class StatementParsingTests
{
    [Test]
    [DisplayName("LOAD * создает statement со всеми полями")]
    public async Task Load_all_fields()
    {
        var load = ParseLoad("LOAD * FROM [orders.csv];");

        await Assert.That(load.Fields).IsNull();
        await Assert.That(load.Source).IsEqualTo("orders.csv");
        await Assert.That(load.Options).IsEmpty();
    }

    [Test]
    [Arguments("LOAD * FROM [orders.csv];")]
    [Arguments("LOAD*FROM[orders.csv];")]
    [Arguments("  LOAD \r\n * \t FROM \n [orders.csv] ; ")]
    [Arguments("load * from [orders.csv];")]
    [Arguments("Load * From [orders.csv];")]
    [Arguments("LoAd * FrOm [orders.csv];")]
    [DisplayName("LOAD * не зависит от пробелов и регистра ключевых слов")]
    public async Task Load_all_fields_ignores_whitespace_and_keyword_case(string text)
    {
        var load = ParseLoad(text);

        await Assert.That(load.Fields).IsNull();
        await Assert.That(load.Source).IsEqualTo("orders.csv");
    }

    [Test]
    [Arguments("LOAD id AS id FROM [orders.csv];")]
    [Arguments("LOAD id AS id, FROM [orders.csv];")]
    [Arguments("LOAD id as id FROM [orders.csv];")]
    [Arguments("load id As id from [orders.csv];")]
    [Arguments("LOAD   id   AS   id   FROM   [orders.csv]   ;")]
    [DisplayName("LOAD одно поле допускает разные пробелы регистр AS/FROM и trailing comma")]
    public async Task Load_single_field_variants(string text)
    {
        var load = ParseLoad(text);

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(1);
        await AssertField(fields[0], "id", "id");
    }

    [Test]
    [Arguments("LOAD id FROM [orders.csv];", "id", "id")]
    [Arguments("LOAD [gross amount] FROM [orders.csv];", "gross amount", "gross amount")]
    [Arguments(@"LOAD [folder\]id] FROM [orders.csv];", "folder]id", "folder]id")]
    [DisplayName("LOAD поле без AS превращается в name AS name")]
    public async Task Load_field_without_alias_becomes_same_name_alias(string text, string expectedName, string expectedExpressionName)
    {
        var load = ParseLoad(text);

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(1);
        await AssertField(fields[0], expectedName, expectedExpressionName);
    }

    [Test]
    [DisplayName("LOAD список полей смешивает короткую и полную форму")]
    public async Task Load_fields_mix_short_and_full_forms()
    {
        var load = ParseLoad(
            """
            LOAD
                id,
                amount * 1.2 AS gross_amount,
                city,
            FROM [orders.csv];
            """);

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(3);
        await AssertField(fields[0], "id", "id");
        await Assert.That(fields[1].Name).IsEqualTo("gross_amount");
        await Assert.That(fields[1].Expression).IsTypeOf<FuncExpr>();
        await AssertField(fields[2], "city", "city");
    }

    [Test]
    [DisplayName("LOAD несколько полей сохраняет порядок")]
    public async Task Load_multiple_fields_preserves_order()
    {
        var load = ParseLoad(
            """
            LOAD
                id AS id,
                name AS name,
                amount AS amount
            FROM [orders.csv];
            """);

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(3);
        await AssertField(fields[0], "id", "id");
        await AssertField(fields[1], "name", "name");
        await AssertField(fields[2], "amount", "amount");
    }

    [Test]
    [DisplayName("LOAD список полей допускает trailing comma")]
    public async Task Load_fields_with_trailing_comma()
    {
        var load = ParseLoad(
            """
            LOAD
                amount * 1.2 AS gross_amount,
                city.Lower() AS city,
            FROM [orders.csv];
            """);

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(2);
        await Assert.That(fields[0].Name).IsEqualTo("gross_amount");
        await Assert.That(fields[0].Expression).IsTypeOf<FuncExpr>();
        await Assert.That(fields[1].Name).IsEqualTo("city");
        await Assert.That(fields[1].Expression).IsTypeOf<FuncExpr>();
    }

    [Test]
    [DisplayName("LOAD поле поддерживает blocked alias и escaped source")]
    public async Task Load_field_supports_blocked_alias_and_escaped_source()
    {
        var load = ParseLoad(@"LOAD amount AS [gross amount] FROM [folder\]name/orders.csv];");

        await Assert.That(load.Source).IsEqualTo("folder]name/orders.csv");
        await AssertField(ExplicitFields(load)[0], "gross amount", "amount");
    }

    [Test]
    [Arguments("amount * 1.2", "gross_amount", "*")]
    [Arguments("(amount + tax) / 2", "avg_amount", "/")]
    [Arguments("city.Lower()", "city", "Lower")]
    [Arguments("If(active, 'yes', 'no')", "active_text", "If")]
    [Arguments("'hello ${name}'", "message", "+")]
    [Arguments("-amount", "negative_amount", "-")]
    [Arguments("amount > 100 and active", "is_big", "and")]
    [DisplayName("LOAD поле поддерживает разные expression формы")]
    public async Task Load_field_supports_expression_variants(string expression, string alias, string rootFunction)
    {
        var load = ParseLoad($"LOAD {expression} AS {alias} FROM [orders.csv];");

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(1);
        await Assert.That(fields[0].Name).IsEqualTo(alias);
        await Assert.That(fields[0].Expression).IsTypeOf<FuncExpr>();
        var function = (FuncExpr)fields[0].Expression;
        await Assert.That(function.Name).IsEqualTo(rootFunction);
    }

    [Test]
    [Arguments("(csv)")]
    [Arguments("(csv,)")]
    [Arguments("(csv, delimiter=',')")]
    [Arguments("(csv, delimiter=',', header=true, batch=100, ratio=10.5,)")]
    [DisplayName("LOAD options допускают comma-separated options и trailing comma")]
    public async Task Load_options_separator_variants(string options)
    {
        var load = ParseLoad($"LOAD id AS id FROM [orders.csv] {options};");

        await Assert.That(load.Options[0].Name).IsEqualTo("csv");
        await Assert.That(load.Options[0].Value).IsNull();
    }

    [Test]
    [DisplayName("LOAD source options разбирает marker и literal values")]
    public async Task Load_options()
    {
        var load = ParseLoad("LOAD id AS id FROM [orders.csv] (csv, delimiter=',', header=true, batch=100, ratio=10.5);");

        await Assert.That(load.Options).Count().IsEqualTo(5);
        await Assert.That(load.Options[0].Name).IsEqualTo("csv");
        await Assert.That(load.Options[0].Value).IsNull();
        await AssertOption<StringLiteral, string>(load.Options[1], "delimiter", literal => literal.Value, ",");
        await AssertOption<BooleanLiteral, bool>(load.Options[2], "header", literal => literal.Value, true);
        await AssertOption<IntegerLiteral, long>(load.Options[3], "batch", literal => literal.Value, 100);
        await AssertOption<NumberLiteral, double>(load.Options[4], "ratio", literal => literal.Value, 10.5);
    }

    [Test]
    [DisplayName("LOAD source options допускает пустые скобки")]
    public async Task Load_options_allow_empty_parentheses()
    {
        var load = ParseLoad("LOAD id AS id FROM [orders.csv] ();");

        await Assert.That(load.Options).IsEmpty();
    }

    [Test]
    [Arguments("LOAD id AS id FROM [orders.csv] (delimiter=name);")]
    [Arguments("LOAD id AS id FROM [orders.csv] (delimiter=null);")]
    [Arguments("LOAD id AS id FROM [orders.csv] (csv delimiter=',');")]
    [Arguments("LOAD id AS id FROM [orders.csv] (csv delimiter=',' header=true);")]
    [DisplayName("LOAD source options запрещает name null и пропущенные запятые")]
    public async Task Load_options_reject_invalid_values_and_missing_commas(string text)
    {
        await AssertInvalidStatement(text);
    }

    [Test]
    [Arguments("")]
    [Arguments("LOAD * FROM [orders.csv]")]
    [Arguments("LOAD id FROM [orders.csv]")]
    [Arguments("LOAD FROM [orders.csv];")]
    [Arguments("LOAD id [orders.csv];")]
    [Arguments("LOAD id FROM;")]
    [Arguments("LOAD id FROM orders.csv;")]
    [Arguments("LOAD * id FROM [orders.csv];")]
    [Arguments("LOAD *, id FROM [orders.csv];")]
    [Arguments("LOAD id,, name FROM [orders.csv];")]
    [Arguments("LOAD id AS FROM [orders.csv];")]
    [Arguments("LOAD amount + 1 FROM [orders.csv];")]
    [Arguments("LOAD amount + 1 AS FROM [orders.csv];")]
    [Arguments("LOAD amount + 1 AS 123 FROM [orders.csv];")]
    [Arguments("LOAD id FROM [orders.csv] (csv,, delimiter=',');")]
    [Arguments("LOAD id FROM [orders.csv] (csv delimiter=',');")]
    [Arguments("LOAD id FROM [orders.csv] (csv, delimiter=);")]
    [Arguments("LOAD id FROM [orders.csv] (csv, delimiter=null);")]
    [Arguments("LOAD id FROM [orders.csv] (csv, delimiter=name);")]
    [Arguments("LOAD id FROM [orders.csv] (csv, delimiter=',',); extra")]
    [DisplayName("Statement.Parse отклоняет невалидные LOAD statements")]
    public async Task Parse_rejects_invalid_load_statements(string text)
    {
        await AssertInvalidStatement(text);
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("TRUE", true)]
    [Arguments("False", false)]
    [Arguments("false", false)]
    [DisplayName("LOAD boolean option value не зависит от регистра")]
    public async Task Load_boolean_option_case_variants(string value, bool expected)
    {
        var load = ParseLoad($"LOAD id AS id FROM [orders.csv] (header={value});");

        await Assert.That(load.Options).Count().IsEqualTo(1);
        await AssertOption<BooleanLiteral, bool>(load.Options[0], "header", literal => literal.Value, expected);
    }

    [Test]
    [DisplayName("Statement.Parse при ошибке возвращает LangError")]
    public async Task Parse_error_returns_lang_error()
    {
        var result = Statement.Parse("LOAD id AS id FROM;");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<LangError>();
        await Assert.That(result.Error.Message).IsNotEmpty();
    }

    private static LoadStatement ParseLoad(string text)
    {
        var result = Statement.Parse(text);
        return (LoadStatement)result.Value;
    }

    private static async Task AssertField(LoadField field, string name, string expressionName)
    {
        await Assert.That(field.Name).IsEqualTo(name);
        await Assert.That(field.Expression).IsTypeOf<NameExpr>();
        var expression = (NameExpr)field.Expression;
        await Assert.That(expression.Value).IsEqualTo(expressionName);
    }

    private static List<LoadField> ExplicitFields(LoadStatement load)
    {
        return load.Fields ?? throw new InvalidOperationException("Expected explicit LOAD fields, got LOAD *.");
    }

    private static async Task AssertOption<TLiteral, TValue>(
        LoadOption option,
        string name,
        Func<TLiteral, TValue> getValue,
        TValue expected)
        where TLiteral : Literal
    {
        await Assert.That(option.Name).IsEqualTo(name);
        await Assert.That(option.Value).IsTypeOf<TLiteral>();
        var literal = (TLiteral)option.Value!;
        await Assert.That(getValue(literal)).IsEqualTo(expected);
    }

    private static async Task AssertInvalidStatement(string text)
    {
        var result = Statement.Parse(text);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<LangError>();
        await Assert.That(result.Error.Message).IsNotEmpty();
    }
}
