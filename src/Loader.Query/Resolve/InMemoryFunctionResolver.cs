namespace Loader.Query.Resolve;

/// <summary>
/// Простое exact-match хранилище функций. Позже сюда можно подать ClickHouse function library.
/// </summary>
public sealed class InMemoryFunctionResolver : IFunctionResolver
{
    private readonly IReadOnlyList<FunctionDefinition> functions;

    public InMemoryFunctionResolver(IReadOnlyList<FunctionDefinition> functions)
    {
        this.functions = functions;
    }

    public FunctionDefinition? Resolve(FunctionSignature signature)
    {
        return functions.FirstOrDefault(function =>
            function.Name.Equals(signature.Name, StringComparison.OrdinalIgnoreCase)
            && function.Kind == signature.Kind
            && function.ArgumentTypes.Count == signature.ArgumentTypes.Count
            && function.ArgumentTypes
                .Zip(signature.ArgumentTypes)
                .All(static pair => pair.First == pair.Second.DataType));
    }
}
