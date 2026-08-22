namespace Loader.Lang.Statements;

/// <summary>
/// Логический вид результата LOAD, разобранный из синтаксиса script.
/// </summary>
public enum LoadTableKind
{
    /// <summary>
    /// Обычный результат LOAD, который будет виден вызывающему коду после выполнения script.
    /// </summary>
    Normal,

    /// <summary>
    /// Временный результат LOAD, доступный следующим statement-ам и удаляемый после выполнения script.
    /// </summary>
    Temp,

    /// <summary>
    /// Mapping-таблица для lookup-функции Map.
    /// </summary>
    Mapped
}
