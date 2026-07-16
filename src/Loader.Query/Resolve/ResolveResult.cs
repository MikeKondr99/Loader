using Loader.Lang;

namespace Loader.Query.Resolve;

/// <summary>
/// Результат resolve без исключений: либо value, либо список ошибок.
/// </summary>
public sealed record ResolveResult<T>
{
    public required T? Value { get; init; }

    public required IReadOnlyList<ExprError> Errors { get; init; }

    public bool IsSuccess => Errors.Count == 0 && Value is not null;

    public static ResolveResult<T> Success(T value)
    {
        return new ResolveResult<T>
        {
            Value = value,
            Errors = []
        };
    }

    public static ResolveResult<T> Failure(IReadOnlyList<ExprError> errors)
    {
        return new ResolveResult<T>
        {
            Value = default,
            Errors = errors
        };
    }
}
