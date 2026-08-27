namespace Loader.Script.Execution;

/// <summary>
/// Назначение физической ClickHouse-таблицы, которую создает LOAD pipeline.
/// От назначения зависит engine и будущие write-настройки.
/// </summary>
internal enum LoadClickHouseTableKind
{
    /// <summary>
    /// Временная stage-таблица с сырыми данными source перед LOAD-преобразованиями.
    /// </summary>
    Temp,

    /// <summary>
    /// Обычная таблица результата LOAD, доступная следующим statement-ам.
    /// </summary>
    Final,

    /// <summary>
    /// Mapping-таблица для ApplyMap.
    /// </summary>
    Mapped
}
