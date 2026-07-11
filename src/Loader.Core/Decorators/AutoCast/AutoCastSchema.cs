namespace Loader.Core.Decorators.AutoCast;

/// <summary>
/// Явная схема автокаста, которую pipeline применяет без попытки решать, откуда она получена.
/// </summary>
public sealed record AutoCastSchema
{
    public required IReadOnlyList<AutoCastField> Fields { get; init; }
}
