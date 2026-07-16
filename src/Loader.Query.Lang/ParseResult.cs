namespace Loader.Query.Lang;

/// <summary>
/// Минимальный result без внешних зависимостей ReData/Pattern.
/// </summary>
public readonly record struct ParseResult<T>
{
    private readonly T? _value;
    private readonly ExprError? _error;

    private ParseResult(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private ParseResult(ExprError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Parse result does not contain a value.");

    public ExprError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Parse result does not contain an error.");

    public static ParseResult<T> Success(T value)
    {
        return new ParseResult<T>(value);
    }

    public static ParseResult<T> Failure(ExprError error)
    {
        return new ParseResult<T>(error);
    }

    public bool TryGetValue(out T? value, out ExprError? error)
    {
        value = _value;
        error = _error;
        return IsSuccess;
    }
}
