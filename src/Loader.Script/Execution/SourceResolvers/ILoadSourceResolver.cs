using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver одного конкретного provider-а: проверяет options, привязывается к внешнему source
/// или уже загруженным script-таблицам и возвращает <see cref="LoadProviderSource"/>.
/// </summary>
internal interface ILoadSourceResolver
{
    /// <summary>
    /// Имя provider-а в синтаксисе <c>FROM Name(...)</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Валидирует provider-specific options и готовит источник строк для LOAD.
    /// Ошибки resolve добавляются в общий список, чтобы пользователь получил все проблемы statement сразу.
    /// </summary>
    ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken);
}
