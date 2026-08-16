using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadUnionStatementTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadUnionStatementTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("Script Union объединяет таблицы по логическим alias полей")]
    public async Task Execute_script_union_aligns_loaded_tables_by_field_alias()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders_a:
            LOAD
                id,
                city,
                amount
            FROM Inline(id, city, amount;
                1, 'Moscow', 10.5;
                2, 'Berlin', 20.0);

            orders_b:
            LOAD
                order_id AS id,
                town AS city,
                total
            FROM Inline(order_id, town, total;
                3, 'London', 30.5;
                4, 'Paris', 40.0);

            orders_all:
            LOAD
                id,
                city,
                amount,
                total
            FROM Union(orders_a, orders_b)
            WHERE city != 'Berlin'
            ORDER BY id ASC;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(3);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "city", "amount", "total"],
            [
                new object?[] { 1L, "Moscow", 10.5m, null },
                new object?[] { 3L, "London", null, 30.5m },
                new object?[] { 4L, "Paris", null, 40.0m }
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Union сохраняет порядок полей по первому появлению")]
    public async Task Execute_script_union_preserves_field_order_by_first_appearance()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                a,
                b
            FROM Inline(a, b;
                1, 'one');

            second_source:
            LOAD
                c,
                a
            FROM Inline(c, a;
                'extra', 2);

            result:
            LOAD *
            FROM Union(first_source, second_source)
            ORDER BY a ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["a", "b", "c"],
            [
                new object?[] { 1L, "one", null },
                new object?[] { 2L, null, "extra" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union работает с blocked table и field names")]
    public async Task Execute_script_union_supports_blocked_table_and_field_names()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            [orders 2026]:
            LOAD
                id,
                city AS [city name]
            FROM Inline(id, city;
                1, 'Moscow');

            [orders archive]:
            LOAD
                order_id AS id,
                town AS [city name],
                comment AS [extra note]
            FROM Inline(order_id, town, comment;
                2, 'London', 'old');

            result:
            LOAD
                id,
                [city name],
                [extra note]
            FROM Union([orders 2026], [orders archive])
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "city name", "extra note"],
            [
                new object?[] { 1L, "Moscow", null },
                new object?[] { 2L, "London", "old" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union поддерживает WHERE GROUP BY ORDER BY поверх объединенной схемы")]
    public async Task Execute_script_union_supports_query_transformations_after_union()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            sales_a:
            LOAD
                city,
                amount
            FROM Inline(city, amount;
                'Moscow', 10.0;
                'London', 20.0);

            sales_b:
            LOAD
                town AS city,
                total AS amount
            FROM Inline(town, total;
                'Moscow', 30.0;
                'Berlin', 40.0);

            result:
            LOAD
                city,
                SUM(amount) AS total_amount
            FROM Union(sales_a, sales_b)
            WHERE city != 'Berlin'
            GROUP BY city
            ORDER BY city ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["city", "total_amount"],
            [
                new object?[] { "London", 20.0m },
                new object?[] { "Moscow", 40.0m }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union объединяет три таблицы и заполняет отсутствующие поля null")]
    public async Task Execute_script_union_supports_three_tables_with_sparse_fields()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            source_a:
            LOAD
                id,
                a
            FROM Inline(id, a;
                1, 'a');

            source_b:
            LOAD
                id,
                b
            FROM Inline(id, b;
                2, 'b');

            source_c:
            LOAD
                id,
                c
            FROM Inline(id, c;
                3, 'c');

            result:
            LOAD *
            FROM Union(source_a, source_b, source_c)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[3],
            ["id", "a", "b", "c"],
            [
                new object?[] { 1L, "a", null, null },
                new object?[] { 2L, null, "b", null },
                new object?[] { 3L, null, null, "c" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union сводит Integer и Number к Number")]
    public async Task Execute_script_union_merges_integer_and_number_to_number()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            ints:
            LOAD
                id,
                value
            FROM Inline(id, value;
                1, 10);

            nums:
            LOAD
                id,
                value
            FROM Inline(id, value;
                2, 10.5);

            result:
            LOAD
                id,
                value,
                Type(value) AS value_type
            FROM Union(ints, nums)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "value", "value_type"],
            [
                new object?[] { 1L, 10.0m, "num" },
                new object?[] { 2L, 10.5m, "num" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union сводит несовместимые типы к Text")]
    public async Task Execute_script_union_merges_incompatible_types_to_text()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            ints:
            LOAD
                id,
                value
            FROM Inline(id, value;
                1, 10);

            texts:
            LOAD
                id,
                value
            FROM Inline(id, value;
                2, 'manual');

            result:
            LOAD
                id,
                value,
                Type(value) AS value_type
            FROM Union(ints, texts)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "value", "value_type"],
            [
                new object?[] { 1L, "10", "text" },
                new object?[] { 2L, "manual", "text" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union сводит Boolean и Integer к Integer")]
    public async Task Execute_script_union_merges_boolean_and_integer_to_integer()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            bools:
            LOAD
                id,
                flag
            FROM Inline(id, flag;
                1, true;
                2, false);

            ints:
            LOAD
                id,
                flag
            FROM Inline(id, flag;
                3, 10);

            result:
            LOAD
                id,
                flag,
                Type(flag) AS flag_type
            FROM Union(bools, ints)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "flag", "flag_type"],
            [
                new object?[] { 1L, 1L, "int" },
                new object?[] { 2L, 0L, "int" },
                new object?[] { 3L, 10L, "int" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union допускает повтор одной и той же таблицы")]
    public async Task Execute_script_union_allows_same_table_more_than_once()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'A';
                2, 'B');

            result:
            LOAD *
            FROM Union(source, source)
            ORDER BY id ASC, name ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[1],
            ["id", "name"],
            [
                new object?[] { 1L, "A" },
                new object?[] { 1L, "A" },
                new object?[] { 2L, "B" },
                new object?[] { 2L, "B" }
            ],
            "ORDER BY `column1` ASC, `column2` ASC");
    }

    [Test]
    [DisplayName("Script Union не схлопывает одинаковые строки")]
    public async Task Execute_script_union_all_preserves_duplicate_rows()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'same');

            second_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'same');

            result:
            LOAD *
            FROM Union(first_source, second_source)
            ORDER BY id ASC, name ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "name"],
            [
                new object?[] { 1L, "same" },
                new object?[] { 1L, "same" }
            ],
            "ORDER BY `column1` ASC, `column2` ASC");
    }

    [Test]
    [DisplayName("Script Union сопоставляет поля по alias даже если физический порядок разный")]
    public async Task Execute_script_union_ignores_source_column_order_for_same_aliases()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                id,
                name,
                amount
            FROM Inline(id, name, amount;
                1, 'A', 10.0);

            second_source:
            LOAD
                amount,
                id,
                name
            FROM Inline(amount, id, name;
                20.0, 2, 'B');

            result:
            LOAD *
            FROM Union(first_source, second_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "name", "amount"],
            [
                new object?[] { 1L, "A", 10.0m },
                new object?[] { 2L, "B", 20.0m }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union LOAD star добавляет новые поля в порядке таблиц")]
    public async Task Execute_script_union_star_orders_new_fields_by_table_order()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD
                id,
                name,
                amount
            FROM Inline(id, name, amount;
                1, 'A', 10.0);

            totals:
            LOAD
                id,
                name,
                total
            FROM Inline(id, name, total;
                2, 'B', 20.0);

            flags:
            LOAD
                id,
                flag
            FROM Inline(id, flag;
                3, true);

            result:
            LOAD *
            FROM Union(orders, totals, flags)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[3],
            ["id", "name", "amount", "total", "flag"],
            [
                new object?[] { 1L, "A", 10.0m, null, null },
                new object?[] { 2L, "B", null, 20.0m, null },
                new object?[] { 3L, null, null, null, true }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union сохраняет полностью null строку")]
    public async Task Execute_script_union_preserves_all_null_row()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                null, null);

            second_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'A');

            result:
            LOAD *
            FROM Union(first_source, second_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "name"],
            [
                new object?[] { "1", "A" },
                new object?[] { null, null }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union поддерживает полностью null column")]
    public async Task Execute_script_union_preserves_all_null_column()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                id,
                maybe
            FROM Inline(id, maybe;
                1, null);

            second_source:
            LOAD
                id,
                maybe
            FROM Inline(id, maybe;
                2, null);

            result:
            LOAD *
            FROM Union(first_source, second_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "maybe"],
            [
                new object?[] { 1L, null },
                new object?[] { 2L, null }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union объединяет нашу агрегацию и готовую агрегированную таблицу")]
    public async Task Execute_script_union_combines_computed_and_preaggregated_tables()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            sales_raw:
            LOAD
                city,
                amount
            FROM Inline(city, amount;
                'Moscow', 10.0;
                'Moscow', 20.0;
                'London', 15.0);

            sales_agg:
            LOAD
                city,
                SUM(amount) AS total
            FROM sales_raw
            GROUP BY city;

            sales_pre_agg:
            LOAD
                city,
                total
            FROM Inline(city, total;
                'Berlin', 40.0;
                'Moscow', 50.0);

            result:
            LOAD *
            FROM Union(sales_agg, sales_pre_agg)
            ORDER BY city ASC, total ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[3],
            ["city", "total"],
            [
                new object?[] { "Berlin", 40.0m },
                new object?[] { "London", 15.0m },
                new object?[] { "Moscow", 30.0m },
                new object?[] { "Moscow", 50.0m }
            ],
            "ORDER BY `column1` ASC, `column2` ASC");
    }

    [Test]
    [DisplayName("Script Union различает поля только по case")]
    public async Task Execute_script_union_treats_field_names_as_case_sensitive()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            lower_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'lower');

            upper_source:
            LOAD
                id,
                value AS Name
            FROM Inline(id, value;
                2, 'upper');

            result:
            LOAD *
            FROM Union(lower_source, upper_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "name", "Name"],
            [
                new object?[] { 1L, "lower", null },
                new object?[] { 2L, null, "upper" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union работает с CSV auto columns A B")]
    public async Task Execute_script_union_supports_csv_generated_column_names()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            csv_part_1:
            LOAD
                A,
                B
            FROM Csv(path='orders.csv', header=false)
            WHERE A != 'id';

            csv_part_2:
            LOAD
                A,
                B
            FROM Csv(path='orders.csv', header=false)
            WHERE A != 'id';

            result:
            LOAD *
            FROM Union(csv_part_1, csv_part_2)
            ORDER BY A ASC, B ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["A", "B"],
            [
                new object?[] { "1", "Alice" },
                new object?[] { "1", "Alice" },
                new object?[] { "2", "Bob" },
                new object?[] { "2", "Bob" },
                new object?[] { "3", "Charlie" },
                new object?[] { "3", "Charlie" }
            ],
            "ORDER BY `column1` ASC, `column2` ASC");
    }

    [Test]
    [DisplayName("Script Union применяет LIMIT OFFSET после объединения")]
    public async Task Execute_script_union_applies_limit_offset_after_union()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'A';
                2, 'B');

            second_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                3, 'C';
                4, 'D');

            result:
            LOAD *
            FROM Union(first_source, second_source)
            ORDER BY id ASC
            LIMIT 2 OFFSET 1;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "name"],
            [
                new object?[] { 2L, "B" },
                new object?[] { 3L, "C" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union объединяет пустую и непустую таблицу")]
    public async Task Execute_script_union_supports_empty_and_non_empty_table()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            empty_base:
            LOAD *
            FROM Inline(id, name;
                0, 'ignored');

            empty_source:
            LOAD *
            FROM empty_base
            WHERE id < 0;

            non_empty_source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'A');

            result:
            LOAD *
            FROM Union(empty_source, non_empty_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[3],
            ["id", "name"],
            [
                new object?[] { 1L, "A" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union создает пустую final table для двух пустых таблиц")]
    public async Task Execute_script_union_supports_two_empty_tables()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            empty_base:
            LOAD *
            FROM Inline(id, name;
                0, 'ignored');

            first_empty:
            LOAD *
            FROM empty_base
            WHERE id < 0;

            second_empty:
            LOAD *
            FROM empty_base
            WHERE id < 0;

            result:
            LOAD *
            FROM Union(first_empty, second_empty);
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[3],
            ["id", "name"],
            []);
    }

    [Test]
    [DisplayName("Script Union сохраняет логический Time тип")]
    public async Task Execute_script_union_preserves_time_type()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                id,
                Time(time_text) AS time_value
            FROM Inline(id, time_text;
                1, '03:04:05');

            second_source:
            LOAD
                id,
                Time(time_text) AS time_value
            FROM Inline(id, time_text;
                2, '04:05:06');

            result:
            LOAD
                id,
                time_value,
                Type(time_value) AS time_type
            FROM Union(first_source, second_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "time_value", "time_type"],
            [
                new object?[] { 1L, new DateTime(1970, 1, 1, 3, 4, 5), "time" },
                new object?[] { 2L, new DateTime(1970, 1, 1, 4, 5, 6), "time" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union фиксирует что Date function сейчас дает DateTime")]
    public async Task Execute_script_union_keeps_date_function_result_as_datetime()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            dates:
            LOAD
                id,
                date_text.Date('yyyy-MM-dd') AS moment
            FROM Inline(id, date_text;
                1, '2026-01-02');

            datetimes:
            LOAD
                id,
                datetime_text.Date('yyyy-MM-dd HH:mm:ss') AS moment
            FROM Inline(id, datetime_text;
                2, '2026-01-03 04:05:06');

            result:
            LOAD
                id,
                moment,
                Type(moment) AS moment_type
            FROM Union(dates, datetimes)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "moment", "moment_type"],
            [
                new object?[] { 1L, new DateTime(2026, 1, 2), "date" },
                new object?[] { 2L, new DateTime(2026, 1, 3, 4, 5, 6), "date" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union порядок таблиц влияет на порядок новых полей в LOAD star")]
    public async Task Execute_script_union_table_order_changes_new_field_order()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            amount_source:
            LOAD
                id,
                amount
            FROM Inline(id, amount;
                1, 10.0);

            total_source:
            LOAD
                id,
                total
            FROM Inline(id, total;
                2, 20.0);

            result:
            LOAD *
            FROM Union(total_source, amount_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "total", "amount"],
            [
                new object?[] { 1L, null, 10.0m },
                new object?[] { 2L, 20.0m, null }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Union позволяет явно переупорядочить поля после LOAD star schema")]
    public async Task Execute_script_union_supports_explicit_select_reorder()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD
                id,
                name,
                amount
            FROM Inline(id, name, amount;
                1, 'A', 10.0);

            second_source:
            LOAD
                id,
                name,
                total
            FROM Inline(id, name, total;
                2, 'B', 20.0);

            result:
            LOAD
                total,
                id,
                amount
            FROM Union(first_source, second_source)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["total", "id", "amount"],
            [
                new object?[] { null, 1L, 10.0m },
                new object?[] { 20.0m, 2L, null }
            ],
            "ORDER BY `column2` ASC");
    }

    [Test]
    [DisplayName("Script Union после DROP source возвращает script error")]
    public async Task Execute_script_union_after_drop_source_returns_script_error()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
                database,
                """
                first_source:
                LOAD *
                FROM Inline(id;
                    1);

                second_source:
                LOAD *
                FROM Inline(id;
                    2);

                DROP first_source;

                result:
                LOAD *
                FROM Union(first_source, second_source);
                """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.StatementIndex).IsEqualTo(3);
        await Assert.That(exception.Errors[0].Message).Contains("first_source");
    }

    [Test]
    [DisplayName("Script DROP после Union удаляет источники после успешной загрузки")]
    public async Task Execute_script_union_allows_drop_sources_after_success()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            first_source:
            LOAD *
            FROM Inline(id;
                1);

            second_source:
            LOAD *
            FROM Inline(id;
                2);

            result:
            LOAD *
            FROM Union(first_source, second_source)
            ORDER BY id ASC;

            DROP first_source;
            DROP second_source;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        await Assert.That(execution.Tables[0].Alias).IsEqualTo("result");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["id"],
            [
                new object?[] { 1L },
                new object?[] { 2L }
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}
