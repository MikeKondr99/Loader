using Loader.Lang;

namespace Loader.Script;

public sealed class ProviderResolutionException : LoadScriptStageException
{
    public ProviderResolutionException(IReadOnlyList<LangError> errors, Exception? innerException = null)
        : base(
            LoadScriptStage.ProviderResolution,
            CreateMessage(errors),
            errors,
            innerException)
    {
    }

    private static string CreateMessage(IReadOnlyList<LangError> errors)
    {
        if (errors.Count == 1)
        {
            return $"Не удалось определить LOAD provider: {errors[0].Message}";
        }

        return $"Не удалось определить LOAD provider. Ошибок: {errors.Count}.";
    }
}
