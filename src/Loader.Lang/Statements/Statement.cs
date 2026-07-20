namespace Loader.Lang.Statements;

/// <summary>
/// Базовый узел statement-уровня.
/// </summary>
public abstract record Statement
{
    public static ParseResult<Statement> Parse(string text)
    {
        return StatementParser.Parse(text);
    }
}
