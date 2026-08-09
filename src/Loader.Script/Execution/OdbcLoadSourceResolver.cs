namespace Loader.Script.Execution;

internal sealed class OdbcLoadSourceResolver : DatabaseLoadSourceResolver
{
    public OdbcLoadSourceResolver()
        : base(ScriptConnectionType.Odbc)
    {
    }

    public override string Name => "Odbc";
}
