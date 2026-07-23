using Loader.Lang.Statements;

namespace Loader.Lang;

/// <summary>
/// Top-level script model. A script is an ordered list of statements.
/// </summary>
public sealed record Script
{
    public required List<Statement> Statements { get; init; }

    public static ParseResult<Script> Parse(string text)
    {
        return ScriptParser.Parse(text);
    }
}
