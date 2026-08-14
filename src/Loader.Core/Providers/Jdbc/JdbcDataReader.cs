using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Loader.Core.Providers.Jdbc;

internal sealed class JdbcDataReader : DbDataReader
{
    private readonly java.sql.Connection connection;
    private readonly java.sql.Statement statement;
    private readonly java.sql.ResultSet resultSet;
    private readonly Column[] columns;
    private bool isClosed;

    public JdbcDataReader(
        java.sql.Connection connection,
        java.sql.Statement statement,
        java.sql.ResultSet resultSet)
    {
        this.connection = connection;
        this.statement = statement;
        this.resultSet = resultSet;

        var metadata = resultSet.getMetaData();
        columns = Enumerable.Range(1, metadata.getColumnCount())
            .Select(ordinal => CreateColumn(metadata, ordinal))
            .ToArray();
    }

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int Depth => 0;

    public override int FieldCount => columns.Length;

    public override bool HasRows => true;

    public override bool IsClosed => isClosed;

    public override int RecordsAffected => -1;

    public override bool Read()
    {
        return resultSet.next();
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read());
    }

    public override bool NextResult()
    {
        return false;
    }

    public override object GetValue(int ordinal)
    {
        EnsureOrdinal(ordinal);
        var column = columns[ordinal];
        var jdbcOrdinal = ordinal + 1;
        object? value = column.JdbcType switch
        {
            java.sql.Types.BIT or java.sql.Types.BOOLEAN => resultSet.getBoolean(jdbcOrdinal),
            java.sql.Types.TINYINT or java.sql.Types.SMALLINT or java.sql.Types.INTEGER => resultSet.getInt(jdbcOrdinal),
            java.sql.Types.BIGINT => resultSet.getLong(jdbcOrdinal),
            java.sql.Types.FLOAT or java.sql.Types.REAL or java.sql.Types.DOUBLE => resultSet.getDouble(jdbcOrdinal),
            java.sql.Types.NUMERIC or java.sql.Types.DECIMAL => ReadDecimal(jdbcOrdinal),
            java.sql.Types.DATE => ReadDate(jdbcOrdinal),
            java.sql.Types.TIME or java.sql.Types.TIME_WITH_TIMEZONE => ReadTime(jdbcOrdinal),
            java.sql.Types.TIMESTAMP or java.sql.Types.TIMESTAMP_WITH_TIMEZONE => ReadTimestamp(jdbcOrdinal),
            _ => ReadObject(jdbcOrdinal)
        };

        if (resultSet.wasNull())
        {
            return DBNull.Value;
        }

        return value ?? DBNull.Value;
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            values[ordinal] = GetValue(ordinal);
        }

        return count;
    }

    public override string GetName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return columns[ordinal].Name;
    }

    public override int GetOrdinal(string name)
    {
        for (var ordinal = 0; ordinal < columns.Length; ordinal++)
        {
            if (string.Equals(columns[ordinal].Name, name, StringComparison.Ordinal))
            {
                return ordinal;
            }
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return columns[ordinal].TypeName;
    }

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return columns[ordinal].ClrType;
    }

    public override bool IsDBNull(int ordinal)
    {
        return GetValue(ordinal) is DBNull;
    }

    public override IEnumerator GetEnumerator()
    {
        while (Read())
        {
            yield return this;
        }
    }

    public override bool GetBoolean(int ordinal)
    {
        return Convert.ToBoolean(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override byte GetByte(int ordinal)
    {
        return Convert.ToByte(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        throw new NotSupportedException();
    }

    public override char GetChar(int ordinal)
    {
        return GetString(ordinal)[0];
    }

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var text = GetString(ordinal);
        if (buffer is null)
        {
            return text.Length;
        }

        var available = Math.Max(0, text.Length - checked((int)dataOffset));
        var count = Math.Min(length, available);
        text.CopyTo(checked((int)dataOffset), buffer, bufferOffset, count);
        return count;
    }

    public override DateTime GetDateTime(int ordinal)
    {
        return Convert.ToDateTime(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override decimal GetDecimal(int ordinal)
    {
        return Convert.ToDecimal(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override double GetDouble(int ordinal)
    {
        return Convert.ToDouble(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override float GetFloat(int ordinal)
    {
        return Convert.ToSingle(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override Guid GetGuid(int ordinal)
    {
        throw new NotSupportedException();
    }

    public override short GetInt16(int ordinal)
    {
        return Convert.ToInt16(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override int GetInt32(int ordinal)
    {
        return Convert.ToInt32(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override long GetInt64(int ordinal)
    {
        return Convert.ToInt64(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override string GetString(int ordinal)
    {
        return Convert.ToString(GetTypedValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public override DataTable GetSchemaTable()
    {
        var table = new DataTable("SchemaTable");
        table.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
        table.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
        table.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
        table.Columns.Add(SchemaTableColumn.ProviderType, typeof(int));
        table.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool));

        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            var row = table.NewRow();
            row[SchemaTableColumn.ColumnName] = columns[ordinal].Name;
            row[SchemaTableColumn.ColumnOrdinal] = ordinal;
            row[SchemaTableColumn.DataType] = columns[ordinal].ClrType;
            row[SchemaTableColumn.ProviderType] = columns[ordinal].JdbcType;
            row[SchemaTableColumn.AllowDBNull] = columns[ordinal].Nullable;
            table.Rows.Add(row);
        }

        return table;
    }

    public override void Close()
    {
        if (isClosed)
        {
            return;
        }

        isClosed = true;
        resultSet.close();
        statement.close();
        connection.close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    private object GetTypedValue(int ordinal)
    {
        var value = GetValue(ordinal);
        if (value is DBNull)
        {
            throw new InvalidCastException($"Column '{GetName(ordinal)}' is null.");
        }

        return value;
    }

    private void EnsureOrdinal(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new IndexOutOfRangeException($"Column ordinal {ordinal} is out of range.");
        }
    }

    private decimal ReadDecimal(int jdbcOrdinal)
    {
        var value = resultSet.getBigDecimal(jdbcOrdinal);
        return value is null
            ? 0m
            : decimal.Parse(value.toPlainString(), CultureInfo.InvariantCulture);
    }

    private DateTime ReadDate(int jdbcOrdinal)
    {
        var value = resultSet.getDate(jdbcOrdinal);
        return value is null
            ? default
            : DateTimeOffset.FromUnixTimeMilliseconds(value.getTime()).DateTime.Date;
    }

    private TimeOnly ReadTime(int jdbcOrdinal)
    {
        var value = resultSet.getTime(jdbcOrdinal);
        return value is null
            ? default
            : TimeOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(value.getTime()).DateTime);
    }

    private DateTime ReadTimestamp(int jdbcOrdinal)
    {
        var value = resultSet.getTimestamp(jdbcOrdinal);
        return value is null
            ? default
            : DateTimeOffset.FromUnixTimeMilliseconds(value.getTime()).DateTime;
    }

    private object? ReadObject(int jdbcOrdinal)
    {
        var value = resultSet.getObject(jdbcOrdinal);
        return value?.ToString();
    }

    private static Column CreateColumn(java.sql.ResultSetMetaData metadata, int jdbcOrdinal)
    {
        var name = metadata.getColumnLabel(jdbcOrdinal);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = metadata.getColumnName(jdbcOrdinal);
        }

        var jdbcType = metadata.getColumnType(jdbcOrdinal);
        var nullable = metadata.isNullable(jdbcOrdinal) != java.sql.ResultSetMetaData.columnNoNulls;
        return new Column(
            name,
            metadata.getColumnTypeName(jdbcOrdinal),
            jdbcType,
            ClrType(jdbcType),
            nullable);
    }

    private static Type ClrType(int jdbcType)
    {
        return jdbcType switch
        {
            java.sql.Types.BIT or java.sql.Types.BOOLEAN => typeof(bool),
            java.sql.Types.TINYINT or java.sql.Types.SMALLINT or java.sql.Types.INTEGER => typeof(int),
            java.sql.Types.BIGINT => typeof(long),
            java.sql.Types.FLOAT or java.sql.Types.REAL or java.sql.Types.DOUBLE => typeof(double),
            java.sql.Types.NUMERIC or java.sql.Types.DECIMAL => typeof(decimal),
            java.sql.Types.DATE => typeof(DateTime),
            java.sql.Types.TIME or java.sql.Types.TIME_WITH_TIMEZONE => typeof(TimeOnly),
            java.sql.Types.TIMESTAMP or java.sql.Types.TIMESTAMP_WITH_TIMEZONE => typeof(DateTime),
            _ => typeof(string)
        };
    }

    private sealed record Column(
        string Name,
        string TypeName,
        int JdbcType,
        Type ClrType,
        bool Nullable);
}
