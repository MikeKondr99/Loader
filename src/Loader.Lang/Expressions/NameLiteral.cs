namespace Loader.Lang.Expressions;

/// <summary>
/// Идентификатор, переданный как значение option в provider call.
/// Это не <see cref="NameExpr"/>: значение не читается из строки данных и не вычисляется как выражение.
/// Resolver provider-а сам решает, где такие имена допустимы и что они означают.
/// Пример: <c>Calendar(table=orders, field=CreatedAt)</c>, где <c>orders</c> и <c>CreatedAt</c>
/// являются именами table/field в script context, а не строковыми литералами и не полями текущего reader-а.
/// </summary>
public sealed record NameLiteral(string Value) : Literal<string>(Value)
{
    public NameLiteral(string value, LangSpan span)
        : this(value)
    {
        Span = span;
    }

    public override string ToString()
    {
        return $"[{Value}]";
    }
}
