using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Query.Models;
using QueryTemplate = Loader.Query.Template.Template;
using QueryModel = Loader.Query.Models.Query;

namespace Loader.Query.Resolve;

/// <summary>
/// Резолвит один Query в ResolvedQuery, не меняя source и не строя SQL.
/// </summary>
public sealed class QueryResolver
{
    private readonly ExpressionResolver expressionResolver = new();

    public ResolveResult<ResolvedQuery> Resolve(
        QueryModel query,
        IFunctionResolver functions,
        ExpressionResolutionContext? expressionContext = null)
    {
        var context = new ResolutionContext
        {
            Source = query.Source,
            Functions = functions,
            ExpressionContext = expressionContext ?? ExpressionResolutionContext.Empty,
            Errors = []
        };

        var select = ResolveSelect(query, context);
        var where = query.Where is null ? null : expressionResolver.Resolve(query.Where, context);
        var groupBy = ResolveExpressions(query.GroupBy, context);
        var orderBy = ResolveOrderBy(query, context);

        ValidateLimit(query, context);
        ValidateWhere(where, context);
        ValidateAggregations(query, select, groupBy, context);
        context.Errors.AddRange(context.ExpressionContext.Errors);

        if (context.Errors.Count > 0)
        {
            return ResolveResult<ResolvedQuery>.Failure(context.Errors);
        }

        var outputFields = select.Count == 0
            ? query.Source.Fields
            : select.Select(item => item.OutputField).ToArray();
        return ResolveResult<ResolvedQuery>.Success(new ResolvedQuery
        {
            Source = query.Source,
            Select = select,
            Where = where,
            GroupBy = groupBy,
            OrderBy = orderBy,
            Limit = query.Limit,
            Offset = query.Offset,
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
                    Alias = item.Alias,
                    Template = QueryTemplate.Text(item.Alias),
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

    private static void ValidateWhere(ResolvedExpression? where, ResolutionContext context)
    {
        if (where is null || where.Type.DataType == DataType.Boolean)
        {
            return;
        }

        context.Errors.Add(new LangError
        {
            Span = where.Expression.Span,
            Message = "WHERE expression должен возвращать Boolean."
        });
    }

    private static void ValidateAggregations(
        QueryModel query,
        IReadOnlyList<ResolvedSelectItem> select,
        IReadOnlyList<ResolvedExpression> groupBy,
        ResolutionContext context)
    {
        if (groupBy.Count == 0)
        {
            ValidateSelectWithoutGroupBy(select, context);
            return;
        }

        ValidateGroupByExpressions(groupBy, context);
        ValidateSelectWithGroupBy(query, select, groupBy, context);
    }

    private static void ValidateSelectWithoutGroupBy(
        IReadOnlyList<ResolvedSelectItem> select,
        ResolutionContext context)
    {
        var hasAggregate = select.Any(static item => item.Expression.Type.Aggregated);
        if (!hasAggregate)
        {
            return;
        }

        foreach (var item in select.Where(static item => RequiresGrouping(item.Expression)))
        {
            context.Errors.Add(new LangError
            {
                Span = item.Expression.Expression.Span,
                Message = $"SELECT expression '{item.Alias}' должен быть агрегирован или вынесен в GROUP BY."
            });
        }
    }

    private static void ValidateGroupByExpressions(
        IReadOnlyList<ResolvedExpression> groupBy,
        ResolutionContext context)
    {
        foreach (var expression in groupBy.Where(static expression => expression.Type.Aggregated))
        {
            context.Errors.Add(new LangError
            {
                Span = expression.Expression.Span,
                Message = "GROUP BY не может содержать агрегатные выражения."
            });
        }
    }

    private static void ValidateSelectWithGroupBy(
        QueryModel query,
        IReadOnlyList<ResolvedSelectItem> select,
        IReadOnlyList<ResolvedExpression> groupBy,
        ResolutionContext context)
    {
        if (query.Select.Count == 0)
        {
            context.Errors.Add(new LangError
            {
                Span = query.GroupBy[0].Span,
                Message = "SELECT * нельзя использовать вместе с GROUP BY. Перечислите группируемые и агрегированные поля явно."
            });
            return;
        }

        var groupByHashes = groupBy
            .Select(static expression => expression.Expression.Hash)
            .ToHashSet();

        foreach (var item in select)
        {
            if (!RequiresGrouping(item.Expression) || groupByHashes.Contains(item.Expression.Expression.Hash))
            {
                continue;
            }

            context.Errors.Add(new LangError
            {
                Span = item.Expression.Expression.Span,
                Message = $"SELECT expression '{item.Alias}' должен быть агрегирован или совпадать с выражением из GROUP BY."
            });
        }
    }

    private static bool RequiresGrouping(ResolvedExpression expression)
    {
        return !expression.Type.Aggregated && !expression.Type.IsConstant;
    }

    private static void ValidateLimit(QueryModel query, ResolutionContext context)
    {
        if (query.Limit is not 0)
        {
            return;
        }

        context.Errors.Add(new LangError
        {
            Span = query.LimitSpan ?? new LangSpan(1, 1, 1, 1),
            Message = "LIMIT 0 запрещен. Укажите положительный LIMIT или уберите LIMIT."
        });
    }
}
