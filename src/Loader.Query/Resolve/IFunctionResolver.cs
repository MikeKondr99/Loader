namespace Loader.Query.Resolve;

/// <summary>
/// Хранилище функций, из которого expression resolver получает SQL-шаблон и return type.
/// </summary>
public interface IFunctionResolver
{
    FunctionDefinition? Resolve(FunctionSignature signature);
}
