using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Functions;

public sealed class ScriptFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Method("ApplyMap")
            .Doc("Возвращает значение из mapping-таблицы по ключу input")
            .Arg("input", DataType.Unknown)
            .ConstArg("map", DataType.Text)
            .Returns((args, context) =>
            {
                var mapName = UnwrapMapName(args);
                var map = context.GetMap(mapName);
                if (map is null)
                {
                    AddMapError(context, mapName, args[1].Expression.Span);
                }
                else
                {
                    ValidateKeyType(context, args[0], map);
                }

                return new FunctionReturnType {
                    DataType = map?.ValueType?.DataType ?? DataType.Unknown,
                    CanBeNull = true
                };
            })
            .CustomNullPropagation(static _ => true)
            .Template((args, context) =>
            {
                var mapName = UnwrapMapName(args);
                var map = context.GetMap(mapName);
                if (map is null)
                {
                    AddMapError(context, mapName, args[1].Expression.Span);
                    return QueryTemplate.Text("NULL");
                }

                ValidateKeyType(context, args[0], map);
                return QueryTemplate.Create($"joinGetOrNull('{EscapeSqlString(map.PhysicalTableName)}', 'column2', {0})");
            });
    }

    private static string UnwrapMapName(IReadOnlyList<ResolvedExpression> args)
    {
        return args[1].Expression switch
        {
            StringLiteral value => value.Value,
            NameExpr value => value.Value,
            _ => args[1].Expression.ToString() ?? string.Empty
        };
    }

    private static string EscapeSqlString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static void AddMapError(
        ExpressionResolutionContext context,
        string mapName,
        LangSpan span)
    {
        context.AddError(new LangError
        {
            Span = span,
            Message = context.HasTable(mapName)
                ? $"Таблица '{mapName}' не является MAPPED LOAD таблицей."
                : $"MAPPED LOAD таблица '{mapName}' не найдена."
        });
    }

    private static void ValidateKeyType(
        ExpressionResolutionContext context,
        ResolvedExpression key,
        MapTableInfo map)
    {
        if (key.Type.DataType == DataType.Null)
        {
            context.AddError(new LangError
            {
                Span = key.Expression.Span,
                Message = "ApplyMap нельзя вызывать с null-ключом без явного типа. Используйте явное приведение, например null.Text().ApplyMap('map')."
            });
            return;
        }

        if (key.Type.DataType is DataType.Unknown ||
            map.KeyType.DataType is DataType.Unknown ||
            key.Type.DataType == map.KeyType.DataType)
        {
            return;
        }

        context.AddError(new LangError
        {
            Span = key.Expression.Span,
            Message = $"ApplyMap key type '{key.Type.DataType}' не совместим с key type '{map.KeyType.DataType}' mapping-таблицы '{map.Alias}'."
        });
    }
}
