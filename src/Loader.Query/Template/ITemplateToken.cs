namespace Loader.Query.Template;

public interface ITemplateToken;

public readonly record struct ConstToken(string Text) : ITemplateToken;

public readonly record struct ArgToken(int Index) : ITemplateToken;
