using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Connect</c>. Создает источник чтения БД через подключение из <see cref="ScriptContext.ConnectionRegistry"/>.
/// Параметры:
/// name: Text - имя подключения в registry.
/// SQL после FROM: Text - запрос, который будет выполнен на стороне подключенной БД.
/// </summary>
internal sealed class ConnectLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Connect";

    private static readonly IReadOnlyDictionary<string, Func<ILoadSourceResolver>> FileResolvers =
        new Dictionary<string, Func<ILoadSourceResolver>>(StringComparer.OrdinalIgnoreCase)
        {
            [".csv"] = static () => new CsvLoadSourceResolver(),
            [".json"] = static () => new JsonLoadSourceResolver(),
            [".jsonl"] = static () => new JsonLoadSourceResolver(),
            [".ndjson"] = static () => new JsonLoadSourceResolver(),
            [".qvd"] = static () => new QvdLoadSourceResolver(),
            [".xlsx"] = static () => new ExcelLoadSourceResolver(),
            [".xls"] = static () => new ExcelLoadSourceResolver(),
            [".xml"] = static () => new XmlLoadSourceResolver()
        };

    public override async ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = options.MapPositionals(Name, ["name"]);
        var nameOption = options.GetOption("name");
        var name = options.RequiredString(
            "name",
            statement.SourceCall.Span,
            "Для Connect требуется опция name='connection_name'.");
        if (name is null || errors.Count > 0)
        {
            return null!;
        }

        var connection = await context.ConnectionRegistry.GetAsync(name, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            var names = await context.ConnectionRegistry.FindNamesAsync(cancellationToken).ConfigureAwait(false);
            var message = NameSuggestion.AppendSuggestion(
                $"Connection '{name}' не найден.",
                name,
                names);
            errors.Add(new LangError
            {
                Message = message,
                Span = nameOption?.Span ?? statement.SourceCall.Span
            });
            return null!;
        }

        return connection switch
        {
            DatabaseScriptConnection database => ResolveDatabaseConnection(statement, options, database, nameOption, errors),
            DwhFileTableScriptConnection dwh => await ResolveDwhFileTableConnectionAsync(statement, context, options, dwh, errors, cancellationToken)
                .ConfigureAwait(false),
            FileStorageScriptConnection fileStorage => await ResolveFileStorageConnectionAsync(statement, context, options, fileStorage, errors, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new NotSupportedException($"Connection '{connection.Name}' имеет неподдерживаемый тип '{connection.GetType().Name}'.")
        };
    }

    private static LoadFromSource ResolveDatabaseConnection(
        LoadStatement statement,
        LoadOptionReader options,
        DatabaseScriptConnection connection,
        LoadOption? nameOption,
        List<LangError> errors)
    {
        RejectUnknownOptions("Connect", options, errors, ["name"]);
        var sql = SourceSql("connect", statement, errors);
        if (sql is null || errors.Count > 0)
        {
            return null!;
        }

        if (!DatabaseLoadProviderFactory.TryGet(connection.Provider, out var factory))
        {
            errors.Add(new LangError
            {
                Message = $"Connection '{connection.Name}' использует неподдерживаемый provider '{connection.Provider}'.",
                Span = nameOption?.Span ?? statement.SourceCall.Span
            });
            return null!;
        }

        return factory.CreateSource(connection.ConnectionString, sql);
    }

    private static async ValueTask<LoadFromSource> ResolveDwhFileTableConnectionAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        DwhFileTableScriptConnection connection,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        RejectUnknownOptions("Connect", options, errors, ["name"]);
        if (statement.SqlPart is not null)
        {
            errors.Add(new LangError
            {
                Message = $"Подключение Файл '{connection.Name}' не поддерживает SQL после FROM.",
                Span = statement.SqlPart.Span
            });
        }

        if (errors.Count > 0)
        {
            return null!;
        }

        var sql = NormalizeDwhSql(connection.Sql);
        return new SqlLoadFromSource
        {
            Sql = sql,
            Fields = await ReadDwhFieldsAsync(context, sql, cancellationToken)
                .ConfigureAwait(false)
        };
    }

    private static async ValueTask<IReadOnlyList<LoadFromSqlField>> ReadDwhFieldsAsync(
        ScriptContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        var schemaSql = $"SELECT * FROM {sql} LIMIT 0";
        await context.DebugSqlAsync("Проверяем схему DWH-подключения", schemaSql, cancellationToken)
            .ConfigureAwait(false);
        await using var rawReader = await new ClickHouseProvider()
            .OpenReaderAsync(
                new ConnectionStringSource
                {
                    ConnectionString = context.TargetConnectionString
                },
                new SqlTableConfig
                {
                    Sql = schemaSql
                },
                cancellationToken)
            .ConfigureAwait(false);
        await using var reader = rawReader.Normalize();

        return reader.DataSchema.Fields.Select(field => new LoadFromSqlField
        {
            Name = field.Name,
            PhysicalName = field.Name,
            DataType = field.DataType,
            CanBeNull = field.AllowDBNull ?? true
        }).ToArray();
    }

    private static async ValueTask<LoadFromSource> ResolveFileStorageConnectionAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        FileStorageScriptConnection connection,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        if (statement.SqlPart is not null)
        {
            errors.Add(new LangError
            {
                Message = $"Подключение Папка '{connection.Name}' не поддерживает SQL после FROM.",
                Span = statement.SqlPart.Span
            });
        }

        var pathOption = options.GetOption("path");
        var path = options.String("path");
        if (path is null)
        {
            errors.Add(new LangError
            {
                Message = $"Подключение Папка '{connection.Name}' требует параметр path='relative/path'.",
                Span = statement.SourceCall.Span
            });
        }

        if (errors.Count > 0)
        {
            return null!;
        }

        var extension = Path.GetExtension(path!);
        if (!FileResolvers.TryGetValue(extension, out var createResolver))
        {
            errors.Add(new LangError
            {
                Message = $"Подключение Папка '{connection.Name}' не поддерживает расширение '{extension}'. Используйте csv, json, jsonl, ndjson, qvd, xlsx, xls или xml.",
                Span = pathOption?.Span ?? statement.SourceCall.Span
            });
            return null!;
        }

        var fileOptions = options.Options
            .Where(static option => !string.Equals(option.Name, "name", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sourceName = SourceName(extension);
        var fileStatement = statement with
        {
            SourceCall = statement.SourceCall with
            {
                Name = sourceName,
                Options = fileOptions
            }
        };
        var fileContext = context with
        {
            FileStorage = connection.Source
        };

        return await createResolver()
            .ResolveAsync(
                fileStatement,
                fileContext,
                new LoadOptionReader(fileOptions, errors),
                errors,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string SourceName(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".csv" => "Csv",
            ".json" or ".jsonl" or ".ndjson" => "Json",
            ".qvd" => "Qvd",
            ".xlsx" or ".xls" => "Excel",
            ".xml" => "Xml",
            _ => extension
        };
    }

    private static string NormalizeDwhSql(string sql)
    {
        var trimmed = sql.Trim();
        return trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase)
            ? $"({trimmed})"
            : trimmed;
    }
}
