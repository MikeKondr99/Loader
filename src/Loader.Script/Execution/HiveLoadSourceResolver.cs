namespace Loader.Script.Execution;

internal sealed class HiveLoadSourceResolver : DatabaseLoadSourceResolver
{
    public HiveLoadSourceResolver()
        : base(ScriptConnectionType.Hive)
    {
    }

    public override string Name => "Hive";
}
