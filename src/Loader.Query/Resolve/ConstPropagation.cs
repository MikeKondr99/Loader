namespace Loader.Query.Resolve;

/// <summary>
/// Правило распространения константности результата функции.
/// </summary>
public enum ConstPropagation
{
    Default = 1,
    AlwaysTrue = 2,
    AlwaysFalse = 3
}
