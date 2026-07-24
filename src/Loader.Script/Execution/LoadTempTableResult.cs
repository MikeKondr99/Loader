using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script;

public sealed record LoadTempTableResult
{
    public required ClickHouseTableName TableName { get; init; }

    public required DataSchema Schema { get; init; }

    public required IReadOnlyList<string> OriginalColumnNames { get; init; }
}
