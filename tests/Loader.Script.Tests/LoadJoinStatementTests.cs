using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadJoinStatementTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadJoinStatementTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("Script Join выполняет inner join и префиксует конфликтующие поля")]
    public async Task Execute_script_join_inner_prefixes_conflicting_fields()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD
                id,
                customer_id,
                amount
            FROM Inline(id, customer_id, amount;
                1, 10, 100.0;
                2, 20, 200.0;
                3, 30, 300.0);

            customers:
            LOAD
                id,
                name
            FROM Inline(id, name;
                10, 'Ann';
                20, 'Bob';
                40, 'Kate');

            result:
            LOAD *
            FROM Join(orders, customer_id, customers, id)
            ORDER BY [orders.id] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["orders.id", "customer_id", "amount", "customers.id", "name"],
            [
                new object?[] { 1L, 10L, 100.0, 10L, "Ann" },
                new object?[] { 2L, 20L, 200.0, 20L, "Bob" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script LeftJoin сохраняет строки левой таблицы")]
    public async Task Execute_script_left_join_keeps_unmatched_left_rows()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD *
            FROM Inline(id, customer_id;
                1, 10;
                2, 99);

            customers:
            LOAD *
            FROM Inline(id, name;
                10, 'Ann');

            result:
            LOAD *
            FROM LeftJoin(orders, customer_id, customers, id)
            ORDER BY [orders.id] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["orders.id", "customer_id", "customers.id", "name"],
            [
                new object?[] { 1L, 10L, 10L, "Ann" },
                new object?[] { 2L, 99L, null, null }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script RightJoin сохраняет строки правой таблицы")]
    public async Task Execute_script_right_join_keeps_unmatched_right_rows()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD *
            FROM Inline(id, customer_id;
                1, 10);

            customers:
            LOAD *
            FROM Inline(id, name;
                10, 'Ann';
                20, 'Bob');

            result:
            LOAD *
            FROM RightJoin(orders, customer_id, customers, id)
            ORDER BY [customers.id] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["orders.id", "customer_id", "customers.id", "name"],
            [
                new object?[] { 1L, 10L, 10L, "Ann" },
                new object?[] { null, null, 20L, "Bob" }
            ],
            "ORDER BY `column3` ASC");
    }

    [Test]
    [DisplayName("Script FullJoin сохраняет unmatched строки обеих таблиц")]
    public async Task Execute_script_full_join_keeps_unmatched_rows_from_both_sides()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD *
            FROM Inline(id, customer_id;
                1, 10;
                2, 99);

            customers:
            LOAD *
            FROM Inline(id, name;
                10, 'Ann';
                20, 'Bob');

            result:
            LOAD *
            FROM FullJoin(orders, customer_id, customers, id)
            ORDER BY [orders.id] ASC, [customers.id] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["orders.id", "customer_id", "customers.id", "name"],
            [
                new object?[] { 1L, 10L, 10L, "Ann" },
                new object?[] { 2L, 99L, null, null },
                new object?[] { null, null, 20L, "Bob" }
            ],
            "ORDER BY `column1` ASC, `column3` ASC");
    }

    [Test]
    [DisplayName("Script Join не матчится по null ключам")]
    public async Task Execute_script_join_does_not_match_null_keys()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            left_source:
            LOAD *
            FROM Inline(id, key;
                1, null;
                2, 10);

            right_source:
            LOAD *
            FROM Inline(id, key;
                3, null;
                4, 10);

            result:
            LOAD *
            FROM Join(left_source, key, right_source, key)
            ORDER BY [left_source.id] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["left_source.id", "left_source.key", "right_source.id", "right_source.key"],
            [
                new object?[] { 2L, 10L, 4L, 10L }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Join размножает строки при duplicate keys")]
    public async Task Execute_script_join_multiplies_duplicate_keys()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            left_source:
            LOAD *
            FROM Inline(left_id, key;
                1, 10;
                2, 10);

            right_source:
            LOAD *
            FROM Inline(right_id, key;
                3, 10;
                4, 10);

            result:
            LOAD *
            FROM Join(left_source, key, right_source, key)
            ORDER BY left_id ASC, right_id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["left_id", "left_source.key", "right_id", "right_source.key"],
            [
                new object?[] { 1L, 10L, 3L, 10L },
                new object?[] { 1L, 10L, 4L, 10L },
                new object?[] { 2L, 10L, 3L, 10L },
                new object?[] { 2L, 10L, 4L, 10L }
            ],
            "ORDER BY `column1` ASC, `column3` ASC");
    }

    [Test]
    [DisplayName("Script Join поддерживает WHERE GROUP BY ORDER BY после join")]
    public async Task Execute_script_join_supports_query_transformations_after_join()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD *
            FROM Inline(id, customer_id, amount;
                1, 10, 100.0;
                2, 10, 50.0;
                3, 20, 40.0);

            customers:
            LOAD *
            FROM Inline(id, city;
                10, 'Moscow';
                20, 'Berlin');

            result:
            LOAD
                city,
                SUM(amount) AS total
            FROM Join(orders, customer_id, customers, id)
            WHERE city != 'Berlin'
            GROUP BY city
            ORDER BY city ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["city", "total"],
            [
                new object?[] { "Moscow", 150.0 }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Join FIRST ограничивает joined source до LOAD преобразований")]
    public async Task Execute_script_join_first_limits_source_rows_before_transformations()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD *
            FROM Inline(id, customer_id;
                1, 10;
                2, 20;
                3, 30);

            customers:
            LOAD *
            FROM Inline(id, name;
                10, 'Zulu';
                20, 'Yankee';
                30, 'Alpha');

            result:
            FIRST 2
            LOAD
                [orders.id] AS order_id,
                name
            FROM Join(orders, customer_id, customers, id)
            ORDER BY name ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["order_id", "name"],
            [
                new object?[] { 2L, "Yankee" },
                new object?[] { 1L, "Zulu" }
            ],
            "ORDER BY `column2` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Join сопоставляет поля сторон по alias при разном physical order")]
    public async Task Execute_script_join_preserves_alias_mapping_when_physical_order_differs()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            left_source:
            LOAD
                raw_key AS left_key,
                raw_left AS left_value
            FROM Inline(raw_left, raw_key;
                10, 1;
                20, 2);

            right_source:
            LOAD
                raw_right AS right_value,
                raw_key AS right_key
            FROM Inline(raw_key, raw_right;
                1, 100;
                2, 200);

            result:
            LOAD
                left_key,
                left_value,
                right_value,
                left_value + right_value AS total
            FROM Join(left_source, left_key, right_source, right_key)
            ORDER BY left_key ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["left_key", "left_value", "right_value", "total"],
            [
                new object?[] { 1L, 10L, 100L, 110L },
                new object?[] { 2L, 20L, 200L, 220L }
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Join квалифицирует physical columns разных сторон")]
    public async Task Execute_script_join_qualifies_physical_columns_from_both_sides()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            left_source:
            LOAD
                raw_key AS left_key,
                raw_name AS left_name
            FROM Inline(raw_key, raw_name;
                1, 'left-1';
                2, 'left-2');

            right_source:
            LOAD
                raw_key AS right_key,
                raw_name AS right_name
            FROM Inline(raw_key, raw_name;
                1, 'right-1';
                3, 'right-3');

            result:
            LOAD
                left_key,
                left_name,
                right_name
            FROM Join(left_source, left_key, right_source, right_key);
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["left_key", "left_name", "right_name"],
            [
                new object?[] { 1L, "left-1", "right-1" }
            ]);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Join читает TEMP LOAD источники и чистит их в конце")]
    public async Task Execute_script_join_reads_temp_sources_and_cleans_them_at_the_end()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            TEMP LOAD *
            FROM Inline(id, customer_id;
                1, 10;
                2, 20);

            customers:
            TEMP LOAD *
            FROM Inline(id, name;
                10, 'Ann';
                30, 'Kate');

            result:
            LOAD
                [orders.id] AS order_id,
                name
            FROM Join(orders, customer_id, customers, id);
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        await Assert.That(execution.Tables[0].Alias).IsEqualTo("result");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["order_id", "name"],
            [
                new object?[] { 1L, "Ann" }
            ]);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
        await ScriptIntegrationAssert.AssertTableCountWithPrefixAsync(database, execution.FinalTablePrefix, 1);
    }

    [Test]
    [DisplayName("Script Join работает с blocked table и field names")]
    public async Task Execute_script_join_supports_blocked_table_and_field_names()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            [orders table]:
            LOAD
                id AS [order id],
                customer AS [customer id]
            FROM Inline(id, customer;
                1, 10);

            [customer table]:
            LOAD
                id AS [customer id],
                name AS [customer name]
            FROM Inline(id, name;
                10, 'Ann');

            result:
            LOAD *
            FROM Join([orders table], [customer id], [customer table], [customer id]);
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["order id", "orders table.customer id", "customer table.customer id", "customer name"],
            [
                new object?[] { 1L, 10L, 10L, "Ann" }
            ]);
    }

    [Test]
    [DisplayName("Script Join отклоняет разные типы key полей")]
    public async Task Execute_script_join_rejects_different_key_types()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
                database,
                """
                left_source:
                LOAD *
                FROM Inline(id, key;
                    1, 10);

                right_source:
                LOAD *
                FROM Inline(id, key;
                    2, '10');

                result:
                LOAD *
                FROM Join(left_source, key, right_source, key);
                """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.Errors[0].Message).Contains("одинаковый тип");
    }

    [Test]
    [DisplayName("Script Join отклоняет конфликт после prefix")]
    public async Task Execute_script_join_rejects_conflict_after_prefix()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
                database,
                """
                orders:
                LOAD
                    id,
                    name,
                    shadow AS [customers.name]
                FROM Inline(id, name, shadow;
                    1, 'left', 'shadow');

                customers:
                LOAD
                    id,
                    name
                FROM Inline(id, name;
                    1, 'right');

                result:
                LOAD *
                FROM Join(orders, id, customers, id);
                """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.Errors[0].Message).Contains("customers.name");
    }

    [Test]
    [DisplayName("Script Join соединяет одну физическую таблицу через два логических alias")]
    public async Task Execute_script_join_self_table_through_second_alias()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            people:
            LOAD *
            FROM Inline(id, manager_id, name;
                1, null, 'CEO';
                2, 1, 'Dev';
                3, 1, 'QA');

            managers:
            LOAD *
            FROM people;

            result:
            LOAD
                [people.name] AS employee,
                [managers.name] AS manager
            FROM LeftJoin(people, manager_id, managers, id)
            WHERE [people.id] > 1
            ORDER BY [people.name] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["employee", "manager"],
            [
                new object?[] { "Dev", "CEO" },
                new object?[] { "QA", "CEO" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Join работает после Union и Numbers providers")]
    public async Task Execute_script_join_supports_union_and_numbers_sources()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            left_a:
            LOAD *
            FROM Inline(id, label;
                1, 'a');

            left_b:
            LOAD *
            FROM Inline(id, label;
                2, 'b');

            all_left:
            LOAD *
            FROM Union(left_a, left_b);

            scores:
            LOAD
                number AS id,
                number * 10 AS score
            FROM Numbers(1, 2);

            result:
            LOAD *
            FROM Join(all_left, id, scores, id)
            ORDER BY label ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[4],
            ["all_left.id", "label", "scores.id", "score"],
            [
                new object?[] { 1L, "a", 1L, 10L },
                new object?[] { 2L, "b", 2L, 20L }
            ],
            "ORDER BY `column2` ASC");
    }

    [Test]
    [DisplayName("Script Join работает с Calendar provider по date полю")]
    public async Task Execute_script_join_supports_calendar_source()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            calendar:
            LOAD
                Date,
                DayOfMonth
            FROM Calendar(min='2024-01-01', max='2024-01-03');

            events:
            LOAD
                Date('2024-01-02') AS Date,
                'middle' AS name
            FROM Inline(dummy; 1);

            result:
            LOAD
                [calendar.Date] AS calendar_date,
                DayOfMonth,
                name
            FROM Join(calendar, Date, events, Date);
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["calendar_date", "DayOfMonth", "name"],
            [
                new object?[] { new DateTime(2024, 1, 2), (byte)2, "middle" }
            ]);
    }

    [Test]
    [DisplayName("Script Join поддерживает WHERE GROUP BY ORDER BY по prefixed полям")]
    public async Task Execute_script_join_supports_query_transformations_on_prefixed_fields()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD *
            FROM Inline(id, customer_id;
                1, 10;
                2, 10;
                3, 20);

            customers:
            LOAD *
            FROM Inline(id, city;
                10, 'Moscow';
                20, 'Berlin');

            result:
            LOAD
                [customers.id] AS customer_id,
                COUNT() AS order_count
            FROM Join(orders, customer_id, customers, id)
            WHERE [orders.id] > 1
            GROUP BY [customers.id]
            ORDER BY [customers.id] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["customer_id", "order_count"],
            [
                new object?[] { 10L, 1UL },
                new object?[] { 20L, 1UL }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script Join поддерживает unicode пробелы и точки в именах")]
    public async Task Execute_script_join_supports_unicode_spaces_and_dots_in_names()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            [заказы.2026]:
            LOAD
                id AS [ключ.id],
                'order' AS [тип записи]
            FROM Inline(id; 1);

            [клиенты 2026]:
            LOAD
                id AS [ключ.id],
                'Анна' AS [имя.клиента]
            FROM Inline(id; 1);

            result:
            LOAD *
            FROM Join([заказы.2026], [ключ.id], [клиенты 2026], [ключ.id]);
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["заказы.2026.ключ.id", "тип записи", "клиенты 2026.ключ.id", "имя.клиента"],
            [
                new object?[] { 1L, "order", 1L, "Анна" }
            ]);
    }

    [Test]
    [DisplayName("Script Join считает имена полей case-sensitive")]
    public async Task Execute_script_join_keeps_case_sensitive_field_names_separate()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            left_source:
            LOAD *
            FROM Inline(id, name;
                1, 'lower');

            right_source:
            LOAD *
            FROM Inline(ref, Name;
                1, 'upper');

            result:
            LOAD *
            FROM Join(left_source, id, right_source, ref);
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "name", "ref", "Name"],
            [
                new object?[] { 1L, "lower", 1L, "upper" }
            ]);
    }

    [Test]
    [DisplayName("Script FullJoin сохраняет правые строки когда левая таблица пустая")]
    public async Task Execute_script_full_join_keeps_right_rows_when_left_is_empty()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            empty_orders:
            LOAD
                number AS id
            FROM Numbers(1)
            WHERE number > 10;

            customers:
            LOAD *
            FROM Inline(id, name;
                1, 'Ann');

            result:
            LOAD *
            FROM FullJoin(empty_orders, id, customers, id);
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["empty_orders.id", "customers.id", "name"],
            [
                new object?[] { null, 1L, "Ann" }
            ]);
    }

    [Test]
    [DisplayName("Script Join допускает nullable key и non-null key одного типа")]
    public async Task Execute_script_join_allows_nullable_and_non_nullable_key_same_type()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            left_source:
            LOAD *
            FROM Inline(id, key;
                1, null;
                2, 10);

            right_source:
            LOAD *
            FROM Inline(key, value;
                10, 'ok');

            result:
            LOAD *
            FROM LeftJoin(left_source, key, right_source, key)
            ORDER BY id ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["id", "left_source.key", "right_source.key", "value"],
            [
                new object?[] { 1L, null, null, null },
                new object?[] { 2L, 10L, 10L, "ok" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script FullJoin возвращает все строки когда совпадений нет")]
    public async Task Execute_script_full_join_returns_all_rows_when_no_matches()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD *
            FROM Inline(id, customer_id;
                1, 10);

            customers:
            LOAD *
            FROM Inline(id, name;
                20, 'Bob');

            result:
            LOAD *
            FROM FullJoin(orders, customer_id, customers, id)
            ORDER BY [orders.id] ASC, [customers.id] ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[2],
            ["orders.id", "customer_id", "customers.id", "name"],
            [
                new object?[] { 1L, 10L, null, null },
                new object?[] { null, null, 20L, "Bob" }
            ],
            "ORDER BY `column1` ASC, `column3` ASC");
    }
}
