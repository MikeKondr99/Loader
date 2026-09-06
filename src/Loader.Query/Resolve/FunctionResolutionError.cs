namespace Loader.Query.Resolve;

/// <summary>
/// Ошибка выбора перегрузки функции с готовым сообщением для пользователя.
/// </summary>
public sealed record FunctionResolutionError
{
    public required string Message { get; init; }
}
