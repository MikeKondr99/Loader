namespace Loader.Core.Providers.Odbc;

public enum OdbcDriverKind
{
    Unknown,
    Hive,
    SqlServer,
    Postgres,
    MySql,
    MariaDb,
    Oracle,
    SQLite,
    Access,
    Excel,
    Teradata,
    Snowflake,
    BigQuery,
    Databricks,
    Redshift,
    Hana,
    Db2,
    Informix
}

public sealed record OdbcDriverInfo
{
    public required OdbcDriverKind Kind { get; init; }

    public required string Name { get; init; }
}