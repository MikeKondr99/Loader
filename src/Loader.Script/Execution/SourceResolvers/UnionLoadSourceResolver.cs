using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Union</c>. Создает SQL-source объединения нескольких уже загруженных script-таблиц по логическим именам полей.
/// Параметры:
/// table1, table2, ...: Name - позиционные имена таблиц; требуется минимум две таблицы.
/// Поведение: resolver строит UNION ALL с одинаковым порядком внутренних union_columnN и NULL для отсутствующих логических полей.
/// </summary>
internal sealed class UnionLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Union";

    public override ValueTask<LoadFromSource> ResolveAsync(
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
            return Error();
        }

        var tables = ResolveTables(context, tableNames, options, statement, errors);
        if (errors.Count > 0)
        {
            return Error();
        }

        var unionSql = UnionSqlBuilder.Build(tables);
        return ValueTask.FromResult<LoadFromSource>(new SqlLoadFromSource
        {
            Sql = $"({unionSql.Sql})",
            Fields = unionSql.Fields.Select((field, ordinal) => new LoadFromSqlField
            {
                Name = field.Name,
                PhysicalName = $"union_column{ordinal + 1}",
                DataType = field.DataType,
                CanBeNull = field.CanBeNull
            }).ToArray()
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
