using System.Data.Common;

namespace Loader.Demo;

internal sealed record DemoSource
{
    public required string Kind { get; init; }

    public required bool RequiresBuffer { get; init; }

    public required Func<CancellationToken, ValueTask<DbDataReader>> OpenReaderAsync { get; init; }
}
