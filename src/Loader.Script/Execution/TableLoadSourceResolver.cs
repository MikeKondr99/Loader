using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal sealed class TableLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Table";

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        RejectUnknownOptions(Name, options, errors, ["name"]);
        RejectSqlForFileProvider("table", statement, errors);

        var tableName = options.RequiredString(
            "name",
            statement.SourceCall.Span,
            "Для provider-а Table требуется опция name='table_name'.");
        if (tableName is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        var loadedTable = context.LoadedTables.SingleOrDefault(table => string.Equals(
            table.Alias,
            tableName,
            StringComparison.Ordinal));
        if (loadedTable is null)
        {
            errors.Add(new LangError
            {
                Message = $"Таблица '{tableName}' не найдена среди уже загруженных LOAD таблиц.",
                Span = options.GetOption("name")?.Span ?? statement.SourceCall.Span
            });
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        var source = new ConnectionStringSource { ConnectionString = context.TargetConnectionString };
        var config = new SqlTableConfig
        {
            Sql = BuildSelectSql(loadedTable)
        };
        return ValueTask.FromResult(new LoadProviderSource
        {
            Kind = "table",
            RequiresBuffer = false,
            OpenReaderAsync = async token =>
            {
                // Читаем физическую final table из DWH. В ClickHouse там всегда columnN и CH-типы.
                var reader = await new ClickHouseProvider()
                    .OpenReaderAsync(source, config, token)
                    .ConfigureAwait(false);

                // Возвращаем наружу пользовательские имена и логические типы из LoadedTable.
                // Например Time физически лежит в CH как DateTime, но для следующего LOAD остается Time.
                var renamedReader = reader.RenameColumns(loadedTable.Fields.Select(static field => field.Name).ToArray());
                return new LoadedTableDataReader(renamedReader, loadedTable.Fields);
            }
        });
    }

    private static string BuildSelectSql(LoadedTable table)
    {
        return $"SELECT * FROM {table.Name.ToSql()}";
    }
}
