using System.Data.Common;
using System.Text.RegularExpressions;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Csv;
using Loader.Core.Providers.Excel;
using Loader.Core.Providers.Hive;
using Loader.Core.Providers.Json;
using Loader.Core.Providers.Odbc;
using Loader.Core.Providers.Oracle;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Qvd;
using Loader.Core.Providers.Sql;
using Loader.Core.Providers.SqlServer;
using Loader.Core.Providers.Xml;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

public sealed partial class LoadProviderResolver : ILoadProviderResolver
{
    public async ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<LangError>();
        var options = new LoadOptionReader(statement.Options, errors);
        var provider = ResolveProvider(statement, options, errors);

        var source = provider switch
        {
            "csv" => Csv(statement, context, options),
            "excel" or "xlsx" or "xls" or "xlsb" => Excel(statement, context, options),
            "json" => errors.Count == 0
                ? await JsonAsync(statement, context, options, errors, cancellationToken).ConfigureAwait(false)
                : null,
            "xml" => await XmlAsync(statement, context, options, errors, cancellationToken).ConfigureAwait(false),
            "qvd" => Qvd(statement, context),
            "postgres" or "postgresql" or "postgre" => Database(
                "postgres",
                statement,
                options,
                errors,
                requiresBuffer: false,
                static (source, config, token) => new PostgresProvider().OpenReaderAsync(source, config, token)),
            "sqlserver" or "mssql" => Database(
                "sqlserver",
                statement,
                options,
                errors,
                requiresBuffer: true,
                static (source, config, token) => new SqlServerProvider().OpenReaderAsync(source, config, token)),
            "oracle" => Database(
                "oracle",
                statement,
                options,
                errors,
                requiresBuffer: true,
                static (source, config, token) => new OracleProvider().OpenReaderAsync(source, config, token)),
            "hive" or "apachehive" or "apache-hive" => Database(
                "hive",
                statement,
                options,
                errors,
                requiresBuffer: true,
                static (source, config, token) => new HiveProvider().OpenReaderAsync(source, config, token)),
            "odbc" => Odbc(statement, options, errors),
            "clickhouse" => Database(
                "clickhouse",
                statement,
                options,
                errors,
                requiresBuffer: false,
                static (source, config, token) => new ClickHouseProvider().OpenReaderAsync(source, config, token)),
            _ => null
        };

        if (errors.Count > 0)
        {
            throw new ProviderResolutionException(errors);
        }

        return source!;
    }

    private static LoadProviderSource Csv(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options)
    {
        var delimiter = options.Character("delimiter", ',');
        var hasHeader = options.Boolean("header", true);

        return File(
            "csv",
            context.FileStorage,
            statement.Source,
            (source, fileName, token) => new CsvProvider().OpenReaderAsync(
                source,
                new CsvTableConfig
                {
                    FileName = fileName,
                    Delimiter = delimiter,
                    HasHeader = hasHeader
                },
                token));
    }

    private static LoadProviderSource Excel(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options)
    {
        var sheet = options.String("sheet");
        var hasHeader = options.Boolean("header", true);

        return File(
            "excel",
            context.FileStorage,
            statement.Source,
            (source, fileName, token) => new ExcelProvider().OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = sheet,
                    HasHeader = hasHeader
                },
                token));
    }

    private static LoadProviderSource Qvd(LoadStatement statement, ScriptContext context)
    {
        return File(
            "qvd",
            context.FileStorage,
            statement.Source,
            static (source, fileName, token) => new QvdProvider().OpenReaderAsync(
                source,
                new QvdTableConfig { FileName = fileName },
                token));
    }

    private static LoadProviderSource File(
        string kind,
        IFileSource source,
        string fileName,
        Func<IFileSource, string, CancellationToken, ValueTask<DbDataReader>> open)
    {
        return new LoadProviderSource
        {
            Kind = kind,
            RequiresBuffer = false,
            OpenReaderAsync = token => open(source, fileName, token)
        };
    }

    private static async ValueTask<LoadProviderSource> JsonAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        var arrayPath = JsonRootPath(options, errors);
        if (errors.Count > 0)
        {
            return null!;
        }

        var provider = new JsonProvider();
        var schema = await provider
            .AnalyzeSchemaAsync(context.FileStorage, statement.Source, arrayPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new LoadProviderSource
        {
            Kind = "json",
            RequiresBuffer = false,
            OpenReaderAsync = token => provider.OpenReaderAsync(
                context.FileStorage,
                new JsonTableConfig
                {
                    FileName = statement.Source,
                    ArrayPath = arrayPath,
                    Schema = schema
                },
                token)
        };
    }

    private static IReadOnlyList<string> JsonRootPath(
        LoadOptionReader options,
        List<LangError> errors)
    {
        var root = options.String("root");
        if (root is null)
        {
            return [];
        }

        var path = root
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (path.Length > 0)
        {
            return path;
        }

        errors.Add(new LangError
        {
            Message = "Опция 'root' должна указывать путь к JSON-массиву.",
            Span = options.GetOption("root")?.Span ?? new LangSpan(1, 1, 1, 1)
        });
        return [];
    }

    private static async ValueTask<LoadProviderSource> XmlAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        var tableName = options.RequiredString(
            "table",
            statement.FromSpan,
            "Для XML-источника требуется опция table='имя-строки'.");
        if (tableName is null || errors.Count > 0)
        {
            return null!;
        }

        var provider = new XmlProvider();
        var schema = await provider
            .AnalyzeSchemaAsync(context.FileStorage, statement.Source, tableName, cancellationToken)
            .ConfigureAwait(false);

        return new LoadProviderSource
        {
            Kind = "xml",
            RequiresBuffer = false,
            OpenReaderAsync = token => provider.OpenReaderAsync(
                context.FileStorage,
                new XmlTableConfig
                {
                    FileName = statement.Source,
                    TableName = tableName,
                    Schema = schema
                },
                token)
        };
    }

    private static LoadProviderSource Database(
        string kind,
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors,
        bool requiresBuffer,
        Func<IDatabaseSource, SqlTableConfig, CancellationToken, ValueTask<DbDataReader>> open)
    {
        var table = options.RequiredString(
            "table",
            statement.FromSpan,
            $"Для provider-а БД '{kind}' требуется опция table='schema.table'.");
        if (table is not null && !QualifiedTableNameRegex().IsMatch(table))
        {
            errors.Add(new LangError
            {
                Message = $"Имя таблицы '{table}' не поддерживается.",
                Span = options.GetOption("table")?.Span ?? statement.FromSpan
            });
        }

        if (errors.Count > 0)
        {
            return null!;
        }

        var source = new ConnectionStringSource { ConnectionString = statement.Source };
        var config = new SqlTableConfig { Sql = $"SELECT * FROM {table}" };
        return new LoadProviderSource
        {
            Kind = kind,
            RequiresBuffer = requiresBuffer,
            OpenReaderAsync = token => open(source, config, token)
        };
    }

    private static LoadProviderSource Odbc(
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var table = options.String("table");
        var sql = options.String("sql");
        var tableOption = options.GetOption("table");
        var sqlOption = options.GetOption("sql");

        if (tableOption is null && sqlOption is null)
        {
            errors.Add(new LangError
            {
                Message = "Для provider-а БД 'odbc' требуется опция table='schema.table' или sql='select ...'.",
                Span = statement.FromSpan
            });
        }

        if (tableOption is not null && sqlOption is not null)
        {
            errors.Add(new LangError
            {
                Message = "Для provider-а БД 'odbc' нельзя указывать table и sql одновременно.",
                Span = sqlOption.Span
            });
        }

        if (table is not null && !QualifiedTableNameRegex().IsMatch(table))
        {
            errors.Add(new LangError
            {
                Message = $"Имя таблицы '{table}' не поддерживается.",
                Span = tableOption?.Span ?? statement.FromSpan
            });
        }

        if (sql is not null && string.IsNullOrWhiteSpace(sql))
        {
            errors.Add(new LangError
            {
                Message = "Опция 'sql' для provider-а БД 'odbc' не должна быть пустой.",
                Span = sqlOption?.Span ?? statement.FromSpan
            });
        }

        if (errors.Count > 0)
        {
            return null!;
        }

        var source = new ConnectionStringSource { ConnectionString = statement.Source };
        var config = new SqlTableConfig { Sql = sql ?? $"SELECT * FROM {table}" };
        return new LoadProviderSource
        {
            Kind = "odbc",
            RequiresBuffer = true,
            OpenReaderAsync = token => new OdbcProvider().OpenReaderAsync(source, config, token)
        };
    }

    private static string? ResolveProvider(
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var markers = options.Markers;
        if (markers.Count > 1)
        {
            var first = markers[0];
            foreach (var marker in markers.Skip(1))
            {
                errors.Add(new LangError
                {
                    Message = $"Provider marker '{marker.Name}' нельзя указывать вместе с '{first.Name}'.",
                    Span = marker.Span
                });
            }
        }

        var provider = options.Provider;
        if (provider is null)
        {
            return ProviderFromExtension(statement, errors);
        }

        if (!IsSupportedProvider(provider))
        {
            errors.Add(new LangError
            {
                Message = $"Provider '{provider}' не поддерживается.",
                Span = markers[0].Span
            });
        }

        return provider;
    }

    private static string? ProviderFromExtension(LoadStatement statement, List<LangError> errors)
    {
        var provider = Path.GetExtension(statement.Source).ToLowerInvariant() switch
        {
            ".csv" => "csv",
            ".xlsx" => "xlsx",
            ".xls" => "xls",
            ".xlsb" => "xlsb",
            ".json" => "json",
            ".xml" => "xml",
            ".qvd" => "qvd",
            _ => null
        };

        if (provider is null)
        {
            errors.Add(new LangError
            {
                Message = "Нужно указать provider marker, если FROM не является поддерживаемым относительным путем к файлу.",
                Span = statement.FromSpan
            });
        }

        return provider;
    }

    private static bool IsSupportedProvider(string provider)
    {
        return provider is
            "csv" or
            "excel" or
            "xlsx" or
            "xls" or
            "xlsb" or
            "json" or
            "xml" or
            "qvd" or
            "postgres" or
            "postgresql" or
            "postgre" or
            "sqlserver" or
            "mssql" or
            "oracle" or
            "hive" or
            "apachehive" or
            "apache-hive" or
            "odbc" or
            "clickhouse";
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$]*(\.[A-Za-z_][A-Za-z0-9_$]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedTableNameRegex();
}
