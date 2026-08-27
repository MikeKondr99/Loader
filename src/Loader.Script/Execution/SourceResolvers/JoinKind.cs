namespace Loader.Script.Execution;

/// <summary>
/// Поддерживаемый тип соединения для provider-ов <c>Join</c>, <c>LeftJoin</c>, <c>RightJoin</c> и <c>FullJoin</c>.
/// </summary>
internal enum JoinKind
{
    Inner,
    Left,
    Right,
    Full
}

/// <summary>
/// Преобразует внутренний тип join-а в имя provider-а, которое используется в script syntax.
/// </summary>
internal static class JoinKindExtensions
{
    public static string ProviderName(this JoinKind kind)
    {
        return kind switch
        {
            JoinKind.Inner => "Join",
            JoinKind.Left => "LeftJoin",
            JoinKind.Right => "RightJoin",
            JoinKind.Full => "FullJoin",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
