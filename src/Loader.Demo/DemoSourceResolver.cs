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

namespace Loader.Demo;

internal static partial class DemoSourceResolver
{
    public static async ValueTask<DemoSource> ResolveAsync(
        LoadStatement load,
        string scriptDirectory,
        CancellationToken cancellationToken)
    {
        var options = new LoadOptionReader(load.Options);
        var provider = options.Provider ?? ProviderFromExtension(load.Source);

        return provider switch
        {
            "csv" => File(
                "csv",
                scriptDirectory,
                load.Source,
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
                scriptDirectory,
                load.Source,
                (source, fileName, token) => new ExcelProvider().OpenReaderAsync(
                    source,
                    new ExcelTableConfig
                    {
                        FileName = fileName,
                        WorksheetName = options.String("sheet"),
                        HasHeader = options.Boolean("header", true)
                    },
                    token)),

            "json" => await JsonAsync(load, scriptDirectory, cancellationToken).ConfigureAwait(false),
            "xml" => await XmlAsync(load, scriptDirectory, options, cancellationToken).ConfigureAwait(false),

            "qvd" => File(
                "qvd",
                scriptDirectory,
                load.Source,
                static (source, fileName, token) => new QvdProvider().OpenReaderAsync(
                    source,
                    new QvdTableConfig { FileName = fileName },
                    token)),

            "postgres" or "postgresql" or "postgre" => Database(
                "postgres",
                load.Source,
                options,
                requiresBuffer: false,
                static (source, config, token) => new PostgresProvider().OpenReaderAsync(source, config, token)),

            "sqlserver" or "mssql" => Database(
                "sqlserver",
                load.Source,
                options,
                requiresBuffer: true,
                static (source, config, token) => new SqlServerProvider().OpenReaderAsync(source, config, token)),

            "oracle" => Database(
                "oracle",
                load.Source,
                options,
                requiresBuffer: true,
                static (source, config, token) => new OracleProvider().OpenReaderAsync(source, config, token)),

            "clickhouse" => Database(
                "clickhouse",
                load.Source,
                options,
                requiresBuffer: false,
                static (source, config, token) => new ClickHouseProvider().OpenReaderAsync(source, config, token)),

            _ => throw new InvalidOperationException($"Провайдер '{provider}' не поддерживается Loader.Demo.")
        };
    }

    private static DemoSource File(
        string kind,
        string scriptDirectory,
        string relativePath,
        Func<IFileSource, string, CancellationToken, ValueTask<DbDataReader>> open)
    {
        var source = new FileSystemSource(scriptDirectory);
        return new DemoSource
        {
            Kind = kind,
            RequiresBuffer = false,
            OpenReaderAsync = token => open(source, relativePath, token)
        };
    }

    private static async ValueTask<DemoSource> JsonAsync(
        LoadStatement load,
        string scriptDirectory,
        CancellationToken cancellationToken)
    {
        var source = new FileSystemSource(scriptDirectory);
        var provider = new JsonProvider();
        var schema = await provider
            .AnalyzeSchemaAsync(source, load.Source, [], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new DemoSource
        {
            Kind = "json",
            RequiresBuffer = false,
            OpenReaderAsync = token => provider.OpenReaderAsync(
                source,
                new JsonTableConfig
                {
                    FileName = load.Source,
                    ArrayPath = [],
                    Schema = schema
                },
                token)
        };
    }

    private static async ValueTask<DemoSource> XmlAsync(
        LoadStatement load,
        string scriptDirectory,
        LoadOptionReader options,
        CancellationToken cancellationToken)
    {
        var tableName = options.String("table") ??
            throw new InvalidOperationException("Для XML-источника требуется опция table='имя-строки'.");
        var source = new FileSystemSource(scriptDirectory);
        var provider = new XmlProvider();
        var schema = await provider
            .AnalyzeSchemaAsync(source, load.Source, tableName, cancellationToken)
            .ConfigureAwait(false);

        return new DemoSource
        {
            Kind = "xml",
            RequiresBuffer = false,
            OpenReaderAsync = token => provider.OpenReaderAsync(
                source,
                new XmlTableConfig
                {
                    FileName = load.Source,
                    TableName = tableName,
                    Schema = schema
                },
                token)
        };
    }

    private static DemoSource Database(
        string kind,
        string connectionString,
        LoadOptionReader options,
        bool requiresBuffer,
        Func<IDatabaseSource, SqlTableConfig, CancellationToken, ValueTask<DbDataReader>> open)
    {
        var table = options.String("table") ??
            throw new InvalidOperationException($"Для провайдера БД '{kind}' требуется опция table='schema.table'.");
        if (!QualifiedTableNameRegex().IsMatch(table))
        {
            throw new InvalidOperationException($"Имя таблицы '{table}' не поддерживается Loader.Demo.");
        }

        var source = new ConnectionStringSource { ConnectionString = connectionString };
        var config = new SqlTableConfig { Sql = $"SELECT * FROM {table}" };
        return new DemoSource
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
                "Нужно указать marker провайдера, если FROM не является поддерживаемым относительным путем к файлу.")
        };
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$]*(\.[A-Za-z_][A-Za-z0-9_$]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedTableNameRegex();
}
