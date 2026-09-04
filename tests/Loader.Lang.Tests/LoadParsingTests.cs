using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Lang.Tests;

public sealed class LoadParsingTests
{
    [Test]
    [DisplayName("LOAD * создает statement со всеми полями")]
    public async Task Load_all_fields()
    {
        var load = ParseLoad("LOAD * FROM Csv(path='orders.csv');");

        await Assert.That(load.TableName).IsEqualTo("tmp");
        await Assert.That(load.Fields).IsNull();
        await Assert.That(load.SourceCall.Name).IsEqualTo("Csv");
        await AssertOption(load.SourceCall, "path", "orders.csv");
        await Assert.That(load.Where).IsNull();
        await Assert.That(load.GroupBy).IsNull();
        await Assert.That(load.OrderBy).IsNull();
        await Assert.That(load.Limit).IsNull();
        await Assert.That(load.Offset).IsNull();
    }

    [Test]
    [Arguments("LOAD * FROM Csv(path='orders.csv');")]
    [Arguments("LOAD*FROM Csv(path='orders.csv');")]
    [Arguments("  LOAD \r\n * \t FROM \n Csv(path='orders.csv') ; ")]
    [Arguments("load * from Csv(path='orders.csv');")]
    [Arguments("Load * From Csv(path='orders.csv');")]
    [Arguments("LoAd * FrOm Csv(path='orders.csv');")]
    [DisplayName("LOAD * не зависит от пробелов и регистра ключевых слов")]
    public async Task Load_all_fields_ignores_whitespace_and_keyword_case(string text)
    {
        var load = ParseLoad(text);

        await Assert.That(load.Fields).IsNull();
        await AssertOption(load.SourceCall, "path", "orders.csv");
    }

    [Test]
    [Arguments("orders: LOAD * FROM Csv(path='orders.csv');", "orders")]
    [Arguments("orders_2026: LOAD * FROM Csv(path='orders.csv');", "orders_2026")]
    [Arguments("_orders: LOAD * FROM Csv(path='orders.csv');", "_orders")]
    [Arguments("orders : LOAD * FROM Csv(path='orders.csv');", "orders")]
    [Arguments("[orders 2026]: LOAD * FROM Csv(path='orders.csv');", "orders 2026")]
    [Arguments(@"[folder\]orders]: LOAD * FROM Csv(path='orders.csv');", "folder]orders")]
    [Arguments("[where]: LOAD * FROM Csv(path='orders.csv');", "where")]
    [DisplayName("LOAD table name prefix задает имя результирующей таблицы")]
    public async Task Load_table_name_prefix_parses_name_before_load(string text, string expectedTableName)
    {
        var load = ParseLoad(text);

        await Assert.That(load.TableName).IsEqualTo(expectedTableName);
        await Assert.That(load.Fields).IsNull();
        await AssertOption(load.SourceCall, "path", "orders.csv");
    }

    [Test]
    public async Task Load_table_name_prefix_parses_name_span()
    {
        var load = ParseLoad("orders: LOAD * FROM Csv(path='orders.csv');");

        await Assert.That(load.TableNameSpan).IsEqualTo(new LangSpan(1, 0, 1, 6));
    }

    [Test]
    [DisplayName("LOAD FIRST ограничивает исходные строки provider перед LOAD")]
    public async Task Load_first_parses_source_row_limit_before_load()
    {
        var load = ParseLoad(
            """
            orders:
            FIRST 1_000
            LOAD *
            FROM Csv(path='orders.csv');
            """);

        await Assert.That(load.TableName).IsEqualTo("orders");
        await Assert.That(load.First).IsEqualTo(1000);
        await Assert.That(load.FirstPart).IsNotNull();
        await Assert.That(load.FirstPart!.Span.StartRow).IsEqualTo(2u);
        await Assert.That(load.Fields).IsNull();
        await AssertOption(load.SourceCall, "path", "orders.csv");
    }

    [Test]
    [DisplayName("LOAD TEMP помечает таблицу как временную")]
    public async Task Load_temp_parses_before_load()
    {
        var load = ParseLoad(
            """
            orders:
            TEMP LOAD *
            FROM Csv(path='orders.csv');
            """);

        await Assert.That(load.TableName).IsEqualTo("orders");
        await Assert.That(load.Kind).IsEqualTo(LoadTableKind.Temp);
        await Assert.That(load.IsTemporary).IsTrue();
        await Assert.That(load.KindSpan).IsNotNull();
        await Assert.That(load.KindSpan!.Value.StartRow).IsEqualTo(2u);
        await Assert.That(load.First).IsNull();
        await AssertOption(load.SourceCall, "path", "orders.csv");
    }

    [Test]
    [DisplayName("LOAD FIRST можно сочетать с TEMP")]
    public async Task Load_first_temp_parses_before_load()
    {
        var load = ParseLoad(
            """
            orders:
            FIRST 10
            TEMP LOAD *
            FROM Csv(path='orders.csv');
            """);

        await Assert.That(load.First).IsEqualTo(10);
        await Assert.That(load.IsTemporary).IsTrue();
    }

    [Test]
    [DisplayName("LOAD допускает temp как обычное имя поля и source table")]
    public async Task Load_allows_temp_keyword_as_name()
    {
        var load = ParseLoad("result: LOAD temp FROM temp;");

        await Assert.That(load.TableName).IsEqualTo("result");
        await Assert.That(load.IsTemporary).IsFalse();
        await Assert.That(load.Kind).IsEqualTo(LoadTableKind.Normal);
        await Assert.That(load.KindSpan).IsNull();
        await AssertField(ExplicitFields(load)[0], "temp", "temp");
        await Assert.That(load.SourceCall.Name).IsEqualTo("Table");
        await AssertOption(load.SourceCall, "name", "temp");
    }

    [Test]
    [Arguments("LOAD * FROM orders;", "orders")]
    [Arguments("LOAD * FROM [orders 2026];", "orders 2026")]
    [Arguments(@"LOAD * FROM [folder\]orders];", "folder]orders")]
    [DisplayName("LOAD FROM table source допускает обычное и blocked имя таблицы")]
    public async Task Load_from_table_source_parses_name_as_table_provider(string text, string expectedTableName)
    {
        var load = ParseLoad(text);

        await Assert.That(load.SourceCall.Name).IsEqualTo("Table");
        await AssertOption(load.SourceCall, "name", expectedTableName);
    }

    [Test]
    [Arguments("LOAD id AS id FROM Csv(path='orders.csv');")]
    [Arguments("LOAD id AS id, FROM Csv(path='orders.csv');")]
    [Arguments("LOAD id as id FROM Csv(path='orders.csv');")]
    [Arguments("load id As id from Csv(path='orders.csv');")]
    [Arguments("LOAD   id   AS   id   FROM   Csv(path='orders.csv')   ;")]
    [DisplayName("LOAD одно поле допускает разные пробелы регистр AS/FROM и trailing comma")]
    public async Task Load_single_field_variants(string text)
    {
        var load = ParseLoad(text);

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(1);
        await AssertField(fields[0], "id", "id");
    }

    [Test]
    [Arguments("LOAD id FROM Csv(path='orders.csv');", "id", "id")]
    [Arguments("LOAD [gross amount] FROM Csv(path='orders.csv');", "gross amount", "gross amount")]
    [Arguments(@"LOAD [folder\]id] FROM Csv(path='orders.csv');", "folder]id", "folder]id")]
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
            FROM Csv(path='orders.csv');
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
            FROM Csv(path='orders.csv');
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
            FROM Csv(path='orders.csv');
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
        var load = ParseLoad(@"LOAD amount AS [gross amount] FROM Csv(path='folder]name/orders.csv');");

        await AssertOption(load.SourceCall, "path", "folder]name/orders.csv");
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
        var load = ParseLoad($"LOAD {expression} AS {alias} FROM Csv(path='orders.csv');");

        var fields = ExplicitFields(load);
        await Assert.That(fields).Count().IsEqualTo(1);
        await Assert.That(fields[0].Name).IsEqualTo(alias);
        await Assert.That(fields[0].Expression).IsTypeOf<FuncExpr>();
        var function = (FuncExpr)fields[0].Expression;
        await Assert.That(function.Name).IsEqualTo(rootFunction);
    }

    [Test]
    [Arguments("Csv(path='orders.csv')")]
    [Arguments("Csv(path='orders.csv',)")]
    [Arguments("Csv(path='orders.csv', delimiter=',')")]
    [Arguments("Csv(path='orders.csv', delimiter=',', header=true, batch=100, ratio=10.5,)")]
    [DisplayName("LOAD options допускают comma-separated options и trailing comma")]
    public async Task Load_options_separator_variants(string options)
    {
        var load = ParseLoad($"LOAD id AS id FROM {options};");

        await Assert.That(load.SourceCall.Name).IsEqualTo("Csv");
        await AssertOption(load.SourceCall, "path", "orders.csv");
    }

    [Test]
    [DisplayName("LOAD source options разбирает literal values без marker-ов")]
    public async Task Load_options()
    {
        var load = ParseLoad("LOAD id AS id FROM Csv(path='orders.csv', delimiter=',', header=true, batch=1_000, ratio=10_000.5_5);");

        await Assert.That(load.SourceCall.Options).Count().IsEqualTo(5);
        await AssertOption(load.SourceCall, "path", "orders.csv");
        await AssertOption(load.SourceCall, "delimiter", ",");
        await AssertOption(load.SourceCall, "header", true);
        await AssertOption(load.SourceCall, "batch", 1000L);
        await AssertOption(load.SourceCall, "ratio", 10000.55);
    }

    [Test]
    [DisplayName("LOAD source options допускают positional literals")]
    public async Task Load_options_support_positional_literals()
    {
        var load = ParseLoad("LOAD id AS id FROM Csv('orders.csv', header=false);");

        await Assert.That(load.SourceCall.Options).Count().IsEqualTo(2);
        await AssertOption(load.SourceCall, "0", "orders.csv");
        await AssertOption(load.SourceCall, "header", false);
    }

    [Test]
    [DisplayName("LOAD source options допускают keyword from как имя option")]
    public async Task Load_options_parse_min_max_range()
    {
        var load = ParseLoad("LOAD * FROM Calendar(min='2024-01-01', max='2024-01-03');");

        await Assert.That(load.SourceCall.Name).IsEqualTo("Calendar");
        await AssertOption(load.SourceCall, "min", "2024-01-01");
        await AssertOption(load.SourceCall, "max", "2024-01-03");
    }

    [Test]
    [DisplayName("LOAD source options различают строку и имя")]
    public async Task Load_options_parse_name_literal()
    {
        var load = ParseLoad("LOAD * FROM Calendar(table=orders, field=[creationDate]);");

        await Assert.That(load.SourceCall.Name).IsEqualTo("Calendar");
        await Assert.That(load.SourceCall.Options.Single(option => option.Name == "table").Value)
            .IsTypeOf<NameLiteral>();
        await Assert.That(load.SourceCall.Options.Single(option => option.Name == "field").Value)
            .IsTypeOf<NameLiteral>();
        await AssertOption(load.SourceCall, "table", "orders");
        await AssertOption(load.SourceCall, "field", "creationDate");
    }

    [Test]
    [DisplayName("LOAD Inline разбирает header и rows")]
    public async Task Load_inline_parses_header_and_rows()
    {
        var load = ParseLoad("LOAD * FROM Inline(id, name, active, amount; 1_000, 'Mike', true, -10_000.5_5; -2_000, null, false, 0);");

        await Assert.That(load.SourceCall.Name).IsEqualTo("Inline");
        await Assert.That(load.SourceCall.Options).IsEmpty();
        await Assert.That(load.SourceCall.InlineData).IsNotNull();
        await Assert.That(load.SourceCall.InlineData!.Columns.Select(static column => column.Name).ToArray())
            .IsEquivalentTo(["id", "name", "active", "amount"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(load.SourceCall.InlineData.Rows).Count().IsEqualTo(2);
        await Assert.That(((IntegerLiteral)load.SourceCall.InlineData.Rows[0].Values[0]).Value).IsEqualTo(1000);
        await Assert.That(((StringLiteral)load.SourceCall.InlineData.Rows[0].Values[1]).Value).IsEqualTo("Mike");
        await Assert.That(((BooleanLiteral)load.SourceCall.InlineData.Rows[0].Values[2]).Value).IsTrue();
        await Assert.That(((NumberLiteral)load.SourceCall.InlineData.Rows[0].Values[3]).Value).IsEqualTo(-10000.55);
        await Assert.That(((IntegerLiteral)load.SourceCall.InlineData.Rows[1].Values[0]).Value).IsEqualTo(-2000);
        await Assert.That(load.SourceCall.InlineData.Rows[1].Values[1]).IsTypeOf<NullLiteral>();
    }

    [Test]
    [DisplayName("LOAD хранит span FROM, source и source options")]
    public async Task Load_from_and_options_keep_spans()
    {
        var load = ParseLoad("LOAD id FROM Connect(name='main_pg', table='public.orders');");

        await Assert.That(load.FromSpan.StartColumn).IsEqualTo(13u);
        await Assert.That(load.FromSpan.EndColumn).IsEqualTo(17u);
        await Assert.That(load.SourceCall.NameSpan.StartColumn).IsEqualTo(18u);
        await Assert.That(load.SourceCall.NameSpan.EndColumn).IsEqualTo(25u);
        await Assert.That(load.SourceCall.Options[0].Span.StartColumn).IsEqualTo(26u);
        await Assert.That(load.SourceCall.Options[0].Span.EndColumn).IsEqualTo(40u);
        await Assert.That(load.SourceCall.Options[1].Span.StartColumn).IsEqualTo(42u);
        await Assert.That(load.SourceCall.Options[1].Span.EndColumn).IsEqualTo(63u);
    }

    [Test]
    [DisplayName("LOAD SQL после FROM сохраняется как source SQL до закрывающей точки с запятой")]
    public async Task Load_sql_after_from_parses_source_sql_until_statement_semicolon()
    {
        var load = ParseLoad(
            """
            LOAD
                id,
                amount
            FROM Connect(name='main_pg')
            SQL
                SELECT id, amount
                FROM public.orders
                WHERE amount > 0
            ;
            """);

        await Assert.That(load.Sql).IsEqualTo(string.Join(
            "\n",
            "SELECT id, amount",
            "    FROM public.orders",
            "    WHERE amount > 0"));
        await Assert.That(load.SqlPart!.Span.StartRow).IsEqualTo(5u);
        await Assert.That(load.SqlPart.Span.EndRow).IsEqualTo(9u);
        await Assert.That(load.Where).IsNull();
        await Assert.That(load.GroupBy).IsNull();
        await Assert.That(load.OrderBy).IsNull();
        await Assert.That(load.Limit).IsNull();
    }

    [Test]
    [DisplayName("LOAD SQL после FROM взаимоисключен с WHERE GROUP ORDER LIMIT")]
    [Arguments("LOAD * FROM Connect(name='main_pg') SQL SELECT * FROM orders WHERE id > 0;")]
    [Arguments("LOAD * FROM Connect(name='main_pg') SQL SELECT * FROM orders GROUP BY id;")]
    [Arguments("LOAD * FROM Connect(name='main_pg') SQL SELECT * FROM orders ORDER BY id;")]
    [Arguments("LOAD * FROM Connect(name='main_pg') SQL SELECT * FROM orders LIMIT 10;")]
    public async Task Load_sql_keeps_sql_keywords_inside_source_sql(string text)
    {
        var load = ParseLoad(text);

        await Assert.That(load.Sql).IsNotNull();
        await Assert.That(load.Where).IsNull();
        await Assert.That(load.GroupBy).IsNull();
        await Assert.That(load.OrderBy).IsNull();
        await Assert.That(load.Limit).IsNull();
    }

    [Test]
    [DisplayName("LOAD source options допускает пустые скобки")]
    public async Task Load_options_allow_empty_parentheses()
    {
        var load = ParseLoad("LOAD id AS id FROM Csv();");

        await Assert.That(load.SourceCall.Options).IsEmpty();
    }

    [Test]
    [DisplayName("LOAD FROM table_name разбирается как ссылка на результат предыдущего LOAD")]
    public async Task Load_from_table_source()
    {
        var load = ParseLoad("second: LOAD * FROM first;");

        await Assert.That(load.TableName).IsEqualTo("second");
        await Assert.That(load.SourceCall.Name).IsEqualTo("Table");
        await AssertOption(load.SourceCall, "name", "first");
    }

    [Test]
    [Arguments("WHERE amount > 100", ">")]
    [Arguments("where amount > 100 and active", "and")]
    [Arguments("WhErE city = 'Moscow' or city = 'London'", "or")]
    [DisplayName("LOAD WHERE разбирает expression после source")]
    public async Task Load_where_parses_expression(string where, string rootFunction)
    {
        var load = ParseLoad($"LOAD id FROM Csv(path='orders.csv') {where};");

        await Assert.That(load.Where).IsNotNull();
        await Assert.That(load.Where).IsTypeOf<FuncExpr>();
        await Assert.That(((FuncExpr)load.Where!).Name).IsEqualTo(rootFunction);
    }

    [Test]
    [DisplayName("LOAD WHERE работает после source options")]
    public async Task Load_where_after_source_options()
    {
        var load = ParseLoad("LOAD id FROM Csv(path='orders.csv', header=true) WHERE active = true;");

        await Assert.That(load.SourceCall.Options).Count().IsEqualTo(2);
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
        var load = ParseLoad($"LOAD city FROM Csv(path='orders.csv') {groupBy};");

        await Assert.That(load.GroupBy!).Count().IsEqualTo(expectedCount);
        await Assert.That(load.GroupBy![0]).IsTypeOf<NameExpr>();
        await Assert.That(((NameExpr)load.GroupBy![0]).Value).IsEqualTo("city");
    }

    [Test]
    [DisplayName("LOAD GROUP BY работает после WHERE и перед ORDER BY")]
    public async Task Load_group_by_after_where_and_before_order_by()
    {
        var load = ParseLoad("LOAD city FROM Csv(path='orders.csv') WHERE active = true GROUP BY city ORDER BY city DESC;");

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
        var load = ParseLoad($"LOAD id FROM Csv(path='orders.csv') {orderBy};");

        await Assert.That(load.OrderBy!).Count().IsEqualTo(1);
        await Assert.That(load.OrderBy![0].Direction).IsEqualTo(expectedDirection);
        await Assert.That(load.OrderBy![0].Expression).IsTypeOf<NameExpr>();
        await Assert.That(((NameExpr)load.OrderBy![0].Expression).Value).IsEqualTo("amount");
    }

    [Test]
    [DisplayName("LOAD ORDER BY несколько полей сохраняет порядок и допускает trailing comma")]
    public async Task Load_order_by_multiple_fields_preserves_order()
    {
        var load = ParseLoad("LOAD id FROM Csv(path='orders.csv') ORDER BY city ASC, amount * 2 DESC, id,;");

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
        var load = ParseLoad("LOAD id FROM Csv(path='orders.csv') WHERE active = true ORDER BY amount DESC;");

        await Assert.That(load.Where).IsNotNull();
        await Assert.That(load.OrderBy!).Count().IsEqualTo(1);
        await Assert.That(load.OrderBy![0].Direction).IsEqualTo(LoadOrderDirection.Descending);
    }

    [Test]
    [Arguments("LIMIT 10", 10L, null)]
    [Arguments("limit 10", 10L, null)]
    [Arguments("LIMIT 10 OFFSET 20", 10L, 20L)]
    [Arguments("LIMIT 1_000 OFFSET 2_000", 1000L, 2000L)]
    [Arguments("LiMiT 10 OfFsEt 20", 10L, 20L)]
    [DisplayName("LOAD LIMIT OFFSET разбирается после source clauses")]
    public async Task Load_limit_offset_parses_integer_values(string clause, long expectedLimit, long? expectedOffset)
    {
        var load = ParseLoad($"LOAD id FROM Csv(path='orders.csv') {clause};");

        await Assert.That(load.Limit).IsEqualTo(expectedLimit);
        await Assert.That(load.Offset).IsEqualTo(expectedOffset);
    }

    [Test]
    [DisplayName("LOAD LIMIT хранит span всего LIMIT clause")]
    public async Task Load_limit_keeps_value_span()
    {
        var load = ParseLoad("LOAD id FROM Csv(path='orders.csv') LIMIT 0;");

        await Assert.That(load.Limit).IsEqualTo(0);
        await Assert.That(load.LimitPart).IsNotNull();
        await Assert.That(load.LimitPart!.Span.StartRow).IsEqualTo(1u);
        await Assert.That(load.LimitPart.Span.StartColumn).IsEqualTo(41u);
        await Assert.That(load.LimitPart.Span.EndColumn).IsEqualTo(48u);
    }

    [Test]
    [DisplayName("LOAD LIMIT OFFSET работает после WHERE GROUP BY ORDER BY")]
    public async Task Load_limit_offset_after_where_group_by_order_by()
    {
        var load = ParseLoad(
            """
            LOAD city
            FROM Csv(path='orders.csv')
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
    [Arguments("LOAD id AS id FROM Csv(path='orders.csv', delimiter=name);")]
    [Arguments("LOAD id AS id FROM Csv(path='orders.csv', delimiter=null);")]
    [Arguments("LOAD id AS id FROM Csv(path='orders.csv' delimiter=',');")]
    [Arguments("LOAD id AS id FROM Csv(path='orders.csv') (csv delimiter=',' header=true);")]
    [DisplayName("LOAD source options запрещает name null и пропущенные запятые")]
    public async Task Load_options_reject_invalid_values_and_missing_commas(string text)
    {
        await AssertInvalidStatement(text);
    }

    [Test]
    [Arguments("")]
    [Arguments("LOAD * FROM Csv(path='orders.csv');")]
    [Arguments("LOAD * FROM Csv(path='orders.csv')")]
    [Arguments("LOAD id FROM Csv(path='orders.csv')")]
    [Arguments("LOAD FROM Csv(path='orders.csv');")]
    [Arguments("LOAD id [orders.csv];")]
    [Arguments("LOAD id FROM;")]
    [Arguments("LOAD id FROM orders.csv;")]
    [Arguments("LOAD * id FROM Csv(path='orders.csv');")]
    [Arguments("LOAD *, id FROM Csv(path='orders.csv');")]
    [Arguments("LOAD id,, name FROM Csv(path='orders.csv');")]
    [Arguments("LOAD id AS FROM Csv(path='orders.csv');")]
    [Arguments("LOAD amount + 1 FROM Csv(path='orders.csv');")]
    [Arguments("LOAD amount + 1 AS FROM Csv(path='orders.csv');")]
    [Arguments("LOAD amount + 1 AS 123 FROM Csv(path='orders.csv');")]
    [Arguments("LOAD id FROM Csv(path='orders.csv',, delimiter=',');")]
    [Arguments("LOAD id FROM Csv(path='orders.csv' delimiter=',');")]
    [Arguments("LOAD id FROM Csv(path='orders.csv', header);")]
    [Arguments("LOAD id FROM Csv(path='orders.csv', delimiter=);")]
    [Arguments("LOAD id FROM Csv(path='orders.csv', delimiter=null);")]
    [Arguments("LOAD id FROM Csv(path='orders.csv', delimiter=name);")]
    [Arguments("LOAD id FROM Csv(path='orders.csv', delimiter=',',); extra")]
    [Arguments("LOAD id WHERE active FROM Csv(path='orders.csv');")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') WHERE;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') WHERE amount > 10 WHERE active;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') WHERE amount > 10 (csv);")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') ORDER;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') ORDER BY;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') ORDER BY id,, name;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') ORDER BY id WHERE active;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') ORDER id;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') GROUP;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') GROUP BY;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') GROUP BY id,, name;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') GROUP BY id WHERE active;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') ORDER BY id GROUP BY id;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') OFFSET 10;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT 10.5;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT -1;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT 10 OFFSET;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT 10 OFFSET 2.5;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT 10 LIMIT 20;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT 10 WHERE active;")]
    [Arguments("LOAD id FROM Csv(path='orders.csv') LIMIT 10 ORDER BY id;")]
    [Arguments("LOAD * FROM Inline(a;);")]
    [Arguments("LOAD * FROM Inline(; 1);")]
    [Arguments("where: LOAD id FROM Csv(path='orders.csv');")]
    [Arguments("123orders: LOAD id FROM Csv(path='orders.csv');")]
    [Arguments("orders-table: LOAD id FROM Csv(path='orders.csv');")]
    [Arguments("orders.table: LOAD id FROM Csv(path='orders.csv');")]
    [Arguments("orders LOAD id FROM Csv(path='orders.csv');")]
    [Arguments("orders: FIRST LOAD * FROM Csv(path='orders.csv');")]
    [Arguments("orders: FIRST 10.5 LOAD * FROM Csv(path='orders.csv');")]
    [Arguments("orders: LOAD FIRST 10 * FROM Csv(path='orders.csv');")]
    [Arguments("orders: LOAD * FIRST 10 FROM Csv(path='orders.csv');")]
    [Arguments("orders: LOAD TEMP * FROM Csv(path='orders.csv');")]
    [Arguments("orders: TEMP FIRST 10 LOAD * FROM Csv(path='orders.csv');")]
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
        var load = ParseLoad($"LOAD id AS id FROM Csv(path='orders.csv', header={value});");

        await Assert.That(load.SourceCall.Options).Count().IsEqualTo(2);
        await AssertOption(load.SourceCall, "header", expected);
    }

    [Test]
    [DisplayName("Script.Parse разбирает несколько LOAD statement и сохраняет порядок")]
    public async Task Script_parse_multiple_load_statements_preserves_order()
    {
        var result = Script.Parse(
            """
            orders: LOAD * FROM Csv(path='orders.csv');
            customers: LOAD id, amount AS amount FROM Excel(path='customers.xlsx', sheet='Sheet1');
            """);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Statements).Count().IsEqualTo(2);

        var first = (LoadStatement)result.Value.Statements[0];
        var second = (LoadStatement)result.Value.Statements[1];

        await AssertOption(first.SourceCall, "path", "orders.csv");
        await Assert.That(first.Fields).IsNull();
        await AssertOption(second.SourceCall, "path", "customers.xlsx");
        await Assert.That(second.Fields).IsNotNull();
        await Assert.That(second.Fields!).Count().IsEqualTo(2);
    }

    [Test]
    [DisplayName("Script.Parse допускает комментарии и переносы между statement")]
    public async Task Script_parse_allows_comments_and_whitespace_between_statements()
    {
        var result = Script.Parse(
            """
            // first source
            orders: LOAD id FROM Csv(path='orders.csv');

            /*
              second source
            */
            users: LOAD name FROM Csv(path='users.csv');
            """);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Statements).Count().IsEqualTo(2);
        await AssertOption(((LoadStatement)result.Value.Statements[0]).SourceCall, "path", "orders.csv");
        await AssertOption(((LoadStatement)result.Value.Statements[1]).SourceCall, "path", "users.csv");
    }

    [Test]
    [DisplayName("Script.Parse пустой скрипт считает ошибкой")]
    public async Task Script_parse_rejects_empty_script()
    {
        var result = Script.Parse("");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Message).IsNotEmpty();
    }

    [Test]
    [DisplayName("Script.Parse если один statement невалиден возвращает LangError")]
    public async Task Script_parse_returns_error_when_any_statement_is_invalid()
    {
        var result = Script.Parse(
            """
            orders: LOAD id FROM Csv(path='orders.csv');
            broken: LOAD id FROM Csv(path='broken.csv')
            users: LOAD name FROM Csv(path='users.csv');
            """);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<LangError>();
        await Assert.That(result.Error.Message).IsNotEmpty();
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

    [Test]
    [Arguments("tmp: FIRST 10_ LOAD * FROM Numbers(10);")]
    [Arguments("tmp: LOAD * FROM Numbers(10_) LIMIT 10;")]
    [Arguments("tmp: LOAD * FROM Numbers(10) LIMIT 10_;")]
    [Arguments("tmp: LOAD * FROM Inline(id; 1_);")]
    [DisplayName("LOAD возвращает понятную ошибку для некорректного числового литерала")]
    public async Task Load_invalid_numeric_literal_returns_domain_error(string text)
    {
        var result = Statement.Parse(text);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<LangError>();
        await Assert.That(result.Error.Message).Contains("Некорректный числовой литерал");
    }

    private static LoadStatement ParseLoad(string text)
    {
        var result = Statement.Parse(EnsureTableName(text));
        return (LoadStatement)result.Value;
    }

    private static string EnsureTableName(string text)
    {
        var loadIndex = text.IndexOf("LOAD", StringComparison.OrdinalIgnoreCase);
        if (loadIndex < 0)
        {
            return text;
        }

        return text[..loadIndex].Contains(':', StringComparison.Ordinal)
            ? text
            : text.Insert(loadIndex, "tmp: ");
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

    private static async Task AssertOption(LoadSourceCall sourceCall, string name, object expected)
    {
        var option = sourceCall.Options.SingleOrDefault(option => option.Name == name);
        await Assert.That(option).IsNotNull();
        await AssertOption(option!, name, expected);
    }

    private static async Task AssertOption(LoadOption option, string name, object expected)
    {
        var actual = LiteralValue(option.Value);
        await Assert.That(option.Name).IsEqualTo(name);
        await Assert.That(actual).IsEqualTo(expected);
    }

    private static object LiteralValue(Literal literal)
    {
        return literal switch
        {
            StringLiteral value => value.Value,
            BooleanLiteral value => value.Value,
            IntegerLiteral value => value.Value,
            NumberLiteral value => value.Value,
            NameLiteral value => value.Value,
            _ => throw new InvalidOperationException($"Unexpected option literal type '{literal.GetType().Name}'.")
        };
    }

    private static async Task AssertInvalidStatement(string text)
    {
        var result = Statement.Parse(text);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<LangError>();
        await Assert.That(result.Error.Message).IsNotEmpty();
    }
}
