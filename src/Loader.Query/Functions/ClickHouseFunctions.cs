using Loader.Query.Resolve;

namespace Loader.Query.Functions;

/// <summary>
/// Точка сборки ClickHouse function library.
/// </summary>
public static class ClickHouseFunctions
{
    public static IReadOnlyList<FunctionDefinition> All { get; } =
    [
        ..new TimeFunctions().GetFunctions(),
        ..new ConversionFunctions().GetFunctions(),
        ..new ImplicitConversionFunctions().GetFunctions(),
        ..new ComparisonFunctions().GetFunctions(),
        ..new LogicFunctions().GetFunctions(),
        ..new ConditionalFunctions().GetFunctions(),
        ..new AggregationFunctions().GetFunctions(),
        ..new MathFunctions().GetFunctions(),
        ..new NumberFunctions().GetFunctions(),
        ..new TrigonometryFunctions().GetFunctions(),
        ..new DateFunctions().GetFunctions(),
        ..new ColorFunctions().GetFunctions(),
        ..new FinancialFunctions().GetFunctions(),
        ..new ReflectionFunctions().GetFunctions(),
        ..new JsonFunctions().GetFunctions(),
        ..new StringFunctions().GetFunctions(),
        ..new ScriptFunctions().GetFunctions()
    ];

    public static IFunctionResolver CreateResolver()
    {
        return new FunctionStorage(All);
    }
}
