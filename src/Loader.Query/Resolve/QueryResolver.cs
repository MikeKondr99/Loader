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
        var errors = new List<LangError>();
        var expressionResolutionContext = expressionContext ?? ExpressionResolutionContext.Empty;
        var sourceContext = new ResolutionContext
        {
            Source = query.Source,
            Fields = query.Source.Fields.ToList(),
            Functions = functions,
            ExpressionContext = expressionResolutionContext,
            Errors = errors
        };
        var selectContext = new ResolutionContext
        {
            Source = query.Source,
            Fields = query.Source.Fields.ToList(),
            Functions = functions,
            ExpressionContext = expressionResolutionContext,
            Errors = errors
        };

        var select = ResolveSelect(query, selectContext);
        var where = query.Where is null ? null : expressionResolver.Resolve(query.Where, selectContext);
        var groupBy = ResolveExpressions(query.GroupBy, selectContext);
        var orderBy = ResolveOrderBy(query, selectContext);
        var aggregationState = AggregationValidationState.Create(select, groupBy, orderBy);

        ValidateLimit(query, sourceContext);
        ValidateWhere(where, sourceContext);
        ValidateSelect(query, select, aggregationState, sourceContext);
        ValidateSelectAliases(query, sourceContext);
        ValidateGroupBy(groupBy, sourceContext);
        ValidateOrderBy(orderBy, aggregationState, sourceContext);
        errors.AddRange(expressionResolutionContext.Errors);

        if (errors.Count > 0)
        {
            return ResolveResult<ResolvedQuery>.Failure(errors);
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
        for (var ordinal = 0; ordinal < query.Select.Count; ordinal++)
        {
            var item = query.Select[ordinal];
            var resolvedExpression = expressionResolver.Resolve(item.Expression, context);
            if (resolvedExpression is null)
            {
                continue;
            }

            var selectItem = new ResolvedSelectItem
            {
                Alias = item.Alias,
                ColumnName = $"column{ordinal + 1}",
                Expression = resolvedExpression,
                OutputField = new Field
                {
                    Alias = item.Alias,
                    Template = QueryTemplate.Text(item.Alias),
                    Type = new FieldType
                    {
                        DataType = resolvedExpression.Type.DataType,
                        CanBeNull = resolvedExpression.Type.CanBeNull
                    },
                    Aggregated = resolvedExpression.Type.Aggregated
                }
            };

            select.Add(selectItem);
            AddOrReplaceField(context, item.Alias, resolvedExpression.Type, select.Count);
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
        if (where is null)
        {
            return;
        }

        if (where.Type.Aggregated)
        {
            context.Errors.Add(new LangError
            {
                Span = where.Expression.Span,
                Message = "WHERE не может содержать агрегатные выражения."
            });
        }

        if (where.Type.DataType != DataType.Boolean)
        {
            context.Errors.Add(new LangError
            {
                Span = where.Expression.Span,
                Message = "WHERE expression должен возвращать Boolean."
            });
        }
    }

    private static void ValidateSelect(
        QueryModel query,
        IReadOnlyList<ResolvedSelectItem> select,
        AggregationValidationState aggregationState,
        ResolutionContext context)
    {
        if (!aggregationState.HasGroupBy)
        {
            // Если в запросе есть агрегат без GROUP BY, все SELECT-выражения должны быть агрегатами или константами.
            if (!aggregationState.HasAggregate)
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

            return;
        }

        if (query.Select.Count == 0)
        {
            context.Errors.Add(new LangError
            {
                Span = query.GroupBy[0].Span,
                Message = "SELECT * нельзя использовать вместе с GROUP BY. Перечислите группируемые и агрегированные поля явно."
            });
            return;
        }

        foreach (var item in select)
        {
            // При GROUP BY обычное SELECT-выражение должно совпадать с одной из групп.
            if (!RequiresGrouping(item.Expression) || aggregationState.ContainsGroupByExpression(item.Expression))
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

    private static void ValidateSelectAliases(QueryModel query, ResolutionContext context)
    {
        // Script LOAD обычно проверяет alias до query resolving; этот guard сохраняет прямой Query API однозначным.
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in query.Select)
        {
            if (aliases.Add(item.Alias))
            {
                continue;
            }

            context.Errors.Add(new LangError
            {
                Span = item.Expression.Span,
                Message = $"LOAD select alias '{item.Alias}' дублируется."
            });
        }
    }

    private static void ValidateGroupBy(
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

    private static void ValidateOrderBy(
        IReadOnlyList<ResolvedOrderItem> orderBy,
        AggregationValidationState aggregationState,
        ResolutionContext context)
    {
        if (!aggregationState.HasGroupBy)
        {
            // Агрегат в ORDER BY переводит запрос в aggregation context, значит обычный ORDER BY без GROUP BY недопустим.
            if (!aggregationState.HasAggregate)
            {
                return;
            }

            foreach (var item in orderBy.Where(static item => RequiresGrouping(item.Expression)))
            {
                context.Errors.Add(new LangError
                {
                    Span = item.Expression.Expression.Span,
                    Message = "ORDER BY expression должен быть агрегирован или вынесен в GROUP BY."
                });
            }

            return;
        }

        foreach (var item in orderBy)
        {
            // При GROUP BY сортировать можно по агрегату, константе или выражению, совпадающему с одной из групп.
            if (!RequiresGrouping(item.Expression) || aggregationState.ContainsGroupByExpression(item.Expression))
            {
                continue;
            }

            context.Errors.Add(new LangError
            {
                Span = item.Expression.Expression.Span,
                Message = "ORDER BY expression должен быть агрегирован или совпадать с выражением из GROUP BY."
            });
        }
    }

    private static bool RequiresGrouping(ResolvedExpression expression)
    {
        return !expression.Type.Aggregated && !expression.Type.IsConstant;
    }

    private static void AddOrReplaceField(
        ResolutionContext context,
        string name,
        ExprType type,
        int outputOrdinal)
    {
        var existingIndex = context.Fields.FindIndex(existing => existing.Alias == name);
        if (existingIndex >= 0)
        {
            context.Fields.RemoveAt(existingIndex);
        }

        context.Fields.Insert(0, new Field
        {
            Alias = name,
            Template = QueryTemplate.Text($"`column{outputOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture)}`"),
            Type = new FieldType
            {
                DataType = type.DataType,
                CanBeNull = type.CanBeNull
            },
            Aggregated = type.Aggregated
        });
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

    private sealed class AggregationValidationState
    {
        private readonly HashSet<int> groupByHashes;

        private AggregationValidationState(
            bool hasAggregate,
            bool hasGroupBy,
            HashSet<int> groupByHashes)
        {
            HasAggregate = hasAggregate;
            HasGroupBy = hasGroupBy;
            this.groupByHashes = groupByHashes;
        }

        public bool HasAggregate { get; }

        public bool HasGroupBy { get; }

        public static AggregationValidationState Create(
            IReadOnlyList<ResolvedSelectItem> select,
            IReadOnlyList<ResolvedExpression> groupBy,
            IReadOnlyList<ResolvedOrderItem> orderBy)
        {
            return new AggregationValidationState(
                select.Any(static item => item.Expression.Type.Aggregated) ||
                orderBy.Any(static item => item.Expression.Type.Aggregated),
                groupBy.Count > 0,
                groupBy.Select(static expression => expression.Expression.Hash).ToHashSet());
        }

        public bool ContainsGroupByExpression(ResolvedExpression expression)
        {
            return groupByHashes.Contains(expression.Expression.Hash);
        }
    }
}
