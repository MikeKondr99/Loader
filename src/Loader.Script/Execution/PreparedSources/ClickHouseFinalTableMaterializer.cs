using ClickHouse.Client.ADO;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script.Execution;

internal sealed class ClickHouseFinalTableMaterializer
{
    public async ValueTask<long> MaterializeAsync(
        ScriptContext context,
        string querySql,
        ClickHouseTableName finalTable,
        LoadClickHouseTableKind kind,
        CancellationToken cancellationToken)
    {
        var source = new ConnectionStringSource
        {
            ConnectionString = context.TargetConnectionString
        };

        await using var schemaReader = await new ClickHouseProvider()
            .OpenReaderAsync(
                source,
                new SqlTableConfig { Sql = ClickHouseFinalTableSql.SchemaProbe(querySql) },
                cancellationToken)
            .ConfigureAwait(false);
        await using var finalNameReader = schemaReader.AbstractColumns();
        await using var finalReader = finalNameReader.Normalize();

        var writeOptions = CreateWriteOptions(finalTable, kind);
        var createSql = new ClickHouseWriter().BuildCreateTableSql(finalReader, writeOptions);
        await ExecuteNonQueryAsync(context, createSql, cancellationToken).ConfigureAwait(false);

        var insertSql = ClickHouseFinalTableSql.InsertSelect(finalTable, finalReader.DataSchema, querySql);
        await ExecuteNonQueryAsync(context, insertSql, cancellationToken).ConfigureAwait(false);

        return await CountRowsAsync(context, finalTable, cancellationToken).ConfigureAwait(false);
    }

    private static ClickHouseWriteOptions CreateWriteOptions(
        ClickHouseTableName tableName,
        LoadClickHouseTableKind kind)
    {
        return new ClickHouseWriteOptions
        {
            TableName = tableName,
            Engine = kind switch
            {
                // Log запрещен в ClickHouse Cloud, поэтому используем минимальный MergeTree без ключа сортировки.
                LoadClickHouseTableKind.Temp => "MergeTree ORDER BY tuple()",

                // Полноценная стратегия ORDER BY для пользовательских final tables пока не определена.
                LoadClickHouseTableKind.Final => "MergeTree ORDER BY tuple()",

                // ApplyMap читает mapping через joinGetOrNull, поэтому нужна ClickHouse Join-таблица.
                LoadClickHouseTableKind.Mapped => "Join(ANY, LEFT, `column1`)",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            }
        };
    }

    private static async ValueTask<long> CountRowsAsync(
        ScriptContext context,
        ClickHouseTableName table,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = ClickHouseFinalTableSql.CountRows(table);
        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(count, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async ValueTask ExecuteNonQueryAsync(
        ScriptContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
