using Loader.Lang;

namespace Loader.Script;

public sealed class QueryResolutionException : LoadScriptStageException
{
    public QueryResolutionException(string message, LangSpan? span = null, Exception? innerException = null)
        : base(LoadScriptStage.QueryResolution, message, span, innerException)
    {
    }

    public QueryResolutionException(IReadOnlyList<LangError> errors, Exception? innerException = null)
        : base(
            LoadScriptStage.QueryResolution,
            CreateMessage(errors),
            errors,
            innerException)
    {
    }

    private static string CreateMessage(IReadOnlyList<LangError> errors)
    {
        if (errors.Count == 1)
        {
            return $"Не удалось разрешить LOAD query: {errors[0].Message}";
        }

        return $"Не удалось разрешить LOAD query. Ошибок: {errors.Count}.";
    }
}
