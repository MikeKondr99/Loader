namespace Loader.Tests.Common;

public sealed class PostgresTestResourceAttribute :
    DatabaseTestResourceAttribute<PostgresTestDatabase, PostgresParallelLimit>
{
    public PostgresTestResourceAttribute()
        : base(TestCategories.Postgres)
    {
    }
}
