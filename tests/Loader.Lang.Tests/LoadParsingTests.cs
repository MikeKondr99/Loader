using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Lang.Tests;

public sealed class LoadParsingTests
{
    [Test]
    [DisplayName("LOAD * создает statement со всеми полями")]
    public async Task Load_all_fields()
    {
        var load = ParseLoad("LOAD * FROM [orders.csv];");

        await Assert.That(load.TableName).IsNull();
        await Assert.That(load.Fields).IsNull();
        await Assert.That(load.Source).IsEqualTo("orders.csv");
        await Assert.That(load.Options).IsEmpty();
        await Assert.That(load.Where).IsNull();
        await Assert.That(load.GroupBy).IsNull();
        await Assert.That(load.OrderBy).IsNull();
        await Assert.That(load.Limit).IsNull();
        await Assert.That(load.Offset).IsNull();
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
    [Arguments("orders: LOAD * FROM [orders.csv];", "orders")]
    [Arguments("orders_2026: LOAD * FROM [orders.csv];", "orders_2026")]
    [Arguments("_orders: LOAD * FROM [orders.csv];", "_orders")]
    [Arguments("orders : LOAD * FROM [orders.csv];", "orders")]
    [DisplayName("LOAD table name prefix задает имя результирующей таблицы")]
    public async Task Load_table_name_prefix_parses_name_before_load(string text, string expectedTableName)
    {
        var load = ParseLoad(text);

        await Assert.That(load.TableName).IsEqualTo(expectedTableName);
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
    [Arguments("WHERE amount > 100", ">")]
    [Arguments("where amount > 100 and active", "and")]
    [Arguments("WhErE city = 'Moscow' or city = 'London'", "or")]
    [DisplayName("LOAD WHERE разбирает expression после source")]
    public async Task Load_where_parses_expression(string where, string rootFunction)
    {
        var load = ParseLoad($"LOAD id FROM [orders.csv] {where};");

        await Assert.That(load.Where).IsNotNull();
        await Assert.That(load.Where).IsTypeOf<FuncExpr>();
        await Assert.That(((FuncExpr)load.Where!).Name).IsEqualTo(rootFunction);
    }

    [Test]
    [DisplayName("LOAD WHERE работает после source options")]
    public async Task Load_where_after_source_options()
    {
        var load = ParseLoad("LOAD id FROM [orders.csv] (csv, header=true) WHERE active = true;");

        await Assert.That(load.Options).Count().IsEqualTo(2);
        await Assert.That(load.Where).IsNotNull();
        await Assert.That(load.Where).IsTypeOf<FuncExpr>();
        await Assert.That(((FuncExpr)load.Where!).Name).IsEqualTo("=");
    }

    [Test]
    [Arguments("GROUP BY city", 1)]
    [Arguments("group by city, amount", 2)]
    [Arguments("GrOuP By city, created.Date(),", 2)]
    [DisplayName("LOAD GROUP BY разбирает список expressions после source или WHERE")]
    public async Task Load_group_by_parses_expression_list(string groupBy, int expectedCount)
    {
        var load = ParseLoad($"LOAD city FROM [orders.csv] {groupBy};");

        await Assert.That(load.GroupBy!).Count().IsEqualTo(expectedCount);
        await Assert.That(load.GroupBy![0]).IsTypeOf<NameExpr>();
        await Assert.That(((NameExpr)load.GroupBy![0]).Value).IsEqualTo("city");
    }

    [Test]
    [DisplayName("LOAD GROUP BY работает после WHERE и перед ORDER BY")]
    public async Task Load_group_by_after_where_and_before_order_by()
    {
        var load = ParseLoad("LOAD city FROM [orders.csv] WHERE active = true GROUP BY city ORDER BY city DESC;");

        await Assert.That(load.Where).IsNotNull();
        await Assert.That(load.GroupBy!).Count().IsEqualTo(1);
        await Assert.That(((NameExpr)load.GroupBy![0]).Value).IsEqualTo("city");
        await Assert.That(load.OrderBy!).Count().IsEqualTo(1);
        await Assert.That(load.OrderBy![0].Direction).IsEqualTo(LoadOrderDirection.Descending);
    }

    [Test]
    [Arguments("ORDER BY amount", LoadOrderDirection.Ascending)]
    [Arguments("ORDER BY amount ASC", LoadOrderDirection.Ascending)]
    [Arguments("ORDER BY amount asc", LoadOrderDirection.Ascending)]
    [Arguments("ORDER BY amount DESC", LoadOrderDirection.Descending)]
    [Arguments("order by amount desc", LoadOrderDirection.Descending)]
    [DisplayName("LOAD ORDER BY разбирает одно поле и направление сортировки")]
    public async Task Load_order_by_single_field(string orderBy, LoadOrderDirection expectedDirection)
    {
        var load = ParseLoad($"LOAD id FROM [orders.csv] {orderBy};");

        await Assert.That(load.OrderBy!).Count().IsEqualTo(1);
        await Assert.That(load.OrderBy![0].Direction).IsEqualTo(expectedDirection);
        await Assert.That(load.OrderBy![0].Expression).IsTypeOf<NameExpr>();
        await Assert.That(((NameExpr)load.OrderBy![0].Expression).Value).IsEqualTo("amount");
    }

    [Test]
    [DisplayName("LOAD ORDER BY несколько полей сохраняет порядок и допускает trailing comma")]
    public async Task Load_order_by_multiple_fields_preserves_order()
    {
        var load = ParseLoad("LOAD id FROM [orders.csv] ORDER BY city ASC, amount * 2 DESC, id,;");

        await Assert.That(load.OrderBy!).Count().IsEqualTo(3);
        await Assert.That(((NameExpr)load.OrderBy![0].Expression).Value).IsEqualTo("city");
        await Assert.That(load.OrderBy![0].Direction).IsEqualTo(LoadOrderDirection.Ascending);
        await Assert.That(load.OrderBy![1].Expression).IsTypeOf<FuncExpr>();
        await Assert.That(load.OrderBy![1].Direction).IsEqualTo(LoadOrderDirection.Descending);
        await Assert.That(((NameExpr)load.OrderBy![2].Expression).Value).IsEqualTo("id");
        await Assert.That(load.OrderBy![2].Direction).IsEqualTo(LoadOrderDirection.Ascending);
    }

    [Test]
    [DisplayName("LOAD ORDER BY работает после WHERE")]
    public async Task Load_order_by_after_where()
    {
        var load = ParseLoad("LOAD id FROM [orders.csv] WHERE active = true ORDER BY amount DESC;");

        await Assert.That(load.Where).IsNotNull();
        await Assert.That(load.OrderBy!).Count().IsEqualTo(1);
        await Assert.That(load.OrderBy![0].Direction).IsEqualTo(LoadOrderDirection.Descending);
    }

    [Test]
    [Arguments("LIMIT 10", 10L, null)]
    [Arguments("limit 10", 10L, null)]
    [Arguments("LIMIT 10 OFFSET 20", 10L, 20L)]
    [Arguments("LiMiT 10 OfFsEt 20", 10L, 20L)]
    [DisplayName("LOAD LIMIT OFFSET разбирается после source clauses")]
    public async Task Load_limit_offset_parses_integer_values(string clause, long expectedLimit, long? expectedOffset)
    {
        var load = ParseLoad($"LOAD id FROM [orders.csv] {clause};");

        await Assert.That(load.Limit).IsEqualTo(expectedLimit);
        await Assert.That(load.Offset).IsEqualTo(expectedOffset);
    }

    [Test]
    [DisplayName("LOAD LIMIT OFFSET работает после WHERE GROUP BY ORDER BY")]
    public async Task Load_limit_offset_after_where_group_by_order_by()
    {
        var load = ParseLoad(
            """
            LOAD city
            FROM [orders.csv]
            WHERE active = true
            GROUP BY city
            ORDER BY city DESC
            LIMIT 10
            OFFSET 20;
            """);

        await Assert.That(load.Where).IsNotNull();
        await Assert.That(load.GroupBy!).Count().IsEqualTo(1);
        await Assert.That(load.OrderBy!).Count().IsEqualTo(1);
        await Assert.That(load.Limit).IsEqualTo(10);
        await Assert.That(load.Offset).IsEqualTo(20);
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
    [Arguments("LOAD id WHERE active FROM [orders.csv];")]
    [Arguments("LOAD id FROM [orders.csv] WHERE;")]
    [Arguments("LOAD id FROM [orders.csv] WHERE amount > 10 WHERE active;")]
    [Arguments("LOAD id FROM [orders.csv] WHERE amount > 10 (csv);")]
    [Arguments("LOAD id FROM [orders.csv] ORDER;")]
    [Arguments("LOAD id FROM [orders.csv] ORDER BY;")]
    [Arguments("LOAD id FROM [orders.csv] ORDER BY id,, name;")]
    [Arguments("LOAD id FROM [orders.csv] ORDER BY id WHERE active;")]
    [Arguments("LOAD id FROM [orders.csv] ORDER id;")]
    [Arguments("LOAD id FROM [orders.csv] GROUP;")]
    [Arguments("LOAD id FROM [orders.csv] GROUP BY;")]
    [Arguments("LOAD id FROM [orders.csv] GROUP BY id,, name;")]
    [Arguments("LOAD id FROM [orders.csv] GROUP BY id WHERE active;")]
    [Arguments("LOAD id FROM [orders.csv] ORDER BY id GROUP BY id;")]
    [Arguments("LOAD id FROM [orders.csv] OFFSET 10;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT 10.5;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT -1;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT 10 OFFSET;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT 10 OFFSET 2.5;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT 10 LIMIT 20;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT 10 WHERE active;")]
    [Arguments("LOAD id FROM [orders.csv] LIMIT 10 ORDER BY id;")]
    [Arguments("[orders]: LOAD id FROM [orders.csv];")]
    [Arguments("where: LOAD id FROM [orders.csv];")]
    [Arguments("123orders: LOAD id FROM [orders.csv];")]
    [Arguments("orders-table: LOAD id FROM [orders.csv];")]
    [Arguments("orders.table: LOAD id FROM [orders.csv];")]
    [Arguments("orders LOAD id FROM [orders.csv];")]
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
