using System.Data.Common;
using Loader.Core.Models;

namespace Loader.Script;

/// <summary>
/// Результат разрешения <c>FROM</c>.
/// Конкретный тип показывает, нужно ли читать внешний <see cref="DbDataReader"/> или можно использовать SQL напрямую.
/// </summary>
public abstract record LoadFromSource;

/// <summary>
/// FROM, который можно открыть только как reader и затем материализовать в ClickHouse temp table.
/// </summary>
public sealed record ReaderLoadFromSource : LoadFromSource
{
    /// <summary>
    /// Открывает поток строк источника. Метод вызывается уже после успешного resolve options.
    /// </summary>
    public required Func<CancellationToken, ValueTask<DbDataReader>> OpenReaderAsync { get; init; }

    /// <summary>
    /// Требует ли reader буферизации перед нормализацией.
    /// Нужно для providers с хрупким streaming reader или раздельным чтением schema/rows.
    /// </summary>
    public required bool RequiresBuffer { get; init; }
}

/// <summary>
/// FROM, который уже выражен ClickHouse SQL-фрагментом и не требует temp table перед LOAD query.
/// </summary>
public sealed record SqlLoadFromSource : LoadFromSource
{
    /// <summary>
    /// SQL-выражение, которое можно поставить после FROM. Для подзапросов уже содержит внешние скобки.
    /// </summary>
    public required string Sql { get; init; }

    /// <summary>
    /// Поля, которые видит LOAD поверх этого source.
    /// </summary>
    public required IReadOnlyList<LoadFromSqlField> Fields { get; init; }
}

/// <summary>
/// Одно поле SQL-source: доменное имя для выражений LOAD и физическое имя внутри SQL-фрагмента.
/// </summary>
public sealed record LoadFromSqlField
{
    /// <summary>
    /// Имя поля, которым пользуется скрипт.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Имя колонки, которое реально возвращает SQL-фрагмент.
    /// </summary>
    public required string PhysicalName { get; init; }

    /// <summary>
    /// Доменный тип поля.
    /// </summary>
    public required DataType DataType { get; init; }

    /// <summary>
    /// Может ли поле возвращать null.
    /// </summary>
    public required bool CanBeNull { get; init; }
}
