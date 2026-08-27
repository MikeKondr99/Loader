using Loader.Lang.Statements;

namespace Loader.Script;

/// <summary>
/// Верхнеуровневый resolver секции <c>FROM</c>. Выбирает конкретный provider по имени
/// и возвращает подготовленное описание source-а для дальнейшего LOAD pipeline.
/// </summary>
public interface ILoadProviderResolver
{
    /// <summary>
    /// Разрешает provider call из <see cref="LoadStatement.SourceCall"/> в исполняемый источник данных.
    /// </summary>
    ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}
