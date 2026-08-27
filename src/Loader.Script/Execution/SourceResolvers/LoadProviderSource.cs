using System.Data.Common;

namespace Loader.Script;

/// <summary>
/// Результат разрешения <c>FROM</c>: источник строк, который следующий шаг LOAD pipeline
/// откроет как <see cref="DbDataReader"/> и перегрузит во временную таблицу.
/// </summary>
public sealed record LoadProviderSource
{
    /// <summary>
    /// Короткое техническое имя источника для диагностики, telemetry и progress-сообщений.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Требует ли reader буферизации перед нормализацией. Нужно для providers, которые не позволяют
    /// безопасно читать schema и строки одним проходом или имеют хрупкий streaming reader.
    /// </summary>
    public required bool RequiresBuffer { get; init; }

    /// <summary>
    /// Открывает поток строк источника. Метод вызывается уже после успешного resolve options.
    /// </summary>
    public required Func<CancellationToken, ValueTask<DbDataReader>> OpenReaderAsync { get; init; }
}
