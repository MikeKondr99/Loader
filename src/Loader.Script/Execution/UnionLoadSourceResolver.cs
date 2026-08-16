using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Provider Union объединяет несколько уже загруженных script-таблиц по логическим именам полей.
/// ClickHouse сам не может сделать это корректно для Loader final tables: физически там только columnN,
/// а семантика columnN различается между таблицами. Поэтому resolver строит ручной UNION ALL, где каждая
/// ветка SELECT имеет одинаковый порядок внутренних union_columnN и NULL для отсутствующих логических полей.
/// </summary>
internal sealed class UnionLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Union";

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        RejectNamedOptions(statement, errors);
        RejectSqlForFileProvider("union", statement, errors);

        var tableNames = ResolveTableNames(statement, options, errors);
        if (errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        var tables = ResolveTables(context, tableNames, options, statement, errors);
        if (errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        var unionSql = UnionSqlBuilder.Build(tables);
        var source = new ConnectionStringSource { ConnectionString = context.TargetConnectionString };
        var config = new SqlTableConfig { Sql = unionSql.Sql };

        return ValueTask.FromResult(new LoadProviderSource
        {
            Kind = "union",
            RequiresBuffer = false,
            OpenReaderAsync = async token =>
            {
                var reader = await new ClickHouseProvider()
                    .OpenReaderAsync(source, config, token)
                    .ConfigureAwait(false);

                var renamedReader = reader.RenameColumns(unionSql.Fields.Select(static field => field.Name).ToArray());
                return new LoadedTableDataReader(renamedReader, unionSql.Fields);
            }
        });
    }

    private static void RejectNamedOptions(LoadStatement statement, List<LangError> errors)
    {
        foreach (var option in statement.SourceCall.Options)
        {
            if (int.TryParse(option.Name, out _))
            {
                continue;
            }

            errors.Add(new LangError
            {
                Message = "Provider 'Union' принимает только позиционные имена таблиц: Union(table1, table2).",
                Span = option.Span
            });
        }
    }

    private static IReadOnlyList<string> ResolveTableNames(
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var positionals = options.PositionalOptions();
        if (positionals.Count < 2)
        {
            errors.Add(new LangError
            {
                Message = "Provider 'Union' требует минимум две таблицы: Union(table1, table2).",
                Span = statement.SourceCall.Span
            });
        }

        var names = new List<string>(positionals.Count);
        foreach (var option in positionals)
        {
            if (option.Value is NameLiteral name)
            {
                names.Add(name.Value);
                continue;
            }

            errors.Add(new LangError
            {
                Message = "Provider 'Union' принимает только имена таблиц без кавычек: Union(table1, table2).",
                Span = option.Span
            });
        }

        return names;
    }

    private static IReadOnlyList<LoadedTable> ResolveTables(
        ScriptContext context,
        IReadOnlyList<string> names,
        LoadOptionReader options,
        LoadStatement statement,
        List<LangError> errors)
    {
        var tables = new List<LoadedTable>(names.Count);
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index];
            var table = context.FindLoadedTable(name);
            if (table is null)
            {
                errors.Add(new LangError
                {
                    Message = $"Таблица '{name}' не найдена среди уже загруженных LOAD таблиц.",
                    Span = options.GetOption(index.ToString(System.Globalization.CultureInfo.InvariantCulture))?.Span
                           ?? statement.SourceCall.Span
                });
                continue;
            }

            tables.Add(table);
        }

        return tables;
    }
}
