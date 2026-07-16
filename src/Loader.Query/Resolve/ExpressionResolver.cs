using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Template;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Resolve;

/// <summary>
/// Резолвит expression tree в типизированное дерево SQL-шаблонов.
/// </summary>
public sealed class ExpressionResolver
{
    public ResolvedExpression? Resolve(Expr expression, ResolutionContext context)
    {
        return expression switch
        {
            Literal literal => LiteralResolver.Resolve(literal),
            NameExpr name => ResolveName(name, context),
            FuncExpr func => ResolveFunction(func, context),
            _ => AddError(expression, context, $"Выражение '{expression}' не поддерживается")
        };
    }

    private static ResolvedExpression? ResolveName(NameExpr name, ResolutionContext context)
    {
        var field = context.Source.Fields.FirstOrDefault(field => field.Name == name.Value);
        if (field is null)
        {
            context.Errors.Add(new ExprError
            {
                Span = name.Span,
                Message = $"Поле '{name.Value}' не найдено"
            });
            return null;
        }

        return new ResolvedExpression
        {
            Expression = name,
            Template = QueryTemplate.Text($"{context.Source.Name}.{field.Name}"),
            Type = new ExprType
            {
                DataType = field.Type.DataType,
                CanBeNull = field.Type.CanBeNull
            }
        };
    }

    private ResolvedExpression? ResolveFunction(FuncExpr function, ResolutionContext context)
    {
        // 1. Рекурсивно резолвим аргументы функции.
        var arguments = new List<ResolvedExpression>(function.Arguments.Count);
        foreach (var argument in function.Arguments)
        {
            var resolvedArgument = Resolve(argument, context);
            if (resolvedArgument is null)
            {
                return null;
            }

            arguments.Add(resolvedArgument);
        }

        // 2. По типам аргументов ищем конкретную реализацию функции.
        var signature = new FunctionSignature
        {
            Name = function.Name,
            Kind = function.Kind,
            ArgumentTypes = arguments.Select(argument => argument.Type).ToArray()
        };
        var definition = context.Functions.Resolve(signature);
        if (definition is null)
        {
            context.Errors.Add(new ExprError
            {
                Span = function.Span,
                Message = $"Функция '{function.Name}' с указанными аргументами не найдена"
            });
            return null;
        }

        // 3. Собираем resolved node: compiler позже раскроет Template через Arguments.
        return new ResolvedExpression
        {
            Expression = function,
            Template = definition.Template,
            Type = new ExprType
            {
                DataType = definition.ReturnType,
                CanBeNull = definition.PropagatesNull && arguments.Any(argument => argument.Type.CanBeNull),
                Aggregated = definition.ReturnsAggregated || arguments.Any(argument => argument.Type.Aggregated),
                IsConstant = definition.ReturnsConstant || arguments.All(argument => argument.Type.IsConstant)
            },
            Arguments = arguments
        };
    }

    private static ResolvedExpression? AddError(Expr expression, ResolutionContext context, string message)
    {
        context.Errors.Add(new ExprError
        {
            Span = expression.Span,
            Message = message
        });
        return null;
    }
}
