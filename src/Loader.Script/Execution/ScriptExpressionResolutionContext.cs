using Loader.Query.Models;
using Loader.Query.Resolve;
using CoreDataType = Loader.Core.Models.DataType;
using QueryDataType = Loader.Query.Models.DataType;

namespace Loader.Script.Execution;

internal sealed class ScriptExpressionResolutionContext : ExpressionResolutionContext
{
    private readonly ScriptContext context;

    public ScriptExpressionResolutionContext(ScriptContext context)
    {
        this.context = context;
    }

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
