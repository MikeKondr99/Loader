namespace Loader.Script.Execution;

internal sealed class OracleLoadSourceResolver : DatabaseLoadSourceResolver
{
    public OracleLoadSourceResolver()
        : base(ScriptConnectionType.Oracle)
    {
    }

    public override string Name => "Oracle";
}
