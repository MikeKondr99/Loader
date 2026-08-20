namespace Loader.Script;

public interface IProgressLogger
{
    ValueTask ReportAsync(ScriptProgressEvent progressEvent, CancellationToken cancellationToken = default);
}

public sealed class NullProgressLogger : IProgressLogger
{
    public static readonly NullProgressLogger Instance = new();

    private NullProgressLogger()
    {
    }

    public ValueTask ReportAsync(ScriptProgressEvent progressEvent, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
