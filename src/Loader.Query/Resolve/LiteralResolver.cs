using System.Globalization;
using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Template;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Resolve;

/// <summary>
/// Превращает literals языка в resolved literals query layer.
/// </summary>
public static class LiteralResolver
{
    public static ResolvedExpression Resolve(Literal literal)
    {
        return literal switch
        {
            IntegerLiteral integer => Create(integer, DataType.Integer, integer.Value.ToString(CultureInfo.InvariantCulture)),
            NumberLiteral number => Create(number, DataType.Number, number.Value.ToString("0.0###############", CultureInfo.InvariantCulture)),
            BooleanLiteral boolean => Create(boolean, DataType.Boolean, boolean.Value ? "true" : "false"),
            StringLiteral text => Create(text, DataType.Text, $"'{text.Value.Replace("'", "''", StringComparison.Ordinal)}'"),
            NullLiteral nullLiteral => new ResolvedExpression
            {
                Expression = nullLiteral,
                Template = QueryTemplate.Text("NULL"),
                Type = new ExprType
                {
                    DataType = DataType.Null,
                    CanBeNull = true,
                    IsConstant = true,
                    IsLiteral = true
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(literal), literal, null)
        };
    }

    private static ResolvedExpression Create(Literal literal, DataType dataType, string sqlText)
    {
        return new ResolvedExpression
        {
            Expression = literal,
            Template = QueryTemplate.Text(sqlText),
            Type = new ExprType
            {
                DataType = dataType,
                IsConstant = true,
                IsLiteral = true
            }
        };
    }
}
