using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Query.Models;
using QueryModel = Loader.Query.Models.Query;

namespace Loader.Query.Resolve;

/// <summary>
/// Резолвит один Query в ResolvedQuery, не меняя source и не строя SQL.
/// </summary>
public sealed class QueryResolver
{
    private readonly ExpressionResolver expressionResolver = new();

    public ResolveResult<ResolvedQuery> Resolve(QueryModel query, IFunctionResolver functions)
    {
        var context = new ResolutionContext
        {
            Source = query.Source,
            Functions = functions,
            Errors = []
        };

        var select = ResolveSelect(query, context);
        var where = query.Where is null ? null : expressionResolver.Resolve(query.Where, context);
        var groupBy = ResolveExpressions(query.GroupBy, context);
        var orderBy = ResolveOrderBy(query, context);

        if (context.Errors.Count > 0)
        {
            return ResolveResult<ResolvedQuery>.Failure(context.Errors);
        }

        var outputFields = select.Select(item => item.OutputField).ToArray();
        return ResolveResult<ResolvedQuery>.Success(new ResolvedQuery
        {
            Source = query.Source,
            Select = select,
            Where = where,
            GroupBy = groupBy,
            OrderBy = orderBy,
            Limit = query.Limit,
            OutputFields = outputFields
        });
    }

    private IReadOnlyList<ResolvedSelectItem> ResolveSelect(QueryModel query, ResolutionContext context)
    {
        var select = new List<ResolvedSelectItem>(query.Select.Count);
        foreach (var item in query.Select)
        {
            var resolvedExpression = expressionResolver.Resolve(item.Expression, context);
            if (resolvedExpression is null)
            {
                continue;
            }

            select.Add(new ResolvedSelectItem
            {
                Alias = item.Alias,
                Expression = resolvedExpression,
                OutputField = new Field
                {
                    Name = item.Alias,
                    Type = new FieldType
                    {
                        DataType = resolvedExpression.Type.DataType,
                        CanBeNull = resolvedExpression.Type.CanBeNull
                    }
                }
            });
        }

        return select;
    }

    private IReadOnlyList<ResolvedExpression> ResolveExpressions(
        IReadOnlyList<Expr> expressions,
        ResolutionContext context)
    {
        var resolved = new List<ResolvedExpression>(expressions.Count);
        foreach (var expression in expressions)
        {
            var resolvedExpression = expressionResolver.Resolve(expression, context);
            if (resolvedExpression is not null)
            {
                resolved.Add(resolvedExpression);
            }
        }

        return resolved;
    }

    private IReadOnlyList<ResolvedOrderItem> ResolveOrderBy(QueryModel query, ResolutionContext context)
    {
        var orderBy = new List<ResolvedOrderItem>(query.OrderBy.Count);
        foreach (var item in query.OrderBy)
        {
            var resolvedExpression = expressionResolver.Resolve(item.Expression, context);
            if (resolvedExpression is not null)
            {
                orderBy.Add(new ResolvedOrderItem
                {
                    Expression = resolvedExpression,
                    Direction = item.Direction
                });
            }
        }

        return orderBy;
    }
}
