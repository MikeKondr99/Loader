using System.Text;
using Loader.Query.Models;

namespace Loader.Query.Compile;

public sealed class ClickHouseQueryCompiler : SqlQueryCompiler
{
    protected override void WriteOutputExpression(StringBuilder builder, ResolvedExpression expression)
    {
        if (expression.Type.DataType != DataType.Boolean)
        {
            base.WriteOutputExpression(builder, expression);
            return;
        }

        var clickHouseType = expression.Type.CanBeNull ? "Nullable(Bool)" : "Bool";
        builder.Append("CAST(");
        ExpressionCompiler.Compile(builder, expression);
        builder
            .Append(" AS ")
            .Append(clickHouseType)
            .Append(')');
    }
}
