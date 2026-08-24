using TUnit.Core.Interfaces;

namespace Loader.Tests.Common;

public sealed record ClickHouseParallelLimit : IParallelLimit
{
    public int Limit => 12;
}

public sealed record PostgresParallelLimit : IParallelLimit
{
    public int Limit => 2;
}

public sealed record OdbcMariaDbParallelLimit : IParallelLimit
{
    public int Limit => 1;
}

public sealed record SqlServerParallelLimit : IParallelLimit
{
    public int Limit => 2;
}

public sealed record OracleParallelLimit : IParallelLimit
{
    public int Limit => 4;
}

public sealed record ApacheHiveParallelLimit : IParallelLimit
{
    public int Limit => 4;
}
