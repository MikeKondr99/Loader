using Loader.Lang.Statements;

namespace Loader.Script;

/// <summary>
/// Верхнеуровневый resolver секции <c>FROM</c>. Выбирает конкретный provider по имени
/// и возвращает единый источник строк для дальнейшего LOAD pipeline.
/// </summary>
public interface ILoadProviderResolver
{
    /// <summary>
    /// Разрешает provider call из <see cref="LoadStatement.SourceCall"/> в исполняемый источник данных.
    /// </summary>
    ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}
