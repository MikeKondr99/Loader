namespace Loader.Core.Providers.Odbc;

/// <summary>
/// Распознанное семейство ODBC-драйвера.
/// </summary>
public enum OdbcDriverKind
{
    /// <summary>
    /// Имя драйвера отсутствует или не совпало с известным семейством.
    /// </summary>
    Unknown,

    /// <summary>
    /// Семейство ODBC-драйверов Apache Hive.
    /// </summary>
    Hive,

    /// <summary>
    /// Семейство ODBC-драйверов Microsoft SQL Server.
    /// </summary>
    SqlServer,

    /// <summary>
    /// Семейство ODBC-драйверов PostgreSQL.
    /// </summary>
    Postgres,

    /// <summary>
    /// Семейство ODBC-драйверов MySQL.
    /// </summary>
    MySql,

    /// <summary>
    /// Семейство ODBC-драйверов MariaDB.
    /// </summary>
    MariaDb,

    /// <summary>
    /// Семейство ODBC-драйверов Oracle.
    /// </summary>
    Oracle,

    /// <summary>
    /// Семейство ODBC-драйверов SQLite.
    /// </summary>
    SQLite,

    /// <summary>
    /// Семейство ODBC-драйверов Microsoft Access.
    /// </summary>
    Access,

    /// <summary>
    /// Семейство ODBC-драйверов Microsoft Excel.
    /// </summary>
    Excel,

    /// <summary>
    /// Семейство ODBC-драйверов Teradata.
    /// </summary>
    Teradata,

    /// <summary>
    /// Семейство ODBC-драйверов Snowflake.
    /// </summary>
    Snowflake,

    /// <summary>
    /// Семейство ODBC-драйверов Google BigQuery.
    /// </summary>
    BigQuery,

    /// <summary>
    /// Семейство ODBC-драйверов Databricks или Spark.
    /// </summary>
    Databricks,

    /// <summary>
    /// Семейство ODBC-драйверов Amazon Redshift.
    /// </summary>
    Redshift,

    /// <summary>
    /// Семейство ODBC-драйверов SAP HANA.
    /// </summary>
    Hana,

    /// <summary>
    /// Семейство ODBC-драйверов IBM DB2.
    /// </summary>
    Db2,

    /// <summary>
    /// Семейство ODBC-драйверов IBM Informix.
    /// </summary>
    Informix
}

/// <summary>
/// Описывает имя ODBC-драйвера, полученное из connection string или открытого соединения.
/// </summary>
public sealed record OdbcDriverInfo
{
    /// <summary>
    /// Распознанное семейство драйвера; используется только как нестрогие метаданные.
    /// </summary>
    public required OdbcDriverKind Kind { get; init; }

    /// <summary>
    /// Нормализованное имя ODBC-драйвера без фигурных скобок из ODBC connection string.
    /// </summary>
    public required string Name { get; init; }
}