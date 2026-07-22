using System.Text;
using Loader.Core.Models;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;
using Loader.Query.Compile;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using QueryDataType = Loader.Query.Models.DataType;
using QueryModel = Loader.Query.Models.Query;

namespace Loader.Demo;

internal static class LoadSelectCompiler
{
    public static CompiledLoadQuery Compile(
        LoadStatement load,
        IReadOnlyList<string> originalNames,
        DataSchema stageSchema,
        string stageTableSql)
    {
        var physicalByOriginal = BuildPhysicalNameMap(originalNames);
        var source = new QuerySource
        {
            Name = "stage",
            Fields = stageSchema.Fields.Select(static field => new Field
            {
                Name = field.Name,
                Type = new FieldType
                {
                    DataType = ToQueryDataType(field.DataType),
                    CanBeNull = field.AllowDBNull ?? true
                }
            }).ToArray()
        };

        // 1. LOAD * разворачиваем по физической схеме; явные expressions переводим с исходных имен на columnN.
        var logicalNames = load.Fields is null
            ? originalNames.ToArray()
            : load.Fields.Select(static field => field.Name).ToArray();
        var select = load.Fields is null
            ? source.Fields.Select(static (field, ordinal) => new SelectItem
            {
                Alias = $"column{ordinal + 1}",
                Expression = new NameExpr(field.Name)
            }).ToArray()
            : load.Fields.Select((field, ordinal) => new SelectItem
            {
                Alias = $"column{ordinal + 1}",
                Expression = RewriteNames(field.Expression, physicalByOriginal)
            }).ToArray();

        // 2. Query resolver проверяет поля и функции и добавляет ClickHouse implicit casts.
        var result = new QueryResolver().Resolve(
            new QueryModel
            {
                Source = source,
                Select = select
            },
            ClickHouseFunctions.CreateResolver());
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                "Не удалось разрешить LOAD:" + Environment.NewLine +
                string.Join(Environment.NewLine, result.Errors.Select(static error => error.Message)));
        }

        // 3. Собираем только SELECT; выполнение и materialization остаются в DemoRunner.
        var builder = new StringBuilder();
        var expressionCompiler = new ExpressionCompiler();
        builder.Append("SELECT ");
        for (var ordinal = 0; ordinal < result.Value!.Select.Count; ordinal++)
        {
            if (ordinal > 0)
            {
                builder.Append(", ");
            }

            expressionCompiler.Compile(builder, result.Value.Select[ordinal].Expression);
            builder.Append(" AS ");
            AppendIdentifier(builder, $"column{ordinal + 1}");
        }

        builder
            .Append(" FROM ")
            .Append(stageTableSql)
            .Append(" AS stage");

        return new CompiledLoadQuery
        {
            Sql = builder.ToString(),
            LogicalNames = logicalNames
        };
    }

    private static IReadOnlyDictionary<string, string> BuildPhysicalNameMap(IReadOnlyList<string> originalNames)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < originalNames.Count; ordinal++)
        {
            if (!result.TryAdd(originalNames[ordinal], $"column{ordinal + 1}"))
            {
                throw new InvalidOperationException(
                    $"Источник содержит повторяющееся имя колонки '{originalNames[ordinal]}'.");
            }
        }

        return result;
    }

    private static Expr RewriteNames(Expr expression, IReadOnlyDictionary<string, string> physicalByOriginal)
    {
        return expression switch
        {
            NameExpr name when physicalByOriginal.TryGetValue(name.Value, out var physicalName) =>
                new NameExpr(physicalName) { Span = name.Span },
            NameExpr name => throw new InvalidOperationException($"Колонка источника '{name.Value}' не найдена."),
            FuncExpr function => function with
            {
                Arguments = function.Arguments
                    .Select(argument => RewriteNames(argument, physicalByOriginal))
                    .ToArray()
            },
            Literal => expression,
            _ => throw new InvalidOperationException($"Выражение '{expression}' не поддерживается Loader.Demo.")
        };
    }

    private static QueryDataType ToQueryDataType(Loader.Core.Models.DataType dataType)
    {
        return dataType switch
        {
            Loader.Core.Models.DataType.Text => QueryDataType.Text,
            Loader.Core.Models.DataType.Integer => QueryDataType.Integer,
            Loader.Core.Models.DataType.Number => QueryDataType.Number,
            Loader.Core.Models.DataType.DateTime => QueryDataType.DateTime,
            Loader.Core.Models.DataType.Date => QueryDataType.Date,
            Loader.Core.Models.DataType.Time => QueryDataType.Time,
            Loader.Core.Models.DataType.Boolean => QueryDataType.Boolean,
            _ => QueryDataType.Unknown
        };
    }

    private static void AppendIdentifier(StringBuilder builder, string value)
    {
        builder.Append('`');
        foreach (var character in value)
        {
            if (character == '`')
            {
                builder.Append("``");
            }
            else
            {
                builder.Append(character);
            }
        }

        builder.Append('`');
    }
}
