using System.Data.Common;
using System.Globalization;
using ClickHouse.Client.ADO;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;
using CoreDataType = Loader.Core.Models.DataType;

namespace Loader.Script.Execution.Calendar;

public class CalendarStatementExecutor
{
    private static readonly DateOnly MinClickHouseDate = new(1970, 1, 1);
    private static readonly DateOnly MaxClickHouseDate = new(2149, 6, 6);

    public string FinalTablePrefix { get; init; } = "loader_script_calendar_";

    public async ValueTask<LoadedTable> ExecuteAsync(
        ScriptContext context,
        CalendarStatement statement,
        CancellationToken cancellationToken = default)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("CalendarStatement.Execute");
        activity?
            .SetTag("calendar.table_name", statement.TableName)
            .SetTag("calendar.range_type", statement.Range.GetType().Name);

        var (startDate, endDate) = await ResolveRangeAsync(context, statement, cancellationToken)
            .ConfigureAwait(false);
        ValidateRange(startDate, endDate);
        activity?
            .SetTag("calendar.start_date", FormatDate(startDate))
            .SetTag("calendar.end_date", FormatDate(endDate));

        await using var finalTable = CreateFinalTable(context);
        var createSql = CalendarSqlBuilder.Build(finalTable.TableName, startDate, endDate);
        await MaterializeWithTelemetryAsync(context, statement, createSql, finalTable.TableName, cancellationToken)
            .ConfigureAwait(false);

        var rowCount = (long)endDate.DayNumber - startDate.DayNumber + 1;
        var loadedTable = CreateLoadedTable(statement, finalTable.TableName, rowCount, startDate, endDate);
        context.AddLoadedTable(loadedTable);
        finalTable.Commit();
        return loadedTable;
    }

    protected virtual async ValueTask<(DateOnly? StartDate, DateOnly? EndDate)> ReadResidentRangeAsync(
        ScriptContext context,
        LoadedTable table,
        string physicalFieldName,
        CancellationToken cancellationToken)
    {
        var identifier = QuoteIdentifier(physicalFieldName);
        var sql = $"""
            SELECT
                countIf(isNotNull({identifier})),
                min(toDate({identifier})),
                max(toDate({identifier}))
            FROM {table.Name.ToSql()}
            """;

        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (null, null);
        }

        var nonNullCount = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
        if (nonNullCount == 0)
        {
            return (null, null);
        }

        return (ReadDate(reader, 1), ReadDate(reader, 2));
    }

    protected virtual async ValueTask MaterializeFinalTableAsync(
        ScriptContext context,
        string createSql,
        ClickHouseTableName finalTable,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = createSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    protected virtual async ValueTask DropFinalTableAsync(
        ScriptContext context,
        ClickHouseTableName finalTable,
        CancellationToken cancellationToken)
    {
        context.Logger.DroppingFinalTable(finalTable.ToSql());
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {finalTable.ToSql()}";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        context.Logger.FinalTableDropped(finalTable.ToSql());
    }

    private async ValueTask<(DateOnly StartDate, DateOnly EndDate)> ResolveRangeAsync(
        ScriptContext context,
        CalendarStatement statement,
        CancellationToken cancellationToken)
    {
        if (statement.Range is CalendarLiteralRange literal)
        {
            return (literal.StartDate, literal.EndDate);
        }

        if (statement.Range is not CalendarResidentRange resident)
        {
            throw new QueryResolutionException(
                $"Тип диапазона CALENDAR '{statement.Range.GetType().Name}' не поддерживается.");
        }

        var table = context.GetLoadedTable(resident.TableName);
        var fieldMatches = table.Fields
            .Select((field, ordinal) => (Field: field, Ordinal: ordinal))
            .Where(item => string.Equals(item.Field.Name, resident.FieldName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (fieldMatches.Length == 0)
        {
            throw new QueryResolutionException(
                $"Поле CALENDAR '{resident.FieldName}' не найдено в таблице RESIDENT '{resident.TableName}'.");
        }

        if (fieldMatches.Length > 1)
        {
            throw new QueryResolutionException(
                $"Поле CALENDAR '{resident.FieldName}' в таблице RESIDENT '{resident.TableName}' неоднозначно.");
        }

        var field = fieldMatches[0];
        if (field.Field.DataType is not CoreDataType.Date and not CoreDataType.DateTime)
        {
            throw new QueryResolutionException(
                $"Поле CALENDAR '{resident.FieldName}' должно иметь тип Date или DateTime, " +
                $"но имеет тип {field.Field.DataType}.");
        }

        context.Logger.ResolvingCalendarResidentRange(resident.TableName, resident.FieldName);
        (DateOnly? StartDate, DateOnly? EndDate) range;
        try
        {
            range = await ReadResidentRangeAsync(
                    context,
                    table,
                    PhysicalFieldName(field.Ordinal),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LoadScriptStageException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new QueryResolutionException(
                $"Не удалось вычислить диапазон CALENDAR из поля '{resident.FieldName}' " +
                $"таблицы '{resident.TableName}'.",
                innerException: exception);
        }

        if (range.StartDate is null || range.EndDate is null)
        {
            throw new QueryResolutionException(
                $"Поле CALENDAR '{resident.FieldName}' таблицы RESIDENT '{resident.TableName}' " +
                "не содержит ни одной даты.");
        }

        return (range.StartDate.Value, range.EndDate.Value);
    }

    private async ValueTask MaterializeWithTelemetryAsync(
        ScriptContext context,
        CalendarStatement statement,
        string createSql,
        ClickHouseTableName finalTable,
        CancellationToken cancellationToken)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("CalendarStatement.FinalTableWrite");
        activity?
            .SetTag("calendar.table_name", statement.TableName)
            .SetTag("calendar.final_table", finalTable.Table);
        context.Logger.MaterializingCalendar(finalTable.ToSql());

        try
        {
            await MaterializeFinalTableAsync(context, createSql, finalTable, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FinalTableWriteException(
                $"Не удалось материализовать CALENDAR в таблицу '{finalTable.ToSql()}'.",
                exception);
        }

        context.Logger.CalendarMaterialized(finalTable.ToSql());
    }

    private static void ValidateRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new QueryResolutionException(
                $"Начальная дата CALENDAR {FormatDate(startDate)} позже конечной даты {FormatDate(endDate)}.");
        }

        if (startDate < MinClickHouseDate || endDate > MaxClickHouseDate)
        {
            throw new QueryResolutionException(
                $"Диапазон CALENDAR должен находиться в диапазоне ClickHouse Date " +
                $"{FormatDate(MinClickHouseDate)}–{FormatDate(MaxClickHouseDate)}.");
        }
    }

    private FinalClickHouseTable CreateFinalTable(ScriptContext context)
    {
        var finalTable = new ClickHouseTableName
        {
            Table = $"{FinalTablePrefix}{Guid.NewGuid():N}"
        };
        return new FinalClickHouseTable(
            finalTable,
            () => DropFinalTableBestEffortAsync(context, finalTable));
    }

    private async ValueTask DropFinalTableBestEffortAsync(
        ScriptContext context,
        ClickHouseTableName finalTable)
    {
        try
        {
            await DropFinalTableAsync(context, finalTable, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Logger.FinalTableDropFailed(finalTable.ToSql(), exception);
        }
    }

    private static LoadedTable CreateLoadedTable(
        CalendarStatement statement,
        ClickHouseTableName finalTable,
        long rowCount,
        DateOnly startDate,
        DateOnly endDate)
    {
        return new LoadedTable
        {
            Name = finalTable,
            Alias = statement.TableName,
            RowCount = rowCount,
            Fields = CalendarFieldDefinitions.All.Select((definition, ordinal) => new LoadedTableField
            {
                Name = definition.Name,
                DataType = definition.DataType,
                CanBeNull = false,
                Cardinality = ordinal == 0 ? rowCount : null,
                Density = rowCount,
                Min = ordinal == 0 ? startDate : null,
                Max = ordinal == 0 ? endDate : null
            }).ToList()
        };
    }

    private static DateOnly ReadDate(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture))
        };
    }

    private static string PhysicalFieldName(int ordinal)
    {
        return $"column{ordinal + 1}";
    }

    private static string QuoteIdentifier(string value)
    {
        return $"`{value.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
