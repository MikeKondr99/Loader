namespace Loader.Script.Execution;

internal sealed class PostgresLoadSourceResolver : DatabaseLoadSourceResolver
{
    public PostgresLoadSourceResolver()
        : base(ScriptConnectionType.Postgres)
    {
    }

    public override string Name => "Postgres";
}
