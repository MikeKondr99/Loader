using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using BenchmarkDotNet.Attributes;
using ClickHouse.Client.ADO;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Csv;
using Loader.Core.Providers.Excel;
using Loader.Core.Providers.Json;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Sql;
using Loader.Core.Providers.SqlServer;
using Loader.Core.Providers.Xml;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Sylvan.Data.Excel;

[MemoryDiagnoser]
[ShortRunJob]
public class WideClickHouseLoadBenchmarks
{
    private const string TargetConnectionEnv = "LOADER_BENCH_CLICKHOUSE_TARGET";
    private const string ClickHouseSourceConnectionEnv = "LOADER_BENCH_CLICKHOUSE_SOURCE";
    private const string PostgresConnectionEnv = "LOADER_BENCH_POSTGRES";
    private const string SqlServerConnectionEnv = "LOADER_BENCH_SQLSERVER";
    private const string PrintPeakMemoryEnv = "LOADER_BENCH_PRINT_PEAK_MEMORY";

    private static readonly CsvProvider CsvProvider = new();
    private static readonly ExcelProvider ExcelProvider = new();
    private static readonly JsonProvider JsonProvider = new();
    private static readonly XmlProvider XmlProvider = new();
    private static readonly PostgresProvider PostgresProvider = new();
    private static readonly SqlServerProvider SqlServerProvider = new();
    private static readonly ClickHouseProvider ClickHouseProvider = new();
    private static readonly ClickHouseWriter Writer = new();

    private WideBenchmarkFixtures _fixtures = null!;
    private ConnectionStringSource _targetSource = null!;
    private DataMetaContainer _meta = null!;
    private AutoCastSchema _autoCastSchema = null!;
    private bool _printPeakMemory;

    [Params(1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _fixtures = await WideBenchmarkFixtures.EnsureAsync(RowCount).ConfigureAwait(false);
        _targetSource = new ConnectionStringSource
        {
            ConnectionString = RequireEnvironment(TargetConnectionEnv)
        };
        _autoCastSchema = WideBenchmarkDataSet.CreateAutoCastSchema();
        _meta = BuildMeta(RowCount);
        _printPeakMemory = Environment.GetEnvironmentVariable(PrintPeakMemoryEnv) == "1";
    }

    [IterationSetup]
    public void DropTargets()
    {
        using var connection = new ClickHouseConnection(_targetSource.ConnectionString);
        connection.Open();

        foreach (var tableName in TargetTables.All)
        {
            using var command = connection.CreateCommand();
            command.CommandText = BuildDropTableSql(tableName);
            command.ExecuteNonQuery();
        }
    }

    [Benchmark]
    public async Task Csv_to_clickhouse()
    {
        await MeasureAsync(
            nameof(Csv_to_clickhouse),
            async () =>
            {
                await using var raw = await CsvProvider.OpenReaderAsync(
                    _fixtures.FileSource,
                    new CsvTableConfig
                    {
                        FileName = _fixtures.CsvFileName
                    }).ConfigureAwait(false);
                await using var reader = raw.Normalize().AutoCast(_autoCastSchema);

                await WriteAsync(reader, TargetTables.Csv).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Excel_to_clickhouse()
    {
        await MeasureAsync(
            nameof(Excel_to_clickhouse),
            async () =>
            {
                await using var raw = await ExcelProvider.OpenReaderAsync(
                    _fixtures.FileSource,
                    new ExcelTableConfig
                    {
                        FileName = _fixtures.ExcelFileName
                    }).ConfigureAwait(false);
                await using var reader = raw.Normalize().AutoCast(_autoCastSchema);

                await WriteAsync(reader, TargetTables.Excel).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Json_to_clickhouse_with_schema_analyze()
    {
        await MeasureAsync(
            nameof(Json_to_clickhouse_with_schema_analyze),
            async () =>
            {
                var schema = await JsonProvider.AnalyzeSchemaAsync(
                    _fixtures.FileSource,
                    _fixtures.JsonFileName,
                    Array.Empty<string>()).ConfigureAwait(false);
                await using var raw = await JsonProvider.OpenReaderAsync(
                    _fixtures.FileSource,
                    new JsonTableConfig
                    {
                        FileName = _fixtures.JsonFileName,
                        ArrayPath = Array.Empty<string>(),
                        Schema = schema
                    }).ConfigureAwait(false);
                await using var reader = raw.Normalize().AutoCast(_autoCastSchema);

                await WriteAsync(reader, TargetTables.Json).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Xml_to_clickhouse_with_schema_analyze()
    {
        await MeasureAsync(
            nameof(Xml_to_clickhouse_with_schema_analyze),
            async () =>
            {
                var schema = await XmlProvider.AnalyzeSchemaAsync(
                    _fixtures.FileSource,
                    _fixtures.XmlFileName,
                    "row").ConfigureAwait(false);
                await using var raw = await XmlProvider.OpenReaderAsync(
                    _fixtures.FileSource,
                    new XmlTableConfig
                    {
                        FileName = _fixtures.XmlFileName,
                        TableName = "row",
                        Schema = schema
                    }).ConfigureAwait(false);
                await using var reader = raw.Normalize().AutoCast(_autoCastSchema);

                await WriteAsync(reader, TargetTables.Xml).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Postgres_to_clickhouse()
    {
        await MeasureAsync(
            nameof(Postgres_to_clickhouse),
            async () =>
            {
                await using var raw = await PostgresProvider.OpenReaderAsync(
                    new ConnectionStringSource
                    {
                        ConnectionString = RequireEnvironment(PostgresConnectionEnv)
                    },
                    new SqlTableConfig
                    {
                        Sql = WideBenchmarkSql.Postgres(RowCount)
                    }).ConfigureAwait(false);
                await using var reader = raw.Normalize();

                await WriteAsync(reader, TargetTables.Postgres).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task SqlServer_to_clickhouse()
    {
        await MeasureAsync(
            nameof(SqlServer_to_clickhouse),
            async () =>
            {
                await using var raw = await SqlServerProvider.OpenReaderAsync(
                    new ConnectionStringSource
                    {
                        ConnectionString = RequireEnvironment(SqlServerConnectionEnv)
                    },
                    new SqlTableConfig
                    {
                        Sql = WideBenchmarkSql.SqlServer(RowCount)
                    }).ConfigureAwait(false);
                await using var reader = raw.Normalize();

                await WriteAsync(reader, TargetTables.SqlServer).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task ClickHouse_to_clickhouse()
    {
        await MeasureAsync(
            nameof(ClickHouse_to_clickhouse),
            async () =>
            {
                await using var raw = await ClickHouseProvider.OpenReaderAsync(
                    new ConnectionStringSource
                    {
                        ConnectionString = Environment.GetEnvironmentVariable(ClickHouseSourceConnectionEnv) ??
                            _targetSource.ConnectionString
                    },
                    new SqlTableConfig
                    {
                        Sql = WideBenchmarkSql.ClickHouse(RowCount)
                    }).ConfigureAwait(false);
                await using var reader = raw.Normalize();

                await WriteAsync(reader, TargetTables.ClickHouse).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private async Task WriteAsync(DomainDataReader reader, ClickHouseTableName tableName)
    {
        await Writer.WriteAsync(
            _targetSource,
            reader,
            new ClickHouseWriteOptions
            {
                TableName = tableName,
                BatchSize = 100_000,
                MaxDegreeOfParallelism = 1
            },
            _meta).ConfigureAwait(false);
    }

    private static DataMetaContainer BuildMeta(int rowCount)
    {
        var meta = new DataMetaContainer();
        using var raw = new WideTypedDataReader(rowCount);
        using var reader = raw.Normalize().CollectMeta(meta);

        while (reader.Read())
        {
        }

        return meta;
    }

    private async Task MeasureAsync(string operation, Func<Task> action)
    {
        if (!_printPeakMemory)
        {
            await action().ConfigureAwait(false);
            return;
        }

        await PeakMemoryProbe.MeasureAsync(operation, action).ConfigureAwait(false);
    }

    private static string RequireEnvironment(string name)
    {
        return Environment.GetEnvironmentVariable(name) ??
            throw new InvalidOperationException($"Environment variable '{name}' is required for this benchmark.");
    }

    private static string BuildDropTableSql(ClickHouseTableName tableName)
    {
        var builder = new StringBuilder();
        builder.Append("DROP TABLE IF EXISTS ");
        builder.Append(tableName.ToSql());
        return builder.ToString();
    }

    private static class TargetTables
    {
        public static readonly ClickHouseTableName Csv = new() { Table = "bench_load_csv" };
        public static readonly ClickHouseTableName Excel = new() { Table = "bench_load_excel" };
        public static readonly ClickHouseTableName Json = new() { Table = "bench_load_json" };
        public static readonly ClickHouseTableName Xml = new() { Table = "bench_load_xml" };
        public static readonly ClickHouseTableName Postgres = new() { Table = "bench_load_postgres" };
        public static readonly ClickHouseTableName SqlServer = new() { Table = "bench_load_sqlserver" };
        public static readonly ClickHouseTableName ClickHouse = new() { Table = "bench_load_clickhouse" };

        public static IReadOnlyList<ClickHouseTableName> All { get; } =
        [
            Csv,
            Excel,
            Json,
            Xml,
            Postgres,
            SqlServer,
            ClickHouse
        ];
    }
}

internal sealed record WideBenchmarkFixtures(
    FileSystemSource FileSource,
    string CsvFileName,
    string JsonFileName,
    string XmlFileName,
    string ExcelFileName)
{
    public static async Task<WideBenchmarkFixtures> EnsureAsync(int rowCount)
    {
        var directory = FindFixtureDirectory();
        Directory.CreateDirectory(directory);

        var csvFileName = $"wide-v2-{rowCount.ToString(CultureInfo.InvariantCulture)}.csv";
        var jsonFileName = $"wide-v2-{rowCount.ToString(CultureInfo.InvariantCulture)}.json";
        var xmlFileName = $"wide-v2-{rowCount.ToString(CultureInfo.InvariantCulture)}.xml";
        var excelFileName = $"wide-v2-{rowCount.ToString(CultureInfo.InvariantCulture)}.xlsx";

        await EnsureCsvAsync(Path.Combine(directory, csvFileName), rowCount).ConfigureAwait(false);
        await EnsureJsonAsync(Path.Combine(directory, jsonFileName), rowCount).ConfigureAwait(false);
        EnsureXml(Path.Combine(directory, xmlFileName), rowCount);
        await EnsureExcelAsync(Path.Combine(directory, excelFileName), rowCount).ConfigureAwait(false);

        return new WideBenchmarkFixtures(
            new FileSystemSource(directory),
            csvFileName,
            jsonFileName,
            xmlFileName,
            excelFileName);
    }

    private static string FindFixtureDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Loader.Benchmarks.csproj")))
        {
            directory = directory.Parent;
        }

        var projectDirectory = directory?.FullName ?? AppContext.BaseDirectory;
        return Path.Combine(projectDirectory, "Fixtures", "Generated");
    }

    private static async Task EnsureCsvAsync(string path, int rowCount)
    {
        if (File.Exists(path))
        {
            return;
        }

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteLineAsync(WideBenchmarkDataSet.CsvHeader).ConfigureAwait(false);
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                await writer.WriteLineAsync(WideBenchmarkDataSet.CreateCsvRow(rowIndex)).ConfigureAwait(false);
            }
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static async Task EnsureJsonAsync(string path, int rowCount)
    {
        if (File.Exists(path))
        {
            return;
        }

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        await using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                WideBenchmarkDataSet.WriteJsonRow(writer, rowIndex);
            }

            writer.WriteEndArray();
            await writer.FlushAsync().ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static void EnsureXml(string path, int rowCount)
    {
        if (File.Exists(path))
        {
            return;
        }

        var tempPath = path + ".tmp";
        using (var writer = XmlWriter.Create(
                   tempPath,
                   new XmlWriterSettings
                   {
                       Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       Indent = false
                   }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("rows");
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                WideBenchmarkDataSet.WriteXmlRow(writer, rowIndex);
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static async Task EnsureExcelAsync(string path, int rowCount)
    {
        if (File.Exists(path))
        {
            return;
        }

        var tempPath = path + ".tmp.xlsx";
        await using (var writer = await ExcelDataWriter.CreateAsync(tempPath).ConfigureAwait(false))
        {
            using var reader = new WideTextDataReader(rowCount);
            await writer.WriteAsync(reader, "data").ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}

internal static class WideBenchmarkDataSet
{
    public static readonly string[] Columns =
    [
        "id",
        "signed_tiny",
        "signed_small",
        "signed_int",
        "signed_big",
        "unsigned_tiny",
        "unsigned_small",
        "unsigned_int",
        "amount_decimal",
        "ratio_decimal",
        "created_at",
        "active",
        "city_low_card",
        "name_high_card",
        "nullable_amount",
        "nullable_text"
    ];

    public static string CsvHeader { get; } = string.Join(",", Columns);

    public static AutoCastSchema CreateAutoCastSchema()
    {
        return new AutoCastSchema
        {
            Fields =
            [
                Integer("id"),
                Integer("signed_tiny"),
                Integer("signed_small"),
                Integer("signed_int"),
                Integer("signed_big"),
                Integer("unsigned_tiny"),
                Integer("unsigned_small"),
                Integer("unsigned_int"),
                Number("amount_decimal"),
                Number("ratio_decimal"),
                DateTime("created_at"),
                Boolean("active"),
                NullableNumber("nullable_amount")
            ]
        };
    }

    public static string CreateCsvRow(int rowIndex)
    {
        var builder = new StringBuilder(192);
        AppendTextColumns(builder, rowIndex);
        if (IsNullableRow(rowIndex))
        {
            builder.Append(",,");
        }
        else
        {
            builder
                .Append(',')
                .Append(FormatInvariant(NullableAmount(rowIndex)))
                .Append(',')
                .Append(NullableText(rowIndex));
        }

        return builder.ToString();
    }

    public static void WriteJsonRow(System.Text.Json.Utf8JsonWriter writer, int rowIndex)
    {
        writer.WriteStartObject();
        WriteString(writer, "id", Id(rowIndex));
        WriteString(writer, "signed_tiny", SignedTiny(rowIndex));
        WriteString(writer, "signed_small", SignedSmall(rowIndex));
        WriteString(writer, "signed_int", SignedInt(rowIndex));
        WriteString(writer, "signed_big", SignedBig(rowIndex));
        WriteString(writer, "unsigned_tiny", UnsignedTiny(rowIndex));
        WriteString(writer, "unsigned_small", UnsignedSmall(rowIndex));
        WriteString(writer, "unsigned_int", UnsignedInt(rowIndex));
        WriteString(writer, "amount_decimal", AmountDecimal(rowIndex));
        WriteString(writer, "ratio_decimal", RatioDecimal(rowIndex));
        writer.WriteString("created_at", CreatedAt(rowIndex).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        writer.WriteString("active", Active(rowIndex).ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        writer.WriteString("city_low_card", CityLowCard(rowIndex));
        writer.WriteString("name_high_card", NameHighCard(rowIndex));
        if (IsNullableRow(rowIndex))
        {
            writer.WriteNull("nullable_amount");
            writer.WriteNull("nullable_text");
        }
        else
        {
            WriteString(writer, "nullable_amount", NullableAmount(rowIndex));
            writer.WriteString("nullable_text", NullableText(rowIndex));
        }

        writer.WriteEndObject();
    }

    public static void WriteXmlRow(XmlWriter writer, int rowIndex)
    {
        writer.WriteStartElement("row");
        for (var ordinal = 0; ordinal < Columns.Length; ordinal++)
        {
            var value = GetTextValue(rowIndex, ordinal);
            if (value == DBNull.Value)
            {
                continue;
            }

            writer.WriteElementString(Columns[ordinal], (string)value);
        }

        writer.WriteEndElement();
    }

    public static object GetTypedValue(int rowIndex, int ordinal)
    {
        return ordinal switch
        {
            0 => Id(rowIndex),
            1 => SignedTiny(rowIndex),
            2 => SignedSmall(rowIndex),
            3 => SignedInt(rowIndex),
            4 => SignedBig(rowIndex),
            5 => UnsignedTiny(rowIndex),
            6 => UnsignedSmall(rowIndex),
            7 => UnsignedInt(rowIndex),
            8 => AmountDecimal(rowIndex),
            9 => RatioDecimal(rowIndex),
            10 => CreatedAt(rowIndex),
            11 => Active(rowIndex),
            12 => CityLowCard(rowIndex),
            13 => NameHighCard(rowIndex),
            14 => IsNullableRow(rowIndex) ? DBNull.Value : NullableAmount(rowIndex),
            15 => IsNullableRow(rowIndex) ? DBNull.Value : NullableText(rowIndex),
            _ => throw new IndexOutOfRangeException()
        };
    }

    public static object GetTextValue(int rowIndex, int ordinal)
    {
        var value = GetTypedValue(rowIndex, ordinal);
        if (value == DBNull.Value)
        {
            return DBNull.Value;
        }

        return value switch
        {
            bool active => active.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    public static Type GetTypedFieldType(int ordinal)
    {
        return ordinal switch
        {
            >= 0 and <= 7 => typeof(long),
            8 or 9 or 14 => typeof(decimal),
            10 => typeof(DateTime),
            11 => typeof(bool),
            12 or 13 or 15 => typeof(string),
            _ => throw new IndexOutOfRangeException()
        };
    }

    private static AutoCastField Integer(string name)
    {
        return new AutoCastField
        {
            Name = name,
            Format = AutoCastFormats.Integer
        };
    }

    private static AutoCastField Number(string name)
    {
        return new AutoCastField
        {
            Name = name,
            Format = AutoCastFormats.InvariantNumber
        };
    }

    private static AutoCastField NullableNumber(string name)
    {
        return new AutoCastField
        {
            Name = name,
            Format = new EmptyStringAsNullAutoCastFormat(AutoCastFormats.InvariantNumber)
        };
    }

    private static AutoCastField DateTime(string name)
    {
        return new AutoCastField
        {
            Name = name,
            Format = AutoCastFormats.DateTimeExact("yyyy-MM-dd HH:mm:ss")
        };
    }

    private static AutoCastField Boolean(string name)
    {
        return new AutoCastField
        {
            Name = name,
            Format = AutoCastFormats.Boolean
        };
    }

    private static void AppendTextColumns(StringBuilder builder, int rowIndex)
    {
        builder
            .Append(Id(rowIndex))
            .Append(',')
            .Append(SignedTiny(rowIndex))
            .Append(',')
            .Append(SignedSmall(rowIndex))
            .Append(',')
            .Append(SignedInt(rowIndex))
            .Append(',')
            .Append(SignedBig(rowIndex))
            .Append(',')
            .Append(UnsignedTiny(rowIndex))
            .Append(',')
            .Append(UnsignedSmall(rowIndex))
            .Append(',')
            .Append(UnsignedInt(rowIndex))
            .Append(',')
            .Append(FormatInvariant(AmountDecimal(rowIndex)))
            .Append(',')
            .Append(FormatInvariant(RatioDecimal(rowIndex)))
            .Append(',')
            .Append(CreatedAt(rowIndex).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Append(',')
            .Append(Active(rowIndex).ToString(CultureInfo.InvariantCulture).ToLowerInvariant())
            .Append(',')
            .Append(CityLowCard(rowIndex))
            .Append(',')
            .Append(NameHighCard(rowIndex));
    }

    private static string FormatInvariant<T>(T value)
        where T : IFormattable
    {
        return value.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static void WriteString<T>(System.Text.Json.Utf8JsonWriter writer, string name, T value)
        where T : IFormattable
    {
        writer.WriteString(name, value.ToString(null, CultureInfo.InvariantCulture));
    }

    private static long Id(int rowIndex)
    {
        return rowIndex;
    }

    private static long SignedTiny(int rowIndex)
    {
        return rowIndex % 255 - 128;
    }

    private static long SignedSmall(int rowIndex)
    {
        return rowIndex % 65_535 - 32_768;
    }

    private static long SignedInt(int rowIndex)
    {
        return rowIndex % 2 == 0 ? rowIndex : -rowIndex;
    }

    private static long SignedBig(int rowIndex)
    {
        return 5_000_000_000L + rowIndex;
    }

    private static long UnsignedTiny(int rowIndex)
    {
        return rowIndex % 256;
    }

    private static long UnsignedSmall(int rowIndex)
    {
        return rowIndex % 65_536;
    }

    private static long UnsignedInt(int rowIndex)
    {
        return rowIndex;
    }

    private static decimal AmountDecimal(int rowIndex)
    {
        return Math.Round((rowIndex % 100_000) / 100m + 0.01m, 2);
    }

    private static decimal RatioDecimal(int rowIndex)
    {
        return Math.Round((rowIndex % 10_000) / 10_000m, 4);
    }

    private static DateTime CreatedAt(int rowIndex)
    {
        return new DateTime(2026, 1, 1, 0, 0, 0).AddSeconds(rowIndex % 86_400);
    }

    private static bool Active(int rowIndex)
    {
        return rowIndex % 2 == 0;
    }

    private static string CityLowCard(int rowIndex)
    {
        return "city_" + (rowIndex % 16).ToString(CultureInfo.InvariantCulture);
    }

    private static string NameHighCard(int rowIndex)
    {
        return "name_" + rowIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsNullableRow(int rowIndex)
    {
        return rowIndex % 10 == 0;
    }

    private static decimal NullableAmount(int rowIndex)
    {
        return Math.Round((rowIndex % 10_000) / 100m, 2);
    }

    private static string NullableText(int rowIndex)
    {
        return "nullable_" + rowIndex.ToString(CultureInfo.InvariantCulture);
    }
}

internal sealed class EmptyStringAsNullAutoCastFormat : IAutoCastFormat
{
    private readonly IAutoCastFormat _inner;

    public EmptyStringAsNullAutoCastFormat(IAutoCastFormat inner)
    {
        _inner = inner;
    }

    public string Name => _inner.Name + "+EmptyStringAsNull";

    public DataType DataType => _inner.DataType;

    public Type ClrType => _inner.ClrType;

    public bool TryConvert(string value, out object converted)
    {
        if (value.Length == 0)
        {
            converted = DBNull.Value;
            return true;
        }

        return _inner.TryConvert(value, out converted);
    }
}

internal static class WideBenchmarkSql
{
    public static string Postgres(int rowCount)
    {
        var builder = new StringBuilder();
        builder
            .AppendLine("select")
            .AppendLine("    g::bigint as id,")
            .AppendLine("    ((g % 255) - 128)::bigint as signed_tiny,")
            .AppendLine("    ((g % 65535) - 32768)::bigint as signed_small,")
            .AppendLine("    case when g % 2 = 0 then g else -g end::bigint as signed_int,")
            .AppendLine("    (5000000000 + g)::bigint as signed_big,")
            .AppendLine("    (g % 256)::bigint as unsigned_tiny,")
            .AppendLine("    (g % 65536)::bigint as unsigned_small,")
            .AppendLine("    g::bigint as unsigned_int,")
            .AppendLine("    round(((g % 100000)::numeric / 100) + 0.01, 2) as amount_decimal,")
            .AppendLine("    round((g % 10000)::numeric / 10000, 4) as ratio_decimal,")
            .AppendLine("    (timestamp '2026-01-01 00:00:00' + ((g % 86400) * interval '1 second')) as created_at,")
            .AppendLine("    (g % 2 = 0) as active,")
            .AppendLine("    ('city_' || (g % 16)) as city_low_card,")
            .AppendLine("    ('name_' || g) as name_high_card,")
            .AppendLine("    case when g % 10 = 0 then null else round((g % 10000)::numeric / 100, 2) end as nullable_amount,")
            .AppendLine("    case when g % 10 = 0 then null else ('nullable_' || g) end as nullable_text")
            .Append("from generate_series(0, ")
            .Append(rowCount - 1)
            .AppendLine(") as rows(g)");
        return builder.ToString();
    }

    public static string SqlServer(int rowCount)
    {
        var builder = new StringBuilder();
        builder
            .AppendLine("with source_rows as")
            .AppendLine("(")
            .Append("    select top (")
            .Append(rowCount)
            .AppendLine(") row_number() over (order by (select null)) - 1 as g")
            .AppendLine("    from sys.all_objects a")
            .AppendLine("    cross join sys.all_objects b")
            .AppendLine("    cross join sys.all_objects c")
            .AppendLine(")")
            .AppendLine("select")
            .AppendLine("    cast(g as bigint) as id,")
            .AppendLine("    cast((g % 255) - 128 as bigint) as signed_tiny,")
            .AppendLine("    cast((g % 65535) - 32768 as bigint) as signed_small,")
            .AppendLine("    cast(case when g % 2 = 0 then g else -g end as bigint) as signed_int,")
            .AppendLine("    cast(5000000000 + g as bigint) as signed_big,")
            .AppendLine("    cast(g % 256 as bigint) as unsigned_tiny,")
            .AppendLine("    cast(g % 65536 as bigint) as unsigned_small,")
            .AppendLine("    cast(g as bigint) as unsigned_int,")
            .AppendLine("    cast(round(cast(g % 100000 as decimal(18, 4)) / 100 + 0.01, 2) as decimal(18, 2)) as amount_decimal,")
            .AppendLine("    cast(round(cast(g % 10000 as decimal(18, 6)) / 10000, 4) as decimal(18, 4)) as ratio_decimal,")
            .AppendLine("    dateadd(second, g % 86400, cast('2026-01-01T00:00:00' as datetime2)) as created_at,")
            .AppendLine("    cast(case when g % 2 = 0 then 1 else 0 end as bit) as active,")
            .AppendLine("    concat('city_', g % 16) as city_low_card,")
            .AppendLine("    concat('name_', g) as name_high_card,")
            .AppendLine("    case when g % 10 = 0 then null else cast(round(cast(g % 10000 as decimal(18, 4)) / 100, 2) as decimal(18, 2)) end as nullable_amount,")
            .AppendLine("    case when g % 10 = 0 then null else concat('nullable_', g) end as nullable_text")
            .AppendLine("from source_rows");
        return builder.ToString();
    }

    public static string ClickHouse(int rowCount)
    {
        var builder = new StringBuilder();
        builder
            .AppendLine("select")
            .AppendLine("    toInt64(number) as id,")
            .AppendLine("    toInt64(number % 255) - 128 as signed_tiny,")
            .AppendLine("    toInt64(number % 65535) - 32768 as signed_small,")
            .AppendLine("    if(number % 2 = 0, toInt64(number), -toInt64(number)) as signed_int,")
            .AppendLine("    toInt64(5000000000) + toInt64(number) as signed_big,")
            .AppendLine("    toInt64(number % 256) as unsigned_tiny,")
            .AppendLine("    toInt64(number % 65536) as unsigned_small,")
            .AppendLine("    toInt64(number) as unsigned_int,")
            .AppendLine("    toDecimal64(round((number % 100000) / 100 + 0.01, 2), 2) as amount_decimal,")
            .AppendLine("    toDecimal64(round((number % 10000) / 10000, 4), 4) as ratio_decimal,")
            .AppendLine("    toDateTime('2026-01-01 00:00:00') + toIntervalSecond(number % 86400) as created_at,")
            .AppendLine("    number % 2 = 0 as active,")
            .AppendLine("    concat('city_', toString(number % 16)) as city_low_card,")
            .AppendLine("    concat('name_', toString(number)) as name_high_card,")
            .AppendLine("    if(number % 10 = 0, cast(null, 'Nullable(Decimal64(2))'), toDecimal64(round((number % 10000) / 100, 2), 2)) as nullable_amount,")
            .AppendLine("    if(number % 10 = 0, cast(null, 'Nullable(String)'), concat('nullable_', toString(number))) as nullable_text")
            .Append("from numbers(")
            .Append(rowCount)
            .AppendLine(")");
        return builder.ToString();
    }
}

internal sealed class WideTypedDataReader : WideBenchmarkDataReader
{
    public WideTypedDataReader(int rowCount)
        : base(rowCount)
    {
    }

    public override object GetValue(int ordinal)
    {
        return WideBenchmarkDataSet.GetTypedValue(RowIndex, ordinal);
    }

    public override Type GetFieldType(int ordinal)
    {
        return WideBenchmarkDataSet.GetTypedFieldType(ordinal);
    }
}

internal sealed class WideTextDataReader : WideBenchmarkDataReader
{
    public WideTextDataReader(int rowCount)
        : base(rowCount)
    {
    }

    public override object GetValue(int ordinal)
    {
        return WideBenchmarkDataSet.GetTextValue(RowIndex, ordinal);
    }

    public override Type GetFieldType(int ordinal)
    {
        return typeof(string);
    }
}

internal abstract class WideBenchmarkDataReader : DbDataReader
{
    private readonly int _rowCount;

    protected WideBenchmarkDataReader(int rowCount)
    {
        _rowCount = rowCount;
    }

    protected int RowIndex { get; private set; } = -1;

    public override int FieldCount => WideBenchmarkDataSet.Columns.Length;

    public override bool HasRows => _rowCount > 0;

    public override int RecordsAffected => -1;

    public override bool IsClosed => false;

    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        RowIndex++;
        return RowIndex < _rowCount;
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public override string GetName(int ordinal)
    {
        return WideBenchmarkDataSet.Columns[ordinal];
    }

    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < WideBenchmarkDataSet.Columns.Length; i++)
        {
            if (WideBenchmarkDataSet.Columns[i].Equals(name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public override string GetDataTypeName(int ordinal)
    {
        return GetFieldType(ordinal).Name;
    }

    public override bool IsDBNull(int ordinal)
    {
        return GetValue(ordinal) == DBNull.Value;
    }

    public override DataTable GetSchemaTable()
    {
        var table = new DataTable("SchemaTable");
        table.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
        table.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
        table.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
        table.Columns.Add(SchemaTableColumn.ProviderType, typeof(int));
        table.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool));
        table.Columns.Add(SchemaTableColumn.IsKey, typeof(bool));
        table.Columns.Add(SchemaTableColumn.IsUnique, typeof(bool));
        table.Columns.Add(SchemaTableColumn.IsLong, typeof(bool));

        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            var row = table.NewRow();
            row[SchemaTableColumn.ColumnName] = GetName(ordinal);
            row[SchemaTableColumn.ColumnOrdinal] = ordinal;
            row[SchemaTableColumn.DataType] = GetFieldType(ordinal);
            row[SchemaTableColumn.ProviderType] = ordinal;
            row[SchemaTableColumn.AllowDBNull] = ordinal is 14 or 15;
            row[SchemaTableColumn.IsKey] = false;
            row[SchemaTableColumn.IsUnique] = false;
            row[SchemaTableColumn.IsLong] = false;
            table.Rows.Add(row);
        }

        return table;
    }

    public override bool NextResult()
    {
        return false;
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new InvalidCastException();

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new InvalidCastException();

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override IEnumerator<object> GetEnumerator()
    {
        while (Read())
        {
            yield return this;
        }
    }
}

internal static class PeakMemoryProbe
{
    public static async Task MeasureAsync(string operation, Func<Task> action)
    {
        using var cancellation = new CancellationTokenSource();
        var sampler = Task.Run(
            async () => await SampleAsync(operation, cancellation.Token).ConfigureAwait(false),
            CancellationToken.None);

        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            await sampler.ConfigureAwait(false);
        }
    }

    private static async Task SampleAsync(string operation, CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        long peakWorkingSet = 0;
        long peakPrivate = 0;
        long peakManagedHeap = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            process.Refresh();
            peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
            peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64);
            peakManagedHeap = Math.Max(peakManagedHeap, GC.GetTotalMemory(forceFullCollection: false));

            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        process.Refresh();
        peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
        peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64);
        peakManagedHeap = Math.Max(peakManagedHeap, GC.GetTotalMemory(forceFullCollection: false));

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{operation} peak: working_set={ToMegabytes(peakWorkingSet):0.0} MB; private={ToMegabytes(peakPrivate):0.0} MB; managed_heap={ToMegabytes(peakManagedHeap):0.0} MB"));
    }

    private static double ToMegabytes(long bytes)
    {
        return bytes / 1024d / 1024d;
    }
}
