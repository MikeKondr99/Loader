namespace Loader.Script;

/// <summary>
/// Редко меняемые настройки выполнения script, которые задаются принимающей стороной снаружи.
/// </summary>
public sealed record ScriptContextOptions
{
    public string TempTablePrefix { get; init; } = "loader_script_temp_";

    public string FinalTablePrefix { get; init; } = "loader_script_final_";

    public int? SourceRowLimit { get; init; }
}
