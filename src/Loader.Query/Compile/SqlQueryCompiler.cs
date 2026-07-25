using System.Globalization;
using System.Text;
using Loader.Query.Models;

namespace Loader.Query.Compile;

public abstract class SqlQueryCompiler : IQueryCompiler
{
    public required IExpressionCompiler ExpressionCompiler { protected get; init; }

    public string Compile(ResolvedQuery query)
    {
        var builder = new StringBuilder();
        WriteQuery(builder, query);
        return builder.ToString().TrimEnd();
    }

    protected virtual void WriteQuery(StringBuilder builder, ResolvedQuery query)
    {
        WriteSelect(builder, query);
        WriteWhere(builder, query);
        WriteGroupBy(builder, query);
        WriteOrderBy(builder, query);
        WriteLimitOffset(builder, query);
    }

    protected virtual void WriteSelect(StringBuilder builder, ResolvedQuery query)
    {
        if (query.Select.Count == 0)
        {
            builder.Append("SELECT * ");
        }
        else
        {
            builder.Append("SELECT").AppendLine();
            WriteSelectTransformations(builder, query);
        }

        WriteFrom(builder, query);
    }

    protected virtual void WriteFrom(StringBuilder builder, ResolvedQuery query)
    {
        builder
            .Append("FROM ")
            .Append(query.Source.Sql);

        builder
            .Append(" AS ")
            .Append(query.Source.Alias);

        builder.AppendLine();
    }

    protected virtual void WriteSelectTransformations(StringBuilder builder, ResolvedQuery query)
    {
        var last = query.Select.Count - 1;
        for (var i = 0; i < query.Select.Count; i++)
        {
            var field = query.Select[i];

            builder.Append("    ");
            ExpressionCompiler.Compile(builder, field.Expression);
            builder.Append(" AS ");
            WriteIdentifier(builder, field.Alias);

            if (i != last)
            {
                builder.Append(", ");
            }

            builder.AppendLine();
        }
    }

    protected virtual void WriteWhere(StringBuilder builder, ResolvedQuery query)
    {
        if (query.Where is null)
        {
            return;
        }

        builder.Append("WHERE ");
        ExpressionCompiler.Compile(builder, query.Where);
        builder.AppendLine();
    }

    protected virtual void WriteGroupBy(StringBuilder builder, ResolvedQuery query)
    {
        if (query.GroupBy.Count == 0)
        {
            return;
        }

        builder.Append("GROUP BY ");
        for (var i = 0; i < query.GroupBy.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            ExpressionCompiler.Compile(builder, query.GroupBy[i]);
        }

        builder.AppendLine();
    }

    protected virtual void WriteOrderBy(StringBuilder builder, ResolvedQuery query)
    {
        if (query.OrderBy.Count == 0)
        {
            return;
        }

        builder.Append("ORDER BY ");
        for (var i = 0; i < query.OrderBy.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var order = query.OrderBy[i];
            ExpressionCompiler.Compile(builder, order.Expression);
            builder.Append(' ');
            builder.Append(order.Direction switch
            {
                OrderDirection.Desc => "DESC",
                OrderDirection.Asc => "ASC",
                var unknown => throw new ArgumentOutOfRangeException(nameof(order.Direction), unknown, null)
            });
        }

        builder.AppendLine();
    }

    protected virtual void WriteLimitOffset(StringBuilder builder, ResolvedQuery query)
    {
        if (query.Limit > 0)
        {
            builder
                .Append("LIMIT ")
                .Append(query.Limit.Value.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        if (query.Offset > 0)
        {
            builder
                .Append("OFFSET ")
                .Append(query.Offset.Value.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }
    }

    protected virtual void WriteIdentifier(StringBuilder builder, string value)
    {
        builder.Append('`');
        foreach (var character in value)
        {
            if (character == '`')
            {
                builder.Append("``");
                continue;
            }

            builder.Append(character);
        }

        builder.Append('`');
    }
}
