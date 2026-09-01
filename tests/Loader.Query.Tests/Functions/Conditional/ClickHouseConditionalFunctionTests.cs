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
    [Arguments("Not(null)", null)]
    [Arguments("true and null", null)]
    [Arguments("false and null", false)]
    [Arguments("true or null", true)]
    [Arguments("false or null", null)]
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
    [Arguments("If(true, Date('2026-01-02'), Date('2026-01-03'))", "@2026-01-02")]
    [Arguments("If(false, Date('2026-01-02'), Date('2026-01-03'))", "@2026-01-03")]
    [Arguments("If(null, Date('2026-01-02'), Date('2026-01-03'))", "@2026-01-03")]
    [Arguments("If(true, null, Date('2026-01-03')).Type()", "date")]
    [Arguments("If(true, Time('03:04:05'), Time('06:07:08'))", "@1970-01-01 03:04:05")]
    [Arguments("If(false, Time('03:04:05'), Time('06:07:08'))", "@1970-01-01 06:07:08")]
    [Arguments("If(null, Time('03:04:05'), Time('06:07:08'))", "@1970-01-01 06:07:08")]
    [Arguments("If(true, null, Time('06:07:08')).Type()", "time")]
    [Arguments("If(true, true, false)", true)]
    [Arguments("If(false, true, false)", false)]
    [Arguments("If(null, true, false)", false)]
    [Arguments("If(true, null, false).Type()", "bool")]
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
    [Arguments("Date('2026-01-02').Alt(Date('2026-01-03'))", "@2026-01-02")]
    [Arguments("Date(null).Alt(Date('2026-01-03'))", "@2026-01-03")]
    [Arguments("Date(null).Alt(Date(null))", null)]
    [Arguments("Time('03:04:05').Alt(Time('06:07:08'))", "@1970-01-01 03:04:05")]
    [Arguments("Time(null).Alt(Time('06:07:08'))", "@1970-01-01 06:07:08")]
    [Arguments("Time(null).Alt(Time(null))", null)]
    [Arguments("true.Alt(false)", true)]
    [Arguments("If(false, true, null).Alt(true)", true)]
    [Arguments("If(false, true, null).Alt(If(false, true, null))", null)]
    [Arguments("If(false, true, null).Alt(true).Type()", "bool!")]
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
    [Arguments("IsNull(Date('2026-01-02'))", false)]
    [Arguments("IsNull(Date(null))", true)]
    [Arguments("IsNull(Time('03:04:05'))", false)]
    [Arguments("IsNull(Time(null))", true)]
    [Arguments("NotNull(null)", false)]
    [Arguments("NotNull(42)", true)]
    [Arguments("NotNull('text')", true)]
    [Arguments("NotNull('')", true)]
    [Arguments("NotNull(0)", true)]
    [Arguments("NotNull(1 + null)", false)]
    [Arguments("NotNull(Lower(Text(null)))", false)]
    [Arguments("NotNull(Date('2026-01-02'))", true)]
    [Arguments("NotNull(Date(null))", false)]
    [Arguments("NotNull(Time('03:04:05'))", true)]
    [Arguments("NotNull(Time(null))", false)]
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
    [Arguments("Case(true, Date('2026-01-02'))", "@2026-01-02")]
    [Arguments("Case(false, Date('2026-01-02'))", null)]
    [Arguments("Case(true, Time('03:04:05'))", "@1970-01-01 03:04:05")]
    [Arguments("Case(false, Time('03:04:05'))", null)]
    [Arguments("Case(true, true)", true)]
    [Arguments("Case(false, true)", null)]
    [Arguments("Type(Case(true, 'text'))", "text")]
    [Arguments("Type(Case(false, 'text'))", "text")]
    [Arguments("Type(Case(true, 42))", "int")]
    [Arguments("Type(Case(false, 42))", "int")]
    [Arguments("Type(Case(true, Date('2026-01-02')))", "date")]
    [Arguments("Type(Case(true, Time('03:04:05')))", "time")]
    [Arguments("Type(Case(true, true))", "bool")]
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
    [Arguments("Case(Date('2026-01-02'), true, Date('2026-01-03'))", "@2026-01-02")]
    [Arguments("Case(Date(null), true, Date('2026-01-03'))", "@2026-01-03")]
    [Arguments("Case(Date(null), false, Date('2026-01-03'))", null)]
    [Arguments("Case(Time('03:04:05'), true, Time('06:07:08'))", "@1970-01-01 03:04:05")]
    [Arguments("Case(Time(null), true, Time('06:07:08'))", "@1970-01-01 06:07:08")]
    [Arguments("Case(Time(null), false, Time('06:07:08'))", null)]
    [Arguments("Case(true, true, false)", true)]
    [Arguments("Case(If(false, true, null), true, false)", false)]
    [Arguments("Case(If(false, true, null), false, true)", null)]
    [Arguments("Type(Case('input', true, 'other'))", "text")]
    [Arguments("Type(Case(null, true, 'other'))", "text")]
    [Arguments("Type(Case(null, false, 'other'))", "text")]
    [Arguments("Type(Case(42, true, 100))", "int")]
    [Arguments("Type(Case(Date(null), true, Date('2026-01-03')))", "date")]
    [Arguments("Type(Case(Time(null), true, Time('06:07:08')))", "time")]
    [Arguments("Type(Case(If(false, true, null), true, false))", "bool")]
    public Task Case_input_tests(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}
