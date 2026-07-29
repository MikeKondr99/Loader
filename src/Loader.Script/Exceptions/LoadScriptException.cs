using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script;

public sealed class LoadScriptException : Exception
{
    public LoadScriptException(
        int statementIndex,
        Statement statement,
        LoadScriptStageException innerException)
        : base(CreateMessage(statementIndex, innerException), innerException)
    {
        StatementIndex = statementIndex;
        StatementType = statement.GetType().Name;
        Stage = innerException.Stage;
        Span = innerException.Span;
        Errors = innerException.Errors;
    }

    public int StatementIndex { get; }

    public string StatementType { get; }

    public LoadScriptStage Stage { get; }

    public LangSpan? Span { get; }

    public IReadOnlyList<LangError> Errors { get; }

    private static string CreateMessage(int statementIndex, LoadScriptStageException innerException)
    {
        return $"Ошибка script в statement #{statementIndex} на этапе {innerException.Stage}.";
    }
}
