using System.Data;
using System.Data.Common;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Loader.Core.Data;
using Loader.Core.Data.AutoCast;
using Loader.Core.Providers.Csv;
using Loader.Core.Sources;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]
[ShortRunJob]
public class ReaderPipelineBenchmarks
{
    [Params(100_000, 1_000_000)]
    public int RowCount { get; set; }

    [Benchmark(Baseline = true)]
    public long Raw_reader_get_value()
    {
        using var reader = new SyntheticDbDataReader(RowCount);
        return Consume(reader);
    }

    [Benchmark]
    public long Normalize()
    {
        using var raw = new SyntheticDbDataReader(RowCount);
        using var reader = raw.Normalize();
        return Consume(reader);
    }

    [Benchmark]
    public long Normalize_where_keeps_half()
    {
        using var raw = new SyntheticDbDataReader(RowCount);
        using var reader = raw
            .Normalize()
            .Where(row => row.Integer("id") % 2 == 0);

        return Consume(reader);
    }

    [Benchmark]
    public long Normalize_autocast()
    {
        using var raw = new SyntheticDbDataReader(RowCount);
        using var reader = raw
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "amount_text", Format = AutoCastFormats.InvariantNumber },
                    new AutoCastField { Name = "created_text", Format = AutoCastFormats.DateExact("yyyy-MM-dd") }
                ]
            });

        return Consume(reader);
    }

    [Benchmark]
    public long Normalize_collect_meta()
    {
        using var raw = new SyntheticDbDataReader(RowCount);
        var meta = new DataMetaContainer();
        using var reader = raw
            .Normalize()
            .CollectMeta(meta);

        return Consume(reader);
    }

    [Benchmark]
    public long Normalize_collect_meta_max_cardinality_zero()
    {
        using var raw = new SyntheticDbDataReader(RowCount);
        var meta = new DataMetaContainer(new DataMetaOptions
        {
            MaxCardinality = 0
        });
        using var reader = raw
            .Normalize()
            .CollectMeta(meta);

        return Consume(reader);
    }

    [Benchmark]
    public long Normalize_where_autocast_collect_meta()
    {
        using var raw = new SyntheticDbDataReader(RowCount);
        var meta = new DataMetaContainer();
        using var reader = raw
            .Normalize()
            .Where(row => row.Integer("id") % 2 == 0)
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "amount_text", Format = AutoCastFormats.InvariantNumber },
                    new AutoCastField { Name = "created_text", Format = AutoCastFormats.DateExact("yyyy-MM-dd") }
                ]
            })
            .CollectMeta(meta);

        return Consume(reader);
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
                    int intValue => intValue,
                    long longValue => longValue,
                    decimal decimalValue => (long)decimalValue,
                    DateOnly dateOnly => dateOnly.DayNumber,
                    DateTime dateTime => dateTime.Day,
                    string text => text.Length,
                    _ => value.GetHashCode()
                };
            }
        }

        return checksum;
    }
}

[MemoryDiagnoser]
[ShortRunJob]
public class CsvPipelineBenchmarks
{
    private static readonly CsvProvider Provider = new();

    private InMemoryFileSource _source = null!;
    private CsvTableConfig _config = null!;

    [Params(100_000, 1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = new InMemoryFileSource(CreateCsv(RowCount));
        _config = new CsvTableConfig
        {
            FileName = "benchmark.csv"
        };
    }

    [Benchmark(Baseline = true)]
    public async Task<long> Csv_provider_raw()
    {
        await using var reader = await Provider.OpenReaderAsync(_source, _config);
        return Consume(reader);
    }

    [Benchmark]
    public async Task<long> Csv_provider_normalize()
    {
        await using var raw = await Provider.OpenReaderAsync(_source, _config);
        await using var reader = raw.Normalize();
        return Consume(reader);
    }

    [Benchmark]
    public async Task<long> Csv_provider_normalize_lazy_unsafe()
    {
        await using var raw = await Provider.OpenReaderAsync(_source, _config);
        await using var reader = raw.Normalize(new NormalizeOptions { Buffer = false });
        return Consume(reader);
    }

    [Benchmark]
    public async Task<long> Csv_provider_normalize_autocast()
    {
        await using var raw = await Provider.OpenReaderAsync(_source, _config);
        await using var reader = raw
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer },
                    new AutoCastField { Name = "amount", Format = AutoCastFormats.InvariantNumber },
                    new AutoCastField { Name = "created", Format = AutoCastFormats.DateExact("yyyy-MM-dd") }
                ]
            });

        return Consume(reader);
    }

    private static string CreateCsv(int rowCount)
    {
        var builder = new StringBuilder(capacity: Math.Min(rowCount * 48, 64 * 1024 * 1024));
        builder.AppendLine("id,city,amount,created,note");

        for (var i = 0; i < rowCount; i++)
        {
            builder
                .Append(i)
                .Append(',')
                .Append(i % 2 == 0 ? "Moscow" : "London")
                .Append(',')
                .Append((i % 1000 + 0.50m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                .Append(',')
                .Append(System.Globalization.CultureInfo.InvariantCulture, $"2026-01-{i % 28 + 1:00}")
                .Append(',')
                .Append("simple text")
                .AppendLine();
        }

        return builder.ToString();
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
                    int intValue => intValue,
                    long longValue => longValue,
                    decimal decimalValue => (long)decimalValue,
                    DateOnly dateOnly => dateOnly.DayNumber,
                    string text => text.Length,
                    _ => value.GetHashCode()
                };
            }
        }

        return checksum;
    }
}

internal sealed class InMemoryFileSource : IFileSource
{
    private readonly byte[] _bytes;

    public InMemoryFileSource(string content)
    {
        _bytes = Encoding.UTF8.GetBytes(content);
    }

    public Stream OpenRead(string fileName)
    {
        return new MemoryStream(_bytes, writable: false);
    }
}

internal sealed class SyntheticDbDataReader : DbDataReader
{
    private readonly int _rowCount;
    private int _rowIndex = -1;

    public SyntheticDbDataReader(int rowCount)
    {
        _rowCount = rowCount;
    }

    public override int FieldCount => 5;

    public override bool HasRows => _rowCount > 0;

    public override int RecordsAffected => -1;

    public override bool IsClosed => false;

    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        _rowIndex++;
        return _rowIndex < _rowCount;
    }

    public override object GetValue(int ordinal)
    {
        return ordinal switch
        {
            0 => _rowIndex,
            1 => _rowIndex % 2 == 0 ? "Moscow" : "London",
            2 => (_rowIndex % 1000 + 0.50m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            3 => $"2026-01-{_rowIndex % 28 + 1:00}",
            4 => new DateTime(2026, 1, _rowIndex % 28 + 1),
            _ => throw new IndexOutOfRangeException()
        };
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
        return ordinal switch
        {
            0 => "id",
            1 => "city",
            2 => "amount_text",
            3 => "created_text",
            4 => "created_at",
            _ => throw new IndexOutOfRangeException()
        };
    }

    public override int GetOrdinal(string name)
    {
        return name switch
        {
            "id" => 0,
            "city" => 1,
            "amount_text" => 2,
            "created_text" => 3,
            "created_at" => 4,
            _ => throw new IndexOutOfRangeException()
        };
    }

    public override Type GetFieldType(int ordinal)
    {
        return ordinal switch
        {
            0 => typeof(int),
            1 => typeof(string),
            2 => typeof(string),
            3 => typeof(string),
            4 => typeof(DateTime),
            _ => throw new IndexOutOfRangeException()
        };
    }

    public override string GetDataTypeName(int ordinal)
    {
        return ordinal switch
        {
            0 => "integer",
            1 => "text",
            2 => "text",
            3 => "text",
            4 => "timestamp",
            _ => throw new IndexOutOfRangeException()
        };
    }

    public override bool IsDBNull(int ordinal)
    {
        return false;
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
            row[SchemaTableColumn.AllowDBNull] = false;
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

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

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
