using System.Data.Common;
using System.Globalization;

namespace Loader.Core.Providers.Jdbc;

internal sealed record JdbcConnectionOptions
{
    public required IReadOnlyList<string> JarPaths { get; init; }

    public required string DriverClass { get; init; }

    public required string JdbcUrl { get; init; }

    public string? User { get; init; }

    public string? Password { get; init; }

    public static JdbcConnectionOptions Parse(string connectionString)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        var jarPath = Required(builder, "JarPath");
        return new JdbcConnectionOptions
        {
            JarPaths = jarPath
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            DriverClass = Required(builder, "DriverClass"),
            JdbcUrl = Required(builder, "JdbcUrl"),
            User = Optional(builder, "User"),
            Password = Optional(builder, "Password")
        };
    }

    private static string Required(DbConnectionStringBuilder builder, string name)
    {
        var value = Optional(builder, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JdbcProviderException($"JDBC connection string должен содержать '{name}'.");
        }

        return value;
    }

    private static string? Optional(DbConnectionStringBuilder builder, string name)
    {
        return builder.TryGetValue(name, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }
}
