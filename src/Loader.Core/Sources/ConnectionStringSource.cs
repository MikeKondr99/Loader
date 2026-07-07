using Loader.Core.Abstractions;

namespace Loader.Core.Sources;

/// <summary>
/// Общий DB source для provider-ов, которым достаточно connection string.
/// </summary>
public interface IDatabaseSource : ISource
{
    string ConnectionString { get; }
}

/// <summary>
/// Source, который хранит connection string для DB-провайдеров.
/// </summary>
public sealed record ConnectionStringSource : IDatabaseSource
{
    public required string ConnectionString { get; init; }
}
