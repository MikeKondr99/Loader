namespace Loader.Script.Execution;

internal enum JoinKind
{
    Inner,
    Left,
    Right,
    Full
}

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
