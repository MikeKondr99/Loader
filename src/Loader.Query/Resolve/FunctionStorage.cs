using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Template;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Resolve;

/// <summary>
/// ReData-like хранилище функций: ищет перегрузку, применяет null/const/aggregate propagation и готовит casts.
/// </summary>
public sealed class FunctionStorage : IFunctionResolver
{
    private readonly ILookup<string, FunctionDefinition> lookup;
    private readonly ILookup<FunctionArgumentType, FunctionDefinition> implicitCasts;

    public FunctionStorage(IEnumerable<FunctionDefinition> functions)
    {
        var materialized = functions.ToArray();
        lookup = materialized.Where(static function => function.ImplicitCast is null).ToLookup(static function => function.Name);
        implicitCasts = materialized.Where(static function => function.ImplicitCast is not null).ToLookup(static function => function.Arguments[0].Type);
    }

    public FunctionResolution? Resolve(FunctionSignature signature)
    {
        var returnsConst = ConstPropagationValue(signature.ArgumentTypes);
        var returnsAggregated = AggPropagationValue(signature.ArgumentTypes);
        if (returnsAggregated is null)
        {
            return null;
        }

        var matches = new List<FunctionResolution>();
        foreach (var function in GetValidFunctions(signature))
        {
            if (function.ReturnType.Aggregated && returnsAggregated.Value)
            {
                continue;
            }

            var casts = function.Arguments
                .Zip(signature.ArgumentTypes, (argument, type) => GetCast(type, argument.Type))
                .ToArray();
            if (casts.Any(static cast => cast is null))
            {
                continue;
            }

            matches.Add(new FunctionResolution
            {
                Function = function,
                Casts = casts!,
                ReturnsConst = function.ConstPropagation switch
                {
                    ConstPropagation.Default => returnsConst,
                    ConstPropagation.AlwaysTrue => true,
                    ConstPropagation.AlwaysFalse => false,
                    _ => throw new ArgumentOutOfRangeException(nameof(function), function.ConstPropagation, null)
                },
                ReturnsAggregated = function.ReturnType.Aggregated || returnsAggregated.Value,
                PropagatesNull = PropagatesNull(function, signature)
            });
        }

        return matches.MinBy(static match => match.Casts.Sum(static cast => Math.Pow(10, cast.ImplicitCast?.Cost ?? 0)));
    }

    private IEnumerable<FunctionDefinition> GetValidFunctions(FunctionSignature signature)
    {
        return lookup[signature.Name]
            .Where(function => function.Arguments.Count == signature.ArgumentTypes.Count)
            .Where(function => ValidFunctionKind(function.Kind, signature.Kind));
    }

    private static bool ValidFunctionKind(FuncExprKind defined, FuncExprKind used)
    {
        return defined == used || defined == FuncExprKind.Method && used == FuncExprKind.Default;
    }

    private static bool? AggPropagationValue(IEnumerable<ExprType> exprs)
    {
        var types = exprs.ToArray();
        if (types.Any(static type => type.Aggregated))
        {
            return types.All(static type => type.Aggregated || type.IsConstant) ? true : null;
        }

        return false;
    }

    private static bool ConstPropagationValue(IEnumerable<ExprType> types)
    {
        return types.All(static type => type.IsConstant);
    }

    private static bool PropagatesNull(FunctionDefinition function, FunctionSignature signature)
    {
        if (!function.ReturnType.CanBeNull)
        {
            return false;
        }

        if (function.CustomNullPropagation is not null)
        {
            return function.CustomNullPropagation(signature.ArgumentTypes.Select(static type => type.CanBeNull));
        }

        for (var i = 0; i < function.Arguments.Count; i++)
        {
            if (function.Arguments[i].PropagateNull && signature.ArgumentTypes[i].CanBeNull)
            {
                return true;
            }
        }

        return false;
    }

    private FunctionDefinition? GetCast(ExprType from, FunctionArgumentType to)
    {
        var fromType = new FunctionArgumentType
        {
            DataType = from.DataType,
            CanBeNull = from.CanBeNull
        };

        if (Compatible(fromType, to))
        {
            return IdentityCast(fromType, to);
        }

        return implicitCasts[fromType].FirstOrDefault(function =>
            function.ReturnType.DataType == to.DataType && function.ReturnType.CanBeNull == to.CanBeNull);
    }

    private static bool Compatible(FunctionArgumentType from, FunctionArgumentType to)
    {
        if (to.DataType == DataType.Unknown)
        {
            return true;
        }

        if (from.DataType == DataType.Null)
        {
            return to.CanBeNull;
        }

        return from.DataType == to.DataType && (to.CanBeNull || !from.CanBeNull);
    }

    private static FunctionDefinition IdentityCast(FunctionArgumentType from, FunctionArgumentType to)
    {
        return new FunctionDefinition
        {
            Name = string.Empty,
            Arguments =
            [
                new FunctionArgument
                {
                    Name = "input",
                    Type = from,
                    PropagateNull = true
                }
            ],
            ReturnType = new FunctionReturnType
            {
                DataType = to.DataType == DataType.Unknown ? from.DataType : to.DataType,
                CanBeNull = to.CanBeNull || from.CanBeNull
            },
            Kind = FuncExprKind.Default,
            Template = QueryTemplate.FromTokens([new ArgToken(0)]),
            ImplicitCast = new ImplicitCastMetadata
            {
                Cost = 0
            },
            CustomNullPropagation = null,
            ConstPropagation = ConstPropagation.Default
        };
    }
}
