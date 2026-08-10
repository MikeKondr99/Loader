namespace Loader.Tests.Common;

public sealed class OracleTestResourceAttribute :
    DatabaseTestResourceAttribute<OracleTestDatabase, OracleParallelLimit>
{
    public OracleTestResourceAttribute()
        : base(TestCategories.Oracle)
    {
    }
}
