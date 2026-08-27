using Loader.Query.Models;
using Loader.Query.Resolve;
using CoreDataType = Loader.Core.Models.DataType;
using QueryDataType = Loader.Query.Models.DataType;

namespace Loader.Script.Execution;

/// <summary>
/// Узкий контекст expression resolver-а поверх <see cref="ScriptContext"/>.
/// Дает функциям доступа только к тем runtime-данным script execution, которые им реально нужны.
/// </summary>
internal sealed class ScriptExpressionResolutionContext : ExpressionResolutionContext
{
    private readonly ScriptContext context;

    public ScriptExpressionResolutionContext(ScriptContext context)
    {
        this.context = context;
    }

    /// <summary>
    /// Ищет загруженную <c>MAPPED LOAD</c> таблицу для функций вроде <c>ApplyMap</c>.
    /// Возвращает физическое имя таблицы и доменные типы key/value.
    /// </summary>
    public override MapTableInfo? GetMap(string name)
    {
        var table = context.LoadedTables.FirstOrDefault(table =>
            table.Kind == LoadedTableKind.Mapped &&
            string.Equals(table.Alias, name, StringComparison.Ordinal));
        if (table is null || table.Fields.Count < 2)
        {
            return null;
        }

        return new MapTableInfo
        {
            Alias = table.Alias ?? name,
            PhysicalTableName = table.Name.Table,
            KeyType = ToQueryFieldType(table.Fields[0]),
            ValueType = ToQueryFieldType(table.Fields[1])
        };
    }

    /// <summary>
    /// Проверяет наличие уже загруженной script-таблицы по alias.
    /// Используется функциями/валидациями, которым достаточно факта существования таблицы.
    /// </summary>
    public override bool HasTable(string name)
    {
        return context.LoadedTables.Any(table => string.Equals(
            table.Alias,
            name,
            StringComparison.Ordinal));
    }

    private static FieldType ToQueryFieldType(LoadedTableField field)
    {
        return new FieldType
        {
            DataType = field.DataType switch
            {
                CoreDataType.Integer => QueryDataType.Integer,
                CoreDataType.Number => QueryDataType.Number,
                CoreDataType.Text => QueryDataType.Text,
                CoreDataType.Boolean => QueryDataType.Boolean,
                CoreDataType.DateTime => QueryDataType.DateTime,
                CoreDataType.Time => QueryDataType.Time,
                _ => QueryDataType.Unknown
            },
            CanBeNull = field.CanBeNull
        };
    }
}
