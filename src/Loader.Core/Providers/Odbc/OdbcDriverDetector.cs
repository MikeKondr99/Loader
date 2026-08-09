using System.Data.Odbc;

namespace Loader.Core.Providers.Odbc;

/// <summary>
/// Нестрогий детектор для распространённых семейств ODBC-драйверов.
/// </summary>
public static class OdbcDriverDetector
{
    /// <summary>
    /// Классифицирует имя ODBC-драйвера, полученное из <see cref="OdbcConnection.Driver"/>.
    /// </summary>
    /// <param name="driverName">Сырое имя драйвера, возможно обёрнутое в ODBC-скобки.</param>
    /// <returns>Нормализованные метаданные драйвера для диагностики.</returns>
    public static OdbcDriverInfo FromDriverName(string? driverName)
    {
        var name = NormalizeDriverName(driverName);
        var normalized = name.ToLowerInvariant();

        var kind = normalized switch
        {
            _ when normalized.Contains("hive", StringComparison.Ordinal) => OdbcDriverKind.Hive,
            _ when normalized.Contains("sql server", StringComparison.Ordinal)
                   || normalized.Contains("msodbcsql", StringComparison.Ordinal) => OdbcDriverKind.SqlServer,
            _ when normalized.Contains("postgresql", StringComparison.Ordinal)
                   || normalized.Contains("psqlodbc", StringComparison.Ordinal) => OdbcDriverKind.Postgres,
            _ when normalized.Contains("mariadb", StringComparison.Ordinal) => OdbcDriverKind.MariaDb,
            _ when normalized.Contains("mysql", StringComparison.Ordinal) => OdbcDriverKind.MySql,
            _ when normalized.Contains("oracle", StringComparison.Ordinal) => OdbcDriverKind.Oracle,
            _ when normalized.Contains("sqlite", StringComparison.Ordinal) => OdbcDriverKind.SQLite,
            _ when normalized.Contains("access", StringComparison.Ordinal)
                   || normalized.Contains("aceodbc", StringComparison.Ordinal) => OdbcDriverKind.Access,
            _ when normalized.Contains("excel", StringComparison.Ordinal) => OdbcDriverKind.Excel,
            _ when normalized.Contains("teradata", StringComparison.Ordinal) => OdbcDriverKind.Teradata,
            _ when normalized.Contains("snowflake", StringComparison.Ordinal) => OdbcDriverKind.Snowflake,
            _ when normalized.Contains("bigquery", StringComparison.Ordinal) => OdbcDriverKind.BigQuery,
            _ when normalized.Contains("databricks", StringComparison.Ordinal)
                   || normalized.Contains("spark", StringComparison.Ordinal) => OdbcDriverKind.Databricks,
            _ when normalized.Contains("redshift", StringComparison.Ordinal) => OdbcDriverKind.Redshift,
            _ when normalized.Contains("hana", StringComparison.Ordinal) => OdbcDriverKind.Hana,
            _ when normalized.Contains("db2", StringComparison.Ordinal) => OdbcDriverKind.Db2,
            _ when normalized.Contains("informix", StringComparison.Ordinal) => OdbcDriverKind.Informix,
            _ => OdbcDriverKind.Unknown
        };

        return new OdbcDriverInfo
        {
            Kind = kind,
            Name = name
        };
    }

    /// <summary>
    /// Читает и классифицирует необязательный параметр Driver из ODBC connection string.
    /// </summary>
    /// <param name="connectionString">ODBC connection string, переданный из скрипта.</param>
    /// <returns>Метаданные драйвера или <see cref="OdbcDriverKind.Unknown"/>, если Driver отсутствует или невалиден.</returns>
    public static OdbcDriverInfo FromConnectionString(string connectionString)
    {
        try
        {
            var builder = new OdbcConnectionStringBuilder(connectionString);
            return builder.TryGetValue("Driver", out var driver)
                ? FromDriverName(driver?.ToString())
                : FromDriverName(null);
        }
        catch (ArgumentException)
        {
            return FromDriverName(null);
        }
    }

    private static string NormalizeDriverName(string? driverName)
    {
        var name = driverName?.Trim() ?? string.Empty;
        return name.Length >= 2 && name[0] == '{' && name[^1] == '}'
            ? name[1..^1].Trim()
            : name;
    }
}