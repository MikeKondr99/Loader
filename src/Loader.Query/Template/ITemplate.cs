namespace Loader.Query.Template;

/// <summary>
/// SQL-шаблон выражения. ConstToken пишет текст как есть, ArgToken рекурсивно раскрывает аргумент.
/// </summary>
public interface ITemplate
{
    IReadOnlyList<ITemplateToken> Tokens { get; }
}
