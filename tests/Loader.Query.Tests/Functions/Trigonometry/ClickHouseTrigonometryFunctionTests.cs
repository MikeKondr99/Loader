using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Trigonometry;

public sealed class ClickHouseTrigonometryFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseTrigonometryFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Cos(0)", 1.0)]
    [Arguments("Cos(1.0471975511965976)", 0.5)]
    [Arguments("Cos(1.5707963267948966)", 0.0)]
    [Arguments("Sin(0)", 0.0)]
    [Arguments("Sin(0.5235987755982988)", 0.5)]
    [Arguments("Sin(1.5707963267948966)", 1.0)]
    [Arguments("Tan(0)", 0.0)]
    [Arguments("Tan(0.7853981633974483)", 1.0)]
    public Task Basic_trigonometry(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Acos(1)", 0.0)]
    [Arguments("Acos(-0.5)", 2.0943951023931957)]
    [Arguments("Acos(0.5)", 1.0471975511965976)]
    [Arguments("Acos(1.1)", null)]
    [Arguments("Acos(999)", null)]
    [Arguments("Asin(0)", 0.0)]
    [Arguments("Asin(-0.5)", -0.5235987755982988)]
    [Arguments("Asin(0.5)", 0.5235987755982988)]
    [Arguments("Asin(-1.0)", -1.5707963267948966)]
    [Arguments("Asin(-1.1)", null)]
    [Arguments("Asin(999)", null)]
    [Arguments("Atan(0)", 0.0)]
    [Arguments("Atan(1)", 0.7853981633974483)]
    [Arguments("Atan2(1, 1)", 0.7853981633974483)]
    [Arguments("Atan2(-1, -1)", -2.356194490192345)]
    public Task Inverse_trigonometry(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Rad(0)", 0.0)]
    [Arguments("Rad(180)", 3.141592653589793)]
    [Arguments("Rad(360)", 6.283185307179586)]
    [Arguments("Rad(90)", 1.5707963267948966)]
    [Arguments("Deg(0)", 0.0)]
    [Arguments("Deg(3.1415926535897931)", 180.0)]
    [Arguments("Deg(6.2831853071795862)", 360.0)]
    [Arguments("Deg(1.5707963267948966)", 90.0)]
    public Task Degrees_radians(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Cosh(0)", 1.0)]
    [Arguments("Cosh(1)", 1.5430806348152437)]
    [Arguments("Cosh(-1)", 1.5430806348152437)]
    [Arguments("Sinh(0)", 0.0)]
    [Arguments("Sinh(1)", 1.1752011936438014)]
    [Arguments("Sinh(-1)", -1.1752011936438014)]
    [Arguments("Tanh(0)", 0.0)]
    [Arguments("Tanh(1)", 0.7615941559557649)]
    [Arguments("Tanh(-1)", -0.7615941559557649)]
    [Arguments("Tanh(100)", 1.0)]
    public Task Hyperbolic_functions(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Acosh(1)", 0.0)]
    [Arguments("Acosh(2)", 1.3169578969248166)]
    [Arguments("Acosh(0.5)", null)]
    [Arguments("Acosh(-1)", null)]
    [Arguments("Asinh(0)", 0.0)]
    [Arguments("Asinh(1)", 0.881373587019543)]
    [Arguments("Asinh(-1)", -0.881373587019543)]
    [Arguments("Atanh(0.5)", 0.5493061443340548)]
    [Arguments("Atanh(-0.99)", -2.6466524123622457)]
    [Arguments("Atanh(1.0)", null)]
    [Arguments("Atanh(-1.0)", null)]
    [Arguments("Atanh(1.1)", null)]
    public Task Inverse_hyperbolic_functions(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}
