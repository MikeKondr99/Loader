namespace Loader.Tests.Common;

public sealed class ClickHouseTestResourceAttribute :
    DatabaseTestResourceAttribute<ClickHouseTestDatabase, ClickHouseParallelLimit>
{
    public ClickHouseTestResourceAttribute()
        : base(TestCategories.ClickHouse)
    {
    }
}
