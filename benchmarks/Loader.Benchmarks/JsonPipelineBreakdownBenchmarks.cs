using System.Data.Common;
using BenchmarkDotNet.Attributes;
using ClickHouse.Client.ADO;
using Loader.Core.Providers.Json;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;

/// <summary>
/// Временный benchmark для поиска дорогой фазы JSON pipeline.
/// Когда JSON будет оптимизирован, этот класс можно удалить.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class JsonPipelineBreakdownBenchmarks
{
    private const string TargetConnectionEnv = "LOADER_BENCH_CLICKHOUSE_TARGET";

    private static readonly JsonProvider JsonProvider = new();
    private static readonly ClickHouseWriter Writer = new();
    private static readonly ClickHouseTableName TargetTable = new() { Table = "bench_json_breakdown" };

    private WideBenchmarkFixtures _fixtures = null!;
    private AutoCastSchema _autoCastSchema = null!;
    private JsonTableSchema _schema = null!;
    private DataMetaContainer _meta = null!;
    private ConnectionStringSource _targetSource = null!;

    [Params(1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _fixtures = await WideBenchmarkFixtures.EnsureAsync(RowCount).ConfigureAwait(false);
        _autoCastSchema = WideBenchmarkDataSet.CreateAutoCastSchema();
        _schema = ExplicitSchema();
        _meta = BuildMeta(RowCount);
        _targetSource = new ConnectionStringSource
        {
            ConnectionString = RequireEnvironment(TargetConnectionEnv)
        };
    }

    [IterationSetup(Target = nameof(Json_normalize_autocast_write_clickhouse_known_schema))]
    public void DropTarget()
    {
        using var connection = new ClickHouseConnection(_targetSource.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {TargetTable.ToSql()}";
        command.ExecuteNonQuery();
    }

    [Benchmark]
    public async Task<int> Json_analyze_schema_only()
    {
        var schema = await JsonProvider
            .AnalyzeSchemaAsync(_fixtures.FileSource, _fixtures.JsonFileName, Array.Empty<string>())
            .ConfigureAwait(false);

        return schema.Columns.Count;
    }

    [Benchmark]
    public async Task<long> Json_raw_read_known_schema()
    {
        await using var reader = await OpenRawReaderAsync().ConfigureAwait(false);
        return Consume(reader);
    }

    [Benchmark]
    public async Task<long> Json_normalize_known_schema()
    {
        await using var raw = await OpenRawReaderAsync().ConfigureAwait(false);
        await using var reader = raw.Normalize();
        return Consume(reader);
    }

    [Benchmark]
    public async Task<long> Json_normalize_autocast_known_schema()
    {
        await using var raw = await OpenRawReaderAsync().ConfigureAwait(false);
        await using var reader = raw
            .Normalize()
            .AutoCast(_autoCastSchema);

        return Consume(reader);
    }

    [Benchmark]
    public async Task Json_normalize_autocast_write_clickhouse_known_schema()
    {
        await using var raw = await OpenRawReaderAsync().ConfigureAwait(false);
        await using var reader = raw
            .Normalize()
            .AutoCast(_autoCastSchema);

        await Writer.WriteAsync(
            _targetSource,
            reader,
            new ClickHouseWriteOptions
            {
                TableName = TargetTable,
                BatchSize = 100_000,
                MaxDegreeOfParallelism = 1
            },
            _meta).ConfigureAwait(false);
    }

    private ValueTask<DbDataReader> OpenRawReaderAsync()
    {
        return JsonProvider.OpenReaderAsync(
            _fixtures.FileSource,
            new JsonTableConfig
            {
                FileName = _fixtures.JsonFileName,
                ArrayPath = Array.Empty<string>(),
                Schema = _schema
            });
    }

    private static JsonTableSchema ExplicitSchema()
    {
        return new JsonTableSchema
        {
            Columns = WideBenchmarkDataSet.Columns
                .Select(static name => new JsonColumnSchema
                {
                    Name = name,
                    Path = name
                })
                .ToArray()
        };
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

    private static string RequireEnvironment(string name)
    {
        return Environment.GetEnvironmentVariable(name) ??
            throw new InvalidOperationException($"Environment variable '{name}' is required for this benchmark.");
    }

    private static long Consume(DbDataReader reader)
    {
        long checksum = 0;
        while (reader.Read())
        {
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                var value = reader.GetValue(ordinal);
                if (value == DBNull.Value)
                {
                    continue;
                }

                checksum += value switch
                {
                    bool boolValue => boolValue ? 1 : 0,
                    int intValue => intValue,
                    long longValue => longValue,
                    decimal decimalValue => (long)decimalValue,
                    DateTime dateTime => dateTime.Day,
                    string text => text.Length,
                    _ => value.GetHashCode()
                };
            }
        }

        return checksum;
    }
}
