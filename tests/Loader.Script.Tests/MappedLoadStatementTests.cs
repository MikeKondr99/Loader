using System.Data;
using System.Data.Common;
using Loader.Core.Sources;
using Loader.Lang.Statements;
using Loader.Script.Execution;
using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class MappedLoadStatementTests
{
    private readonly ClickHouseTestDatabase database;

    public MappedLoadStatementTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("Script MAPPED LOAD применяет строковый mapping и не возвращает mapping-таблицу в result")]
    public async Task Execute_script_apply_map_maps_text_key_to_text_value()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key, value;
                'Новые клиенты', 'New Customers';
                'Затраты на рекламу', 'Ad Spend');

            raw:
            TEMP LOAD *
            FROM Inline(metric;
                'Новые клиенты';
                'Затраты на рекламу');

            result:
            LOAD
                metric.ApplyMap('labels') AS metric
            FROM raw
            ORDER BY metric ASC;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        await Assert.That(execution.Tables[0].Alias).IsEqualTo("result");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["metric"],
            [
                new object?[] { "Ad Spend" },
                new object?[] { "New Customers" }
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
        await ScriptIntegrationAssert.AssertTableCountWithPrefixAsync(database, execution.FinalTablePrefix, 1);
    }

    [Test]
    [DisplayName("Script MAPPED LOAD поддерживает целое число как ключ")]
    public async Task Execute_script_apply_map_supports_integer_key()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            statuses:
            MAPPED LOAD *
            FROM Inline(code, name;
                1, 'New';
                2, 'Paid');

            raw:
            TEMP LOAD *
            FROM Inline(code;
                1;
                3);

            result:
            LOAD
                code,
                code.ApplyMap('statuses').Alt('Unknown') AS status
            FROM raw
            ORDER BY code ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["code", "status"],
            [
                new object?[] { 1L, "New" },
                new object?[] { 3L, "Unknown" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script ApplyMap возвращает null если ключ не найден")]
    public async Task Execute_script_apply_map_returns_null_for_missing_key()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key, value;
                'known', 'Known');

            result:
            LOAD
                key.ApplyMap('labels') AS value
            FROM Inline(key;
                'missing');
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["value"],
            [
                new object?[] { null }
            ]);
    }

    [Test]
    [DisplayName("Script ApplyMap можно использовать в WHERE если mapping возвращает boolean")]
    public async Task Execute_script_apply_map_can_filter_by_boolean_value()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            active_flags:
            MAPPED LOAD *
            FROM Inline(code, active;
                'A', true;
                'B', false);

            raw:
            TEMP LOAD *
            FROM Inline(code;
                'A';
                'B';
                'C');

            result:
            LOAD
                code
            FROM raw
            WHERE code.ApplyMap('active_flags')
            ORDER BY code ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["code"],
            [
                new object?[] { "A" }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script MAPPED LOAD с явными двумя полями создает mapping-таблицу")]
    public async Task Execute_script_mapped_load_explicit_two_fields_creates_mapping_table()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD
                source_key AS key,
                source_value AS value
            FROM Inline(source_key, source_value;
                'a', 'A');

            result:
            LOAD
                key.ApplyMap('labels') AS value
            FROM Inline(key;
                'a');
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["value"],
            [
                new object?[] { "A" }
            ]);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script ApplyMap поддерживает разные типы значения")]
    public async Task Execute_script_apply_map_supports_value_types()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            number_map:
            MAPPED LOAD *
            FROM Inline(key, value;
                'a', 10.5);

            date_time_map:
            MAPPED LOAD
                key,
                Date(value) AS value
            FROM Inline(key, value;
                'a', '2024-01-02 03:04:05');

            time_map:
            MAPPED LOAD
                key,
                Time(value) AS value
            FROM Inline(key, value;
                'a', '03:04:05');

            result:
            LOAD
                key.ApplyMap('number_map') AS number_value,
                key.ApplyMap('date_time_map') AS date_time_value,
                key.ApplyMap('time_map') AS time_value
            FROM Inline(key;
                'a');
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["number_value", "date_time_value", "time_value"],
            [
                new object?[]
                {
                    10.5,
                    new DateTime(2024, 1, 2, 3, 4, 5),
                    new DateTime(1970, 1, 1, 3, 4, 5)
                }
            ]);
    }

    [Test]
    [DisplayName("Script ApplyMap можно использовать в GROUP BY и ORDER BY")]
    public async Task Execute_script_apply_map_can_group_and_order_by_mapped_value()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            categories:
            MAPPED LOAD *
            FROM Inline(code, category;
                'A', 'Retail';
                'B', 'Retail';
                'C', 'Wholesale');

            raw:
            TEMP LOAD *
            FROM Inline(code;
                'A';
                'B';
                'C';
                'C');

            result:
            LOAD
                code.ApplyMap('categories') AS category,
                COUNT(code) AS cnt
            FROM raw
            GROUP BY code.ApplyMap('categories')
            ORDER BY code.ApplyMap('categories') ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["category", "cnt"],
            [
                new object?[] { "Retail", 2UL },
                new object?[] { "Wholesale", 2UL }
            ],
            "ORDER BY `column1` ASC");
    }

    [Test]
    [DisplayName("Script MAPPED LOAD можно создать из CSV")]
    public async Task Execute_script_mapped_load_can_use_file_source()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            names:
            MAPPED LOAD
                id AS key,
                name AS value
            FROM Csv('orders.csv');

            raw:
            TEMP LOAD *
            FROM Inline(id;
                '1';
                '3');

            result:
            LOAD
                id.ApplyMap('names') AS name
            FROM raw;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["name"],
            [
                new object?[] { "Alice" },
                new object?[] { "Charlie" }
            ],
            null);
    }

    [Test]
    [TestWithDependency(DatabaseDependency.ClickHouse, UseDataSource = false)]
    [DisplayName("Script MAPPED LOAD можно создать из database source")]
    public async Task Execute_script_mapped_load_can_use_database_source()
    {
        var sourceTable = $"script_mapped_source_{Guid.NewGuid():N}";
        try
        {
            await ScriptIntegrationAssert.ExecuteClickHouseAsync(
                database,
                $$"""
                CREATE TABLE `{{sourceTable}}`
                (
                    `key` String,
                    `value` String
                )
                ENGINE = Memory
                """);
            await ScriptIntegrationAssert.ExecuteClickHouseAsync(
                database,
                $$"""
                INSERT INTO `{{sourceTable}}` (`key`, `value`) VALUES
                ('a', 'A'),
                ('b', 'B')
                """);

            var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
                database,
                $$"""
                labels:
                MAPPED LOAD *
                FROM Connect('container_ch')
                SQL SELECT key, value FROM `{{sourceTable}}`;

                result:
                LOAD
                    key.ApplyMap('labels') AS value
                FROM Inline(key;
                    'b');
                """);

            await ScriptIntegrationAssert.AssertFinalTableAsync(
                database,
                execution.Tables[0],
                ["value"],
                [
                    new object?[] { "B" }
                ]);
        }
        finally
        {
            await ScriptIntegrationAssert.ExecuteClickHouseAsync(database, $"DROP TABLE IF EXISTS `{sourceTable}`");
        }
    }

    [Test]
    [DisplayName("Script MAPPED LOAD схлопывает дубли ключей")]
    public async Task Execute_script_mapped_load_collapses_duplicate_keys()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key, value;
                'a', 'A1';
                'a', 'A2');

            result:
            LOAD
                key.ApplyMap('labels') AS value
            FROM Inline(key;
                'a');
            """);

        var rows = await ScriptIntegrationAssert.ReadFinalTableAsync(database, execution.Tables[0]);

        await Assert.That(rows.Rows).Count().IsEqualTo(1);
        await Assert.That(new[] { "A1", "A2" }.Contains((string)rows.Rows[0][0]!)).IsTrue();
    }

    [Test]
    [DisplayName("Script MAPPED LOAD с явными полями требует ровно два поля")]
    public async Task Execute_script_mapped_load_explicit_fields_rejects_not_two_fields()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD
                key,
                value,
                value AS extra
            FROM Inline(key, value;
                'a', 'A');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("MAPPED LOAD");
    }

    [Test]
    [DisplayName("Script MAPPED LOAD с одним явным полем выдает ошибку")]
    public async Task Execute_script_mapped_load_explicit_fields_rejects_one_field()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD
                key
            FROM Inline(key, value;
                'a', 'A');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("MAPPED LOAD");
    }

    [Test]
    [DisplayName("Script MAPPED LOAD * работает только с двумя полями")]
    public async Task Execute_script_mapped_load_star_rejects_source_with_not_two_fields()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key, value, extra;
                'a', 'A', 'extra');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("ожидалось 2");
    }

    [Test]
    [DisplayName("Script MAPPED LOAD * проверяет число полей для Table source")]
    public async Task Execute_script_mapped_load_star_rejects_table_source_with_not_two_fields()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            raw:
            LOAD *
            FROM Inline(key, value, extra;
                'a', 'A', 'extra');

            labels:
            MAPPED LOAD *
            FROM raw;
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("ожидалось 2");
    }

    [Test]
    [DisplayName("Script MAPPED LOAD * выдает ошибку если source возвращает одно поле")]
    public async Task Execute_script_mapped_load_star_rejects_source_with_one_field()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key;
                'a');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("ожидалось 2");
    }

    [Test]
    [DisplayName("Script MAPPED LOAD * выдает ошибку если source возвращает ноль полей")]
    public async Task Execute_script_mapped_load_star_rejects_source_with_zero_fields()
    {
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            labels:
            MAPPED LOAD *
            FROM Csv('empty.csv');
            """).Value!;

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = new LoadStatementExecutor
            {
                ProviderResolver = new ZeroFieldProviderResolver()
            }
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("Источник вернул ноль полей.");
    }

    [Test]
    [DisplayName("Script ApplyMap выдает ошибку если mapping-таблица не найдена")]
    public async Task Execute_script_apply_map_rejects_missing_mapping_table()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            result:
            LOAD
                key.ApplyMap('missing') AS value
            FROM Inline(key;
                'a');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("missing");
        await Assert.That(exception.Errors[0].Message).Contains("не найдена");
    }

    [Test]
    [DisplayName("Script ApplyMap выдает ошибку если таблица существует, но она не MAPPED LOAD")]
    public async Task Execute_script_apply_map_rejects_normal_table()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            LOAD *
            FROM Inline(key, value;
                'a', 'A');

            result:
            LOAD
                key.ApplyMap('labels') AS value
            FROM Inline(key;
                'a');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("labels");
        await Assert.That(exception.Errors[0].Message).Contains("не является MAPPED LOAD");
    }

    [Test]
    [DisplayName("Script ApplyMap после DROP выдает ошибку как на отсутствующую mapping-таблицу")]
    public async Task Execute_script_apply_map_rejects_dropped_mapping_table()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key, value;
                'a', 'A');

            DROP labels;

            result:
            LOAD
                key.ApplyMap('labels') AS value
            FROM Inline(key;
                'a');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("labels");
        await Assert.That(exception.Errors[0].Message).Contains("не найдена");
    }

    [Test]
    [DisplayName("Script ApplyMap выдает ошибку если тип ключа не совпадает с mapping key")]
    public async Task Execute_script_apply_map_rejects_incompatible_key_type()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key, value;
                1, 'A');

            result:
            LOAD
                key.ApplyMap('labels') AS value
            FROM Inline(key;
                '1');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("ApplyMap key type");
        await Assert.That(exception.Errors[0].Message).Contains("labels");
    }

    [Test]
    [DisplayName("Script ApplyMap выдает ошибку если mapping объявлен позже")]
    public async Task Execute_script_apply_map_rejects_mapping_table_declared_later()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            result:
            LOAD
                key.ApplyMap('labels') AS value
            FROM Inline(key;
                'a');

            labels:
            MAPPED LOAD *
            FROM Inline(key, value;
                'a', 'A');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("labels");
        await Assert.That(exception.Errors[0].Message).Contains("не найдена");
    }

    [Test]
    [DisplayName("Script ApplyMap с нестроковым именем mapping отклоняется системой функций")]
    public async Task Execute_script_apply_map_rejects_non_text_mapping_name()
    {
        var exception = await Assert.That(async () => await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            labels:
            MAPPED LOAD *
            FROM Inline(key, value;
                'a', 'A');

            result:
            LOAD
                key.ApplyMap(1) AS value
            FROM Inline(key;
                'a');
            """))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("ApplyMap");
        await Assert.That(exception.Errors[0].Message).Contains("не найдена");
    }

    private static ScriptContext CreateContext()
    {
        return new ScriptContext
        {
            FileStorage = new FileSystemSource(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Script")),
            TargetConnectionString = "Host=localhost"
        };
    }

    private sealed class ZeroFieldProviderResolver : ILoadProviderResolver
    {
        public ValueTask<LoadFromSource> ResolveAsync(
            LoadStatement statement,
            ScriptContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<LoadFromSource>(new ReaderLoadFromSource
            {
                RequiresBuffer = false,
                OpenReaderAsync = _ => ValueTask.FromResult<DbDataReader>(CreateReader())
            });
        }

        private static DbDataReader CreateReader()
        {
            return new DataTable().CreateDataReader();
        }
    }
}
