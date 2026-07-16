using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Conditional;

public sealed class ClickHouseConditionalFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseConditionalFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("false", false)]
    [Arguments("Not(true)", false)]
    [Arguments("Not(false)", true)]
    [Arguments("true and true", true)]
    [Arguments("true and false", false)]
    [Arguments("false and false", false)]
    [Arguments("true or true", true)]
    [Arguments("true or false", true)]
    [Arguments("false or false", false)]
    public Task Basic_logic(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("If(true, null, 0).Type()", "int")]
    [Arguments("If(true, null, 0.0).Type()", "num")]
    [Arguments("If(true, null, 'lol').Type()", "text")]
    [Arguments("If(null, 1, 0).Type()", "int!")]
    [Arguments("If(null, 1.0, 0.0).Type()", "num!")]
    [Arguments("If(null, 'one', 'zero').Type()", "text!")]
    [Arguments("If(null, 'then', 'else')", "else")]
    [Arguments("If(10 > 5 and null, 'then', 'else')", "else")]
    [Arguments("If(true, 10, 15.5)", 10.0)]
    [Arguments("If(true, 0, 12 / 0)", 0)]
    public Task If_function(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("2.Alt(3).Type()", "int!")]
    [Arguments("Int(null).Alt(3).Type()", "int!")]
    [Arguments("2.Alt(Int(null)).Type()", "int!")]
    [Arguments("Int(null).Alt(Int(null)).Type()", "int")]
    [Arguments("2.Alt(3)", 2)]
    [Arguments("Int(null).Alt(3)", 3)]
    [Arguments("2.Alt(Int(null))", 2)]
    [Arguments("Int(null).Alt(Int(null))", null)]
    [Arguments("Int(null).Alt(If(true, 2, null))", 2)]
    [Arguments("'first'.Alt('second')", "first")]
    [Arguments("Text(null).Alt('default')", "default")]
    public Task Alt_function(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("IsNull(null)", true)]
    [Arguments("IsNull(42)", false)]
    [Arguments("IsNull('text')", false)]
    [Arguments("IsNull('')", false)]
    [Arguments("IsNull(0)", false)]
    [Arguments("IsNull(1 + null)", true)]
    [Arguments("IsNull(Lower(Text(null)))", true)]
    [Arguments("NotNull(null)", false)]
    [Arguments("NotNull(42)", true)]
    [Arguments("NotNull('text')", true)]
    [Arguments("NotNull('')", true)]
    [Arguments("NotNull(0)", true)]
    [Arguments("NotNull(1 + null)", false)]
    [Arguments("NotNull(Lower(Text(null)))", false)]
    public Task Null_checks(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Case(true, 'text')", "text")]
    [Arguments("Case(false, 'text')", null)]
    [Arguments("Case(null, 'text')", null)]
    [Arguments("Case(true, 42)", 42)]
    [Arguments("Case(false, 42)", null)]
    [Arguments("Case(true, 3.14)", 3.14)]
    [Arguments("Case(false, 3.14)", null)]
    [Arguments("Type(Case(true, 'text'))", "text")]
    [Arguments("Type(Case(false, 'text'))", "text")]
    [Arguments("Type(Case(true, 42))", "int")]
    [Arguments("Type(Case(false, 42))", "int")]
    public Task Case_condition_tests(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Case('input', true, 'other')", "input")]
    [Arguments("Case('input', false, 'other')", "input")]
    [Arguments("Case(null, true, 'other')", "other")]
    [Arguments("Case(null, false, 'other')", null)]
    [Arguments("Case(42, true, 100)", 42)]
    [Arguments("Case(42, false, 100)", 42)]
    [Arguments("Case(null, true, 100)", 100)]
    [Arguments("Case(null, false, 100)", null)]
    [Arguments("Case(3.14, true, 2.71)", 3.14)]
    [Arguments("Case(null, true, 2.71)", 2.71)]
    [Arguments("Type(Case('input', true, 'other'))", "text")]
    [Arguments("Type(Case(null, true, 'other'))", "text")]
    [Arguments("Type(Case(null, false, 'other'))", "text")]
    [Arguments("Type(Case(42, true, 100))", "int")]
    public Task Case_input_tests(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}
