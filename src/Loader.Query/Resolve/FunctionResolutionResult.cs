namespace Loader.Query.Resolve;

/// <summary>
/// Результат выбора перегрузки функции.
/// </summary>
public sealed record FunctionResolutionResult
{
    public FunctionResolution? Resolution { get; init; }

    public FunctionResolutionError? Error { get; init; }

    public static FunctionResolutionResult Success(FunctionResolution resolution)
    {
        return new FunctionResolutionResult
        {
            Resolution = resolution
        };
    }

    public static FunctionResolutionResult Failure(string message)
    {
        return new FunctionResolutionResult
        {
            Error = new FunctionResolutionError
            {
                Message = message
            }
        };
    }
}
