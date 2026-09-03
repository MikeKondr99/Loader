using System.Globalization;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Подготавливает часть FROM для LOAD.
/// Для внешних providers создает физическую temp table, а для DWH-native sources возвращает SQL-фрагмент напрямую.
/// </summary>
internal sealed class LoadFromPreparer
{
    private readonly ILoadProviderResolver providerResolver;
    private readonly TempTableMaterializer tempTableMaterializer;

    public LoadFromPreparer(
        ILoadProviderResolver providerResolver,
        TempTableMaterializer tempTableMaterializer)
    {
        this.providerResolver = providerResolver;
        this.tempTableMaterializer = tempTableMaterializer;
    }

    public async ValueTask<PreparedLoadSource> PrepareAsync(
        ScriptContext context,
        LoadStatement statement,
        CancellationToken cancellationToken)
    {
        // 1. Открываем общий span подготовки FROM, чтобы было видно выбранный путь:
        // физическая temp table или прямой SQL-source.
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.Prepare");
        activity?
            .SetTag("load.table_name", statement.TableName)
            .SetTag("load.source_provider", statement.SourceCall.Name);

        // 2. Resolver разбирает конкретный provider и возвращает один из вариантов:
        // reader-source для внешнего чтения или sql-source для чтения напрямую из DWH.
        var source = await providerResolver
            .ResolveAsync(statement, context, cancellationToken)
            .ConfigureAwait(false);

        // 3. Сообщаем пользователю, какой источник начинаем читать.
        // Для SQL-source это событие открытия source-а, даже если чтение произойдет позже внутри final query.
        await ReportSourceReadStartedAsync(context, statement, cancellationToken)
            .ConfigureAwait(false);

        // 4. Выбор пути подготовки остается здесь: resolver только описывает source,
        // а preparer решает, нужна ли физическая temp table.
        var preparedSource = source switch
        {
            SqlLoadFromSource sqlSource => PrepareSqlSource(sqlSource, statement.First),
            ReaderLoadFromSource readerSource => await PrepareReaderSourceAsync(context, statement, readerSource, activity, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new NotSupportedException($"FROM source '{source.GetType().Name}' не поддерживается.")
        };

        activity?
            .SetSanitizedTag("load.prepared_source_sql", preparedSource.Sql)
            .SetTag("load.prepared_source_alias", preparedSource.Alias)
            .SetTag("load.prepared_source_field_count", preparedSource.Fields.Count);

        return preparedSource;
    }

    /// <summary>
    /// Подготавливает source, который уже выражен SQL-ом.
    /// Поля source-а сохраняют связь между доменным именем и физической колонкой внутри SQL.
    /// </summary>
    private static PreparedLoadSource PrepareSqlSource(
        SqlLoadFromSource source,
        long? first)
    {
        var alias = CreateSourceAlias();
        return new PreparedLoadSource(
            ApplyFirst(source.Sql, first),
            alias,
            source.Fields.Select(field => new PreparedLoadSourceField
            {
                Name = field.Name,
                PhysicalName = field.PhysicalName,
                DataType = field.DataType,
                CanBeNull = field.CanBeNull
            }).ToArray());
    }

    /// <summary>
    /// Загружает reader-source в temp table и возвращает подготовленный source поверх этой таблицы.
    /// </summary>
    private async ValueTask<PreparedLoadSource> PrepareReaderSourceAsync(
        ScriptContext context,
        LoadStatement statement,
        ReaderLoadFromSource source,
        System.Diagnostics.Activity? activity,
        CancellationToken cancellationToken)
    {
        var tempTable = await tempTableMaterializer.MaterializeAsync(context, statement, source, cancellationToken)
            .ConfigureAwait(false);
        return PrepareTempTable(tempTable);
    }

    /// <summary>
    /// Подготавливает source, который был физически загружен в temp table.
    /// Владение очисткой temp table передается в <see cref="PreparedLoadSource"/>.
    /// </summary>
    private static PreparedLoadSource PrepareTempTable(TemporaryClickHouseTable tempTable)
    {
        var alias = CreateSourceAlias();
        return new PreparedLoadSource(
            tempTable.TableName.ToSql(),
            alias,
            tempTable.Schema.Fields.Select((field, ordinal) => new PreparedLoadSourceField
            {
                Name = tempTable.OriginalColumnNames[ordinal],
                PhysicalName = field.Name,
                DataType = field.DataType,
                CanBeNull = field.AllowDBNull ?? true
            }).ToArray(),
            tempTable.DisposeAsync);
    }

    /// <summary>
    /// Применяет FIRST к исходным строкам provider-а до LOAD-преобразований.
    /// Для SQL-source это делается внешней оберткой с LIMIT.
    /// </summary>
    private static string ApplyFirst(string sql, long? first)
    {
        if (first is null)
        {
            return sql;
        }

        // Внутренний alias нужен ClickHouse для подзапроса и не должен пересекаться
        // с alias-ом prepared source-а, который будет использован внешним LOAD query.
        var innerAlias = CreateSourceAlias();
        return $"(SELECT * FROM {sql} AS {innerAlias} LIMIT {first.Value.ToString(CultureInfo.InvariantCulture)})";
    }

    /// <summary>
    /// Создает уникальный alias для FROM source-а.
    /// Через него строятся все обращения к physical columns, чтобы не ловить неоднозначные column1/column2.
    /// </summary>
    private static string CreateSourceAlias()
    {
        return $"source_{Guid.NewGuid():N}";
    }

    private static async ValueTask ReportSourceReadStartedAsync(
        ScriptContext context,
        LoadStatement statement,
        CancellationToken cancellationToken)
    {
        var providerName = statement.SourceCall.Name;
        if (string.Equals(providerName, "connect", StringComparison.OrdinalIgnoreCase) ||
            IsDatabaseSourceKind(providerName))
        {
            if (TryGetSourceOption(statement, "name", 0) is { Length: > 0 } connectionName)
            {
                await context.Logger.ConnectionOpeningAsync(connectionName, cancellationToken).ConfigureAwait(false);
            }

            if (statement.SqlPart is not null)
            {
                await context.Logger.SqlSourceReadStartedAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (IsFileSourceKind(providerName) &&
            TryGetSourceOption(statement, "path", 0) is { Length: > 0 } fileName)
        {
            await context.Logger.FileSourceReadStartedAsync(fileName, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsFileSourceKind(string kind)
    {
        return string.Equals(kind, "csv", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "excel", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "xml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "qvd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatabaseSourceKind(string kind)
    {
        return string.Equals(kind, "postgres", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "sqlserver", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "oracle", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "clickhouse", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "hive", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "odbc", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "jdbc", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "ydb", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetSourceOption(LoadStatement statement, string namedOption, int positionalIndex)
    {
        var positional = positionalIndex.ToString(CultureInfo.InvariantCulture);
        foreach (var option in statement.SourceCall.Options)
        {
            if (!string.Equals(option.Name, namedOption, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(option.Name, positional, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return option.Value switch
            {
                Loader.Lang.Expressions.StringLiteral value => value.Value,
                Loader.Lang.Expressions.NameLiteral value => value.Value,
                _ => null
            };
        }

        return null;
    }
}
