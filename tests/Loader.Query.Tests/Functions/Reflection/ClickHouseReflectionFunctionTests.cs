using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Reflection;

public sealed class ClickHouseReflectionFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseReflectionFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Type(42)", "int!")]
    [Arguments("Type(If(false, 42, null))", "int")]
    [Arguments("Type(Int(null))", "int")]
    [Arguments("Type('hello')", "text!")]
    [Arguments("Type(If(false, 'hello', null))", "text")]
    [Arguments("Type(''.EmptyIsNull())", "text")]
    [Arguments("Type(3.14)", "num!")]
    [Arguments("Type(If(false, 3.14, null))", "num")]
    [Arguments("Type(Num(null))", "num")]
    [Arguments("Type(Date(2023, 1, 1))", "date!")]
    [Arguments("Type(Date(null, 1, 1))", "date")]
    [Arguments("Type(true)", "bool!")]
    [Arguments("Type(Bool(null))", "bool")]
    [Arguments("Type(null)", "null")]
    public Task Type_function(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    public Task Db_name()
    {
        return AssertExpressionAsync("DbName()", "ClickHouse");
    }

    [Test]
    public async Task Db_version()
    {
        var version = await GetStringAsync("DbVersion()");
        await Assert.That(version.Split('.').Length).IsGreaterThanOrEqualTo(2);
    }
}
