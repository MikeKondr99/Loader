namespace Loader.Script;

/// <summary>
/// Редко меняемые настройки выполнения script, которые задаются принимающей стороной снаружи.
/// </summary>
public sealed record ScriptContextOptions
{
    /// <summary>
    /// Префикс физических временных таблиц, создаваемых для reader-source.
    /// </summary>
    public string TempTablePrefix { get; init; } = "loader_script_temp_";

    /// <summary>
    /// Префикс физических финальных таблиц, создаваемых обычным <c>LOAD</c>.
    /// </summary>
    public string FinalTablePrefix { get; init; } = "loader_script_final_";

    /// <summary>
    /// Как часто отправлять обновляемый progress по количеству строк,
    /// прочитанных из reader-source и записанных во временную таблицу.
    /// <c>null</c> отключает промежуточные обновления, финальное сообщение все равно отправляется.
    /// </summary>
    public TimeSpan? SourceRowsProgressInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Как часто отправлять heartbeat для записи финальной таблицы через ClickHouse <c>INSERT SELECT</c>.
    /// <c>null</c> отключает heartbeat, финальное сообщение с точным временем все равно отправляется.
    /// </summary>
    public TimeSpan? FinalTableWriteHeartbeatInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Отправлять ли debug progress-события. По умолчанию выключено, чтобы принимающая сторона
    /// явно включала потенциально подробные SQL-сообщения.
    /// </summary>
    public bool EmitDebugProgress { get; init; }
}
