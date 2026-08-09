namespace Loader.Script.Execution;

internal sealed class ClickHouseLoadSourceResolver : DatabaseLoadSourceResolver
{
    public ClickHouseLoadSourceResolver()
        : base(ScriptConnectionType.ClickHouse)
    {
    }

    public override string Name => "ClickHouse";
}
