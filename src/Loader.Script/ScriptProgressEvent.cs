namespace Loader.Script;

public enum ScriptProgressLevel
{
    User,
    Debug
}

public sealed record ScriptProgressEvent
{
    public required string Kind { get; init; }

    public ScriptProgressLevel Level { get; init; } = ScriptProgressLevel.User;

    public required string Message { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
