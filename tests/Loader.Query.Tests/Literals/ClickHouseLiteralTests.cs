using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Literals;

public sealed class ClickHouseLiteralTests : ClickHouseExpressionTestBase
{
    public ClickHouseLiteralTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("21", 21L)]
    [Arguments("00023", 23L)]
    [Arguments("0", 0L)]
    [Arguments("0000", 0L)]
    [Arguments("2147483647", 2147483647L)]
    [Arguments("9223372036854775807", 9223372036854775807L)]
    public Task Int(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("2.1", 2.1d)]
    [Arguments("0.0", 0.0d)]
    [Arguments(".0", 0.0d)]
    [Arguments(".3", 0.3d)]
    [Arguments("4.0", 4.0d)]
    [Arguments("0.0001", 0.0001d)]
    [Arguments("123.456", 123.456d)]
    public Task Number(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("'Hello World!'", "Hello World!")]
    [Arguments("'contains $ inside'", "contains $ inside")]
    [Arguments("''", "")]
    [Arguments(@"'line1\nline2'", "line1\nline2")]
    [Arguments(@"'\\n'", @"\n")]
    [Arguments(@"'tab\tseparated'", "tab\tseparated")]
    [Arguments(@"'caret\rreturn'", "caret\rreturn")]
    [Arguments(@"'backslash\\here'", @"backslash\here")]
    [Arguments(@"'just \p'", @"just \p")]
    [Arguments(@"' \\\\\\ '", @" \\\ ")]
    [Arguments(@"'quoted: \'text\''", "quoted: 'text'")]
    [Arguments("'unicode: привет'", "unicode: привет")]
    [Arguments("'emoji: 😀👍'", "emoji: 😀👍")]
    [Arguments("'café'", "café")]
    [Arguments("'北京'", "北京")]
    [Arguments(@"'; DROP TABLE users; --'", "; DROP TABLE users; --")]
    [Arguments(@"'\' OR 1=1; --'", "' OR 1=1; --")]
    [Arguments(@"'%_%'", "%_%")]
    [Arguments(@"'[](){}|+*?^$.'", "[](){}|+*?^$.")]
    public Task Text(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("false", false)]
    public Task Bool(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }
}
