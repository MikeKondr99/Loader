namespace Loader.Tests.Common;

public sealed class SqlServerTestResourceAttribute :
    DatabaseTestResourceAttribute<SqlServerTestDatabase, SqlServerParallelLimit>
{
    public SqlServerTestResourceAttribute()
        : base(TestCategories.SqlServer)
    {
    }
}
