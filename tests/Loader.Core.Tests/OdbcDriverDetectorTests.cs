using Loader.Core.Providers.Odbc;

namespace Loader.Core.Tests;

public sealed class OdbcDriverDetectorTests
{
    [Test]
    [MethodDataSource(nameof(DriverNameCases))]
    [DisplayName("ODBC detector определяет тип драйвера по имени")]
    public async Task Driver_name_maps_to_expected_kind(string driverName, OdbcDriverKind expectedKind)
    {
        var info = OdbcDriverDetector.FromDriverName(driverName);

        await Assert.That(info.Kind).IsEqualTo(expectedKind);
        await Assert.That(info.Name).IsEqualTo(driverName);
    }

    [Test]
    [DisplayName("ODBC detector читает значение Driver из connection string")]
    public async Task Connection_string_driver_maps_to_expected_kind()
    {
        var info = OdbcDriverDetector.FromConnectionString(
            "Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=db;Uid=sa;Pwd=password");

        await Assert.That(info.Kind).IsEqualTo(OdbcDriverKind.SqlServer);
        await Assert.That(info.Name).IsEqualTo("ODBC Driver 18 for SQL Server");
    }

    [Test]
    [DisplayName("ODBC detector убирает фигурные скобки из имени драйвера")]
    public async Task Driver_name_trims_odbc_braces()
    {
        var info = OdbcDriverDetector.FromDriverName("{PostgreSQL Unicode(x64)}");

        await Assert.That(info.Kind).IsEqualTo(OdbcDriverKind.Postgres);
        await Assert.That(info.Name).IsEqualTo("PostgreSQL Unicode(x64)");
    }

    [Test]
    [DisplayName("ODBC detector возвращает Unknown если в connection string нет Driver")]
    public async Task Connection_string_without_driver_returns_unknown()
    {
        var info = OdbcDriverDetector.FromConnectionString("Dsn=analytics");

        await Assert.That(info.Kind).IsEqualTo(OdbcDriverKind.Unknown);
        await Assert.That(info.Name).IsEqualTo(string.Empty);
    }

    public static IEnumerable<(string DriverName, OdbcDriverKind ExpectedKind)> DriverNameCases()
    {
        yield return ("ODBC Driver 18 for SQL Server", OdbcDriverKind.SqlServer);
        yield return ("msodbcsql18.dll", OdbcDriverKind.SqlServer);
        yield return ("PostgreSQL Unicode(x64)", OdbcDriverKind.Postgres);
        yield return ("psqlodbc", OdbcDriverKind.Postgres);
        yield return ("Simba Hive ODBC Driver", OdbcDriverKind.Hive);
        yield return ("Cloudera Hive ODBC Driver", OdbcDriverKind.Hive);
        yield return ("MySQL ODBC 8.0 Unicode Driver", OdbcDriverKind.MySql);
        yield return ("MariaDB ODBC 3.1 Driver", OdbcDriverKind.MariaDb);
        yield return ("maodbc.dll", OdbcDriverKind.MariaDb);
        yield return ("Oracle in OraClient19Home1", OdbcDriverKind.Oracle);
        yield return ("SQLite3 ODBC Driver", OdbcDriverKind.SQLite);
        yield return ("Microsoft Access Driver (*.mdb, *.accdb)", OdbcDriverKind.Access);
        yield return ("Microsoft Excel Driver (*.xls, *.xlsx)", OdbcDriverKind.Excel);
        yield return ("Teradata Database ODBC Driver", OdbcDriverKind.Teradata);
        yield return ("SnowflakeDSIIDriver", OdbcDriverKind.Snowflake);
        yield return ("Simba Google BigQuery ODBC Driver", OdbcDriverKind.BigQuery);
        yield return ("Databricks ODBC Driver", OdbcDriverKind.Databricks);
        yield return ("Simba Spark ODBC Driver", OdbcDriverKind.Databricks);
        yield return ("Amazon Redshift ODBC Driver", OdbcDriverKind.Redshift);
        yield return ("HDBODBC SAP HANA", OdbcDriverKind.Hana);
        yield return ("IBM DB2 ODBC DRIVER", OdbcDriverKind.Db2);
        yield return ("IBM INFORMIX ODBC DRIVER", OdbcDriverKind.Informix);
        yield return ("Some Vendor Driver", OdbcDriverKind.Unknown);
    }
}
