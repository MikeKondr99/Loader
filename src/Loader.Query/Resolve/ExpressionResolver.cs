using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Query.Models;

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
        var field = context.Fields.FirstOrDefault(field => field.Alias == name.Value);
        if (field is null)
        {
            context.Errors.Add(new LangError
            {
                Span = name.Span,
                Message = $"Поле '{name.Value}' не найдено"
            });
            return null;
        }

        return new ResolvedExpression
        {
            Expression = name,
            Template = field.Template,
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

        // 2. По типам аргументов ищем конкретную перегрузку функции.
        var signature = new FunctionSignature
        {
            Name = function.Name,
            Kind = function.Kind,
            ArgumentTypes = arguments.Select(argument => argument.Type).ToArray()
        };
        var resolution = context.Functions.Resolve(signature);
        if (resolution is null)
        {
            context.Errors.Add(new LangError
            {
                Span = function.Span,
                Message = $"Функция '{function.Name}' с указанными аргументами не найдена"
            });
            return null;
        }

        var definition = resolution.Function;
        if (!ValidateConstArguments(function, definition, arguments, context))
        {
            return null;
        }

        var resolvedArguments = arguments
            .Zip(resolution.Casts, static (argument, cast) => argument with
            {
                Template = cast.Template,
                Arguments = [argument]
            })
            .ToArray();

        // Собираем потанциально динимические template и returnType
        var returnType = definition.ContextReturnTypeProvider?.Invoke(resolvedArguments, context.ExpressionContext) ??
                definition.ReturnType;
        var template = definition.TemplateProvider?.Invoke(resolvedArguments, context.ExpressionContext) ??
                definition.Template;

        // 3. Собираем resolved node: compiler позже раскроет Template через Arguments.
        return new ResolvedExpression
        {
            Expression = function,
            Template = template,
            Type = new ExprType
            {
                DataType = returnType.DataType,
                CanBeNull = resolution.PropagatesNull,
                Aggregated = resolution.ReturnsAggregated,
                IsConstant = resolution.ReturnsConst
            },
            Arguments = resolvedArguments
        };
    }

    private static bool ValidateConstArguments(
        FuncExpr function,
        FunctionDefinition definition,
        IReadOnlyList<ResolvedExpression> arguments,
        ResolutionContext context)
    {
        for (var i = 0; i < definition.Arguments.Count; i++)
        {
            if (!definition.Arguments[i].IsConstRequired || arguments[i].Type.IsLiteral)
            {
                continue;
            }

            context.Errors.Add(new LangError
            {
                Span = function.Arguments[i].Span,
                Message = $"Функция '{function.Name}' требует, чтобы аргумент {i + 1} был константой"
            });
            return false;
        }

        return true;
    }

    private static ResolvedExpression? AddError(Expr expression, ResolutionContext context, string message)
    {
        context.Errors.Add(new LangError
        {
            Span = expression.Span,
            Message = message
        });
        return null;
    }
}
