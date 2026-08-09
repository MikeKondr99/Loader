namespace Loader.Script.Execution;

internal sealed class SqlServerLoadSourceResolver : DatabaseLoadSourceResolver
{
    public SqlServerLoadSourceResolver()
        : base(ScriptConnectionType.SqlServer)
    {
    }

    public override string Name => "SqlServer";
}
