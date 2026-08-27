using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Table</c>. Создает SQL-source для чтения уже загруженной script-таблицы из DWH.
/// Параметры:
/// name: Text - alias таблицы, созданной предыдущим LOAD statement.
/// </summary>
internal sealed class TableLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Table";

    public override ValueTask<LoadFromSource> ResolveAsync(
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
            return Error();
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
            return Error();
        }

        return ValueTask.FromResult<LoadFromSource>(new SqlLoadFromSource
        {
            Sql = loadedTable.Name.ToSql(),
            Fields = loadedTable.Fields.Select((field, ordinal) => new LoadFromSqlField
            {
                Name = field.Name,
                PhysicalName = $"column{ordinal + 1}",
                DataType = field.DataType,
                CanBeNull = field.CanBeNull
            }).ToArray()
        });
    }
}
