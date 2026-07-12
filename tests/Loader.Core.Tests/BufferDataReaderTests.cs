using System.Data;
using System.Data.Common;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Csv;
using Loader.Core.Providers.Excel;
using Loader.Core.Providers.Json;
using Loader.Core.Providers.Oracle;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Qvd;
using Loader.Core.Providers.Sql;
using Loader.Core.Providers.SqlServer;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class BufferDataReaderTests
{
    private const string OracleConnectionStringEnvironmentVariable = "ORACLE_TEST_CONNECTION_STRING";

    [Test]
    [DisplayName("CsvProvider works without buffer")]
    public async Task Csv_provider_works_without_buffer()
    {
        var provider = new CsvProvider();
        var source = new InlineCsv("id,name\r\n1,Moscow");
        var config = new CsvTableConfig
        {
            FileName = "inline.csv",
            HasHeader = true
        };

        await AssertProviderWorksWithAndWithoutBuffer(
            () => provider.OpenReaderAsync(source, config),
            "1",
            "Moscow");
    }

    [Test]
    [DisplayName("JsonProvider works without buffer")]
    public async Task Json_provider_works_without_buffer()
    {
        var provider = new JsonProvider();
        var source = new InlineJson("""[{ "id": 1, "name": "Moscow" }]""");
        var config = new JsonTableConfig
        {
            FileName = "inline.json",
            ArrayPath = [],
            Schema = new JsonTableSchema
            {
                Columns =
                [
                    new JsonColumnSchema { Name = "id", Path = "id" },
                    new JsonColumnSchema { Name = "name", Path = "name" }
                ]
            }
        };

        await AssertProviderWorksWithAndWithoutBuffer(
            () => provider.OpenReaderAsync(source, config),
            "1",
            "Moscow");
    }

    [Test]
    [DisplayName("ExcelProvider works without buffer")]
    public async Task Excel_provider_works_without_buffer()
    {
        var provider = new ExcelProvider();
        var source = new FileSystemSource(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Excel"));
        var config = new ExcelTableConfig
        {
            FileName = "excel-provider-complex.xlsx",
            WorksheetName = "hidden_sheet",
            HasHeader = true
        };

        await AssertProviderWorksWithAndWithoutBuffer(
            () => provider.OpenReaderAsync(source, config),
            "1",
            "Hidden Sheet");
    }

    [Test]
    [DisplayName("QvdProvider works without buffer")]
    public async Task Qvd_provider_works_without_buffer()
    {
        var provider = new QvdProvider();
        var source = new FileSystemSource(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Qvd"));
        var config = new QvdTableConfig
        {
            FileName = "basic_text.qvd"
        };

        await AssertProviderWorksWithAndWithoutBuffer(
            () => provider.OpenReaderAsync(source, config),
            "1",
            "Alice");
    }

    [Test]
    [DisplayName("PostgresProvider works without buffer")]
    public async Task Postgres_provider_works_without_buffer()
    {
        await using var database = await PostgresTestDatabase.StartAsync();
        var provider = new PostgresProvider();
        var source = new ConnectionStringSource { ConnectionString = database.ConnectionString };
        var config = new SqlTableConfig { Sql = "select 1::integer as id, 'Moscow'::text as name" };

        await AssertProviderWorksWithAndWithoutBuffer(
            () => provider.OpenReaderAsync(source, config),
            1,
            "Moscow");
    }

    [Test]
    [DisplayName("ClickHouseProvider works without buffer")]
    public async Task ClickHouse_provider_works_without_buffer()
    {
        await using var database = await ClickHouseTestDatabase.StartAsync();
        var provider = new ClickHouseProvider();
        var source = new ConnectionStringSource { ConnectionString = database.ConnectionString };
        var config = new SqlTableConfig { Sql = "select toInt32(1) as id, 'Moscow' as name" };

        await AssertProviderWorksWithAndWithoutBuffer(
            () => provider.OpenReaderAsync(source, config),
            1,
            "Moscow");
    }

    [Test]
    [DisplayName("SqlServerProvider works only with buffer")]
    public async Task SqlServer_provider_works_only_with_buffer()
    {
        await using var database = await SqlServerTestDatabase.StartAsync();
        var provider = new SqlServerProvider();
        var source = new ConnectionStringSource { ConnectionString = database.ConnectionString };
        var config = new SqlTableConfig { Sql = "select cast(1 as int) as id, cast('Moscow' as nvarchar(20)) as name" };

        await AssertProviderWorksWithAndWithoutBuffer(
            () => provider.OpenReaderAsync(source, config),
            1,
            "Moscow");
    }

    [Test]
    [DisplayName("OracleProvider works only with buffer")]
    public async Task Oracle_provider_works_only_with_buffer()
    {
        var connectionString = Environment.GetEnvironmentVariable(OracleConnectionStringEnvironmentVariable);
        Skip.When(
            string.IsNullOrWhiteSpace(connectionString),
            $"Set {OracleConnectionStringEnvironmentVariable} to run Oracle integration tests.");

        var provider = new OracleProvider();
        var source = new ConnectionStringSource { ConnectionString = connectionString! };
        var config = new SqlTableConfig { Sql = "select 1 as \"id\", 'Moscow' as \"name\" from dual" };

        await AssertProviderNeedsBuffer(
            () => provider.OpenReaderAsync(source, config),
            1m,
            "Moscow");
    }

    [Test]
    [DisplayName("Normalize without buffer works for sequential reader when columns are read in order")]
    public async Task Normalize_without_buffer_works_for_ordered_sequential_access()
    {
        using var table = CreateTable();
        using var rawReader = new SequentialOnlyReader(table.CreateDataReader());
        using var reader = rawReader.Normalize(new NormalizeOptions { Buffer = false });

        await Assert.That(reader.Read()).IsTrue();

        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
        await Assert.That(rawReader.GetValueCalls).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Normalize without buffer fails for sequential reader when columns are read out of order")]
    public async Task Normalize_without_buffer_fails_for_out_of_order_sequential_access()
    {
        using var table = CreateTable();
        using var rawReader = new SequentialOnlyReader(table.CreateDataReader());
        using var reader = rawReader.Normalize(new NormalizeOptions { Buffer = false });

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
        await Assert.That(() => reader.GetValue(0))
            .ThrowsExactly<DataReaderValueException>()
            .WithMessage("Failed to read field 'id' at ordinal 0.");
    }

    [Test]
    [DisplayName("Normalize with buffer works for sequential reader when columns are read out of order")]
    public async Task Normalize_with_buffer_works_for_out_of_order_sequential_access()
    {
        using var table = CreateTable();
        using var rawReader = new SequentialOnlyReader(table.CreateDataReader());
        using var reader = rawReader.Normalize(new NormalizeOptions { Buffer = true });

        await Assert.That(reader.Read()).IsTrue();

        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(rawReader.GetValueCalls).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Normalize with buffer reads inner values once during Read")]
    public async Task Normalize_with_buffer_reads_inner_values_once_during_read()
    {
        using var table = CreateTable();
        using var rawReader = new SequentialOnlyReader(table.CreateDataReader());
        using var reader = rawReader.Normalize(new NormalizeOptions { Buffer = true });

        await Assert.That(rawReader.GetValueCalls).IsEqualTo(0);
        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(rawReader.GetValueCalls).IsEqualTo(2);

        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(rawReader.GetValueCalls).IsEqualTo(2);
    }

    private static DataTable CreateTable()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1, "Moscow");
        return table;
    }

    private static async Task AssertProviderWorksWithAndWithoutBuffer(
        Func<ValueTask<DbDataReader>> openReader,
        object expectedFirstValue,
        object expectedSecondValue)
    {
        await using (var rawReader = await openReader())
        await using (var reader = rawReader.Normalize(new NormalizeOptions { Buffer = false }))
        {
            await Assert.That(reader.Read()).IsTrue();
            await Assert.That(reader.GetValue(1)).IsEqualTo(expectedSecondValue);
            await Assert.That(reader.GetValue(0)).IsEqualTo(expectedFirstValue);
        }

        await using (var rawReader = await openReader())
        await using (var reader = rawReader.Normalize(new NormalizeOptions { Buffer = true }))
        {
            await Assert.That(reader.Read()).IsTrue();
            await Assert.That(reader.GetValue(1)).IsEqualTo(expectedSecondValue);
            await Assert.That(reader.GetValue(0)).IsEqualTo(expectedFirstValue);
        }
    }

    private static async Task AssertProviderNeedsBuffer(
        Func<ValueTask<DbDataReader>> openReader,
        object expectedFirstValue,
        object expectedSecondValue)
    {
        await using (var rawReader = await openReader())
        await using (var reader = rawReader.Normalize(new NormalizeOptions { Buffer = false }))
        {
            await Assert.That(reader.Read()).IsTrue();
            await Assert.That(reader.GetValue(1)).IsEqualTo(expectedSecondValue);
            await Assert.That(() => reader.GetValue(0))
                .ThrowsExactly<DataReaderValueException>();
        }

        await using (var rawReader = await openReader())
        await using (var reader = rawReader.Normalize(new NormalizeOptions { Buffer = true }))
        {
            await Assert.That(reader.Read()).IsTrue();
            await Assert.That(reader.GetValue(1)).IsEqualTo(expectedSecondValue);
            await Assert.That(reader.GetValue(0)).IsEqualTo(expectedFirstValue);
        }
    }

    private sealed class SequentialOnlyReader : DbDataReaderDecorator
    {
        private int _lastReadOrdinal = -1;

        public SequentialOnlyReader(DbDataReader inner)
            : base(inner)
        {
        }

        public int GetValueCalls { get; private set; }

        public override bool Read()
        {
            _lastReadOrdinal = -1;
            return Inner.Read();
        }

        public override object GetValue(int ordinal)
        {
            if (ordinal < _lastReadOrdinal)
            {
                throw new InvalidOperationException(
                    $"Sequential reader cannot read ordinal {ordinal} after ordinal {_lastReadOrdinal}.");
            }

            _lastReadOrdinal = ordinal;
            GetValueCalls++;
            return Inner.GetValue(ordinal);
        }
    }
}
