namespace Loader.Script;

public enum ScriptProgressLevel
{
    User,
    Debug
}

public sealed record ScriptProgressEvent
{
    /// <summary>
    /// Стабильный идентификатор обновляемого сообщения progress.
    /// Если <c>null</c>, принимающая сторона должна добавить событие как новое сообщение.
    /// Если задан, принимающая сторона может обновлять уже показанное сообщение с тем же id.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Машинно-читаемый вид события progress.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Уровень события: пользовательское сообщение или debug-информация.
    /// </summary>
    public ScriptProgressLevel Level { get; init; } = ScriptProgressLevel.User;

    /// <summary>
    /// Пользовательский текст сообщения.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Подробное debug-содержимое, которое принимающая сторона может показывать по требованию.
    /// Например полный SQL-запрос при коротком сообщении в <see cref="Message"/>.
    /// </summary>
    public string? DebugPayload { get; init; }

    /// <summary>
    /// Время создания события в UTC.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
