using System.Data.Common;
using System.Text.RegularExpressions;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Csv;
using Loader.Core.Providers.Excel;
using Loader.Core.Providers.Json;
using Loader.Core.Providers.Oracle;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Qvd;
using Loader.Core.Providers.Sql;
using Loader.Core.Providers.SqlServer;
using Loader.Core.Providers.Xml;
using Loader.Core.Sources;
using Loader.Lang.Statements;

namespace Loader.Script;

public sealed partial class LoadProviderResolver : ILoadProviderResolver
{
    public async ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var options = new LoadOptionReader(statement.Options);
        var provider = options.Provider ?? ProviderFromExtension(statement.Source);

        return provider switch
        {
            "csv" => File(
                "csv",
                context.FileStorage,
                statement.Source,
                (source, fileName, token) => new CsvProvider().OpenReaderAsync(
                    source,
                    new CsvTableConfig
                    {
                        FileName = fileName,
                        Delimiter = options.Character("delimiter", ','),
                        HasHeader = options.Boolean("header", true)
                    },
                    token)),

            "excel" or "xlsx" or "xls" or "xlsb" => File(
                "excel",
                context.FileStorage,
                statement.Source,
                (source, fileName, token) => new ExcelProvider().OpenReaderAsync(
                    source,
                    new ExcelTableConfig
                    {
                        FileName = fileName,
                        WorksheetName = options.String("sheet"),
                        HasHeader = options.Boolean("header", true)
                    },
                    token)),

            "json" => await JsonAsync(statement, context, cancellationToken).ConfigureAwait(false),
            "xml" => await XmlAsync(statement, context, options, cancellationToken).ConfigureAwait(false),

            "qvd" => File(
                "qvd",
                context.FileStorage,
                statement.Source,
                static (source, fileName, token) => new QvdProvider().OpenReaderAsync(
                    source,
                    new QvdTableConfig { FileName = fileName },
                    token)),

            "postgres" or "postgresql" or "postgre" => Database(
                "postgres",
                statement.Source,
                options,
                requiresBuffer: false,
                static (source, config, token) => new PostgresProvider().OpenReaderAsync(source, config, token)),

            "sqlserver" or "mssql" => Database(
                "sqlserver",
                statement.Source,
                options,
                requiresBuffer: true,
                static (source, config, token) => new SqlServerProvider().OpenReaderAsync(source, config, token)),

            "oracle" => Database(
                "oracle",
                statement.Source,
                options,
                requiresBuffer: true,
                static (source, config, token) => new OracleProvider().OpenReaderAsync(source, config, token)),

            "clickhouse" => Database(
                "clickhouse",
                statement.Source,
                options,
                requiresBuffer: false,
                static (source, config, token) => new ClickHouseProvider().OpenReaderAsync(source, config, token)),

            _ => throw new InvalidOperationException($"Provider '{provider}' не поддерживается.")
        };
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
        CancellationToken cancellationToken)
    {
        var provider = new JsonProvider();
        var schema = await provider
            .AnalyzeSchemaAsync(context.FileStorage, statement.Source, [], cancellationToken: cancellationToken)
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
                    ArrayPath = [],
                    Schema = schema
                },
                token)
        };
    }

    private static async ValueTask<LoadProviderSource> XmlAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        CancellationToken cancellationToken)
    {
        var tableName = options.String("table") ??
            throw new InvalidOperationException("Для XML-источника требуется опция table='имя-строки'.");
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
        string connectionString,
        LoadOptionReader options,
        bool requiresBuffer,
        Func<IDatabaseSource, SqlTableConfig, CancellationToken, ValueTask<DbDataReader>> open)
    {
        var table = options.String("table") ??
            throw new InvalidOperationException($"Для provider-а БД '{kind}' требуется опция table='schema.table'.");
        if (!QualifiedTableNameRegex().IsMatch(table))
        {
            throw new InvalidOperationException($"Имя таблицы '{table}' не поддерживается.");
        }

        var source = new ConnectionStringSource { ConnectionString = connectionString };
        var config = new SqlTableConfig { Sql = $"SELECT * FROM {table}" };
        return new LoadProviderSource
        {
            Kind = kind,
            RequiresBuffer = requiresBuffer,
            OpenReaderAsync = token => open(source, config, token)
        };
    }

    private static string ProviderFromExtension(string source)
    {
        return Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".csv" => "csv",
            ".xlsx" => "xlsx",
            ".xls" => "xls",
            ".xlsb" => "xlsb",
            ".json" => "json",
            ".xml" => "xml",
            ".qvd" => "qvd",
            _ => throw new InvalidOperationException(
                "Нужно указать provider marker, если FROM не является поддерживаемым относительным путем к файлу.")
        };
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$]*(\.[A-Za-z_][A-Za-z0-9_$]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedTableNameRegex();
}
