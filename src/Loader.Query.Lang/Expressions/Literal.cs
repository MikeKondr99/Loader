namespace Loader.Query.Lang.Expressions;

public abstract record Literal : Expr;

public abstract record Literal<T>(T Value) : Literal;
