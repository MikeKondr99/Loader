using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.String;

public sealed class ClickHouseStringFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseStringFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("'hello' + 'world'", "helloworld")]
    [Arguments("'a' + 'b' + 'c'", "abc")]
    [Arguments("'' + 'text'", "text")]
    [Arguments("'text' + ''", "text")]
    [Arguments("'' + ''", "")]
    [Arguments("'text' + null", null)]
    [Arguments("null + 'text'", null)]
    [Arguments("null + null", null)]
    [Arguments("Type('a' + 'b')", "text!")]
    [Arguments("Type('a' + null)", "text")]
    [Arguments("Type(null + 'b')", "text")]
    [Arguments("'hello ' + 'world'", "hello world")]
    [Arguments("'line1\\n' + 'line2'", "line1\nline2")]
    [Arguments("'tab\\t' + 'end'", "tab\tend")]
    [Arguments("'привет' + 'мир'", "приветмир")]
    [Arguments("'😀' + '👍'", "😀👍")]
    [Arguments("'number: ' + Text(42)", "number: 42")]
    [Arguments("Text(3.14) + ' is pi'", "3.14 is pi")]
    [Arguments("'result: ' + Text(true)", "result: true")]
    [Arguments("Text(false) + ' is false'", "false is false")]
    [Arguments("Type('text' + Text(42))", "text!")]
    [Arguments("Type(Text(42) + 'text')", "text!")]
    [Arguments("'text' + Text(null)", null)]
    [Arguments("Text(null) + 'text'", null)]
    public Task StringAddition(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Upper('Hello World!')", "HELLO WORLD!")]
    public Task FuncUpperTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Lower('Hello World!')", "hello world!")]
    public Task FuncLowerTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Trim('  hello  ')", "hello")]
    [Arguments("Trim('  ')", "")]
    [Arguments("Trim('')", "")]
    [Arguments("Trim('привет  ')", "привет")]
    [Arguments("Trim('😀  👍  ')", "😀  👍")]
    [Arguments("Trim(null)", null)]
    public Task FuncTrimTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("TrimLeft('  hello  ')", "hello  ")]
    [Arguments("TrimLeft('  ')", "")]
    [Arguments("TrimLeft('')", "")]
    [Arguments("TrimLeft('  привет')", "привет")]
    [Arguments("TrimLeft('  😀👍')", "😀👍")]
    [Arguments("TrimLeft(null)", null)]
    public Task FuncTrimLeftTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("TrimRight('  hello  ')", "  hello")]
    [Arguments("TrimRight('  ')", "")]
    [Arguments("TrimRight('')", "")]
    [Arguments("TrimRight('привет  ')", "привет")]
    [Arguments("TrimRight('😀👍  ')", "😀👍")]
    [Arguments("TrimRight(null)", null)]
    public Task FuncTrimRightTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("PadLeft('abc', 5)", "  abc")]
    [Arguments("PadLeft('abc', 3)", "abc")]
    [Arguments("PadLeft('abc', 2)", "ab")]
    [Arguments("PadLeft('abc', 0)", "")]
    [Arguments("PadLeft('', 5)", "     ")]
    [Arguments("PadLeft('привет', 8)", "  привет")]
    [Arguments("PadLeft('😀', 3)", "  😀")]
    [Arguments("PadLeft('abc', -1)", "")]
    [Arguments("PadLeft(null, 5)", null)]
    public Task FuncPadLeftCountTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("PadRight('abc', 5)", "abc  ")]
    [Arguments("PadRight('abc', 3)", "abc")]
    [Arguments("PadRight('abc', 2)", "ab")]
    [Arguments("PadRight('abc', 0)", "")]
    [Arguments("PadRight('', 5)", "     ")]
    [Arguments("PadRight('привет', 8)", "привет  ")]
    [Arguments("PadRight('😀', 3)", "😀  ")]
    [Arguments("PadRight('abc', -1)", "")]
    [Arguments("PadRight(null, 5)", null)]
    public Task FuncPadRightCountTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("PadLeft('abc', 5, '*')", "**abc")]
    [Arguments("PadLeft('abc', 5, '0')", "00abc")]
    [Arguments("PadLeft('abc', 3, '*')", "abc")]
    [Arguments("PadLeft('abc', 2, '*')", "ab")]
    [Arguments("PadLeft('abc', 0, '*')", "")]
    [Arguments("PadLeft('', 5, '-')", "-----")]
    [Arguments("PadLeft('123', 5, '0')", "00123")]
    [Arguments("PadLeft('abc', 5, '.')", "..abc")]
    [Arguments("PadLeft('abc', 5, 'XY')", "XYabc")]
    [Arguments("PadLeft('abc', 5, '')", "abc")]
    [Arguments("PadLeft('abc', -1, '*')", "")]
    [Arguments("PadLeft(null, 5, '*')", null)]
    public Task FuncPadLeftSymbolTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("PadRight('abc', 5, '*')", "abc**")]
    [Arguments("PadRight('abc', 5, '0')", "abc00")]
    [Arguments("PadRight('abc', 3, '*')", "abc")]
    [Arguments("PadRight('abc', 2, '*')", "ab")]
    [Arguments("PadRight('abc', 0, '*')", "")]
    [Arguments("PadRight('', 5, '-')", "-----")]
    [Arguments("PadRight('123', 5, '0')", "12300")]
    [Arguments("PadRight('abc', 5, '.')", "abc..")]
    [Arguments("PadRight('abc', 5, 'XY')", "abcXY")]
    [Arguments("PadRight('abc', 5, '')", "abc")]
    [Arguments("PadRight('abc', -1, '*')", "")]
    [Arguments("PadRight(null, 5, '*')", null)]
    public Task FuncPadRightSymbolTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Substring('Hello World!',5)", "o World!")]
    [Arguments("Substring('Hello World!',5,3)", "o W")]
    public Task FuncSubstringTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Reverse('Hello')", "olleH")]
    [Arguments("Reverse('Привет мир!')", "!рим тевирП")]
    public Task FuncReverseTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("EmptyIsNull('Hello world!')", "Hello world!")]
    [Arguments("EmptyIsNull('')", null)]
    [Arguments("EmptyIsNull(null)", null)]
    public Task FuncEmptyIsNullTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Replace('hello', 'l', 'x')", "hexxo")]
    [Arguments("Replace('hello', 'ell', 'ipp')", "hippo")]
    [Arguments("Replace('a.*b', '.*', '-')", "a-b")]
    [Arguments("Replace('a\\d+b', '\\d+', '-')", "a-b")]
    [Arguments("Replace('a[b]', '[b]', 'c')", "ac")]
    [Arguments("Replace('^$', '^', '!')", "!$")]
    [Arguments("Replace('', 'x', 'y')", "")]
    [Arguments("Replace('hello', 'x', 'y')", "hello")]
    [Arguments("Replace(null, 'a', 'b')", null)]
    [Arguments("Replace('hello', null, 'x')", null)]
    [Arguments("Replace('hello', 'l', null)", null)]
    public Task FuncReplaceTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Repeat('a', 3)", "aaa")]
    [Arguments("Repeat('ab', 2)", "abab")]
    [Arguments("Repeat(' ', 4)", "    ")]
    [Arguments("Repeat('', 5)", "")]
    [Arguments("Repeat('a', 0)", "")]
    [Arguments("Repeat('a', 1)", "a")]
    [Arguments("Repeat('привет', 2)", "приветпривет")]
    [Arguments("Repeat('😀', 3)", "😀😀😀")]
    [Arguments("Repeat('\\n', 2)", "\n\n")]
    [Arguments("Repeat('.*+', 2)", ".*+.*+")]
    [Arguments("Repeat('a', -1)", "")]
    [Arguments("Repeat('abc', 1000).Len()", 3000)]
    [Arguments("Repeat(null, 3)", null)]
    [Arguments("Repeat('abc', null)", null)]
    public Task FuncRepeatTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Index('abc', 'a')", 1)]
    [Arguments("Index('abc', 'b')", 2)]
    [Arguments("Index('abc', 'c')", 3)]
    [Arguments("Index('abc', 'bc')", 2)]
    [Arguments("Index('aaaaAaaa', 'A')", 5)]
    [Arguments("Index('abc', 'd')", null)]
    [Arguments("Index('', 'a')", null)]
    [Arguments("Index('abc', '')", 1)]
    [Arguments("Index('aabaa', 'aa')", 1)]
    [Arguments("Index('привет', 'ив')", 3)]
    public Task FuncIndexTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("LastIndex('abc', 'a')", 1)]
    [Arguments("LastIndex('abcba', 'b')", 4)]
    [Arguments("LastIndex('abc', 'c')", 3)]
    [Arguments("LastIndex('abcabc', 'bc')", 5)]
    [Arguments("Index('aaaaAaaa', 'A')", 5)]
    [Arguments("LastIndex('abc', 'd')", null)]
    [Arguments("LastIndex('', 'a')", null)]
    [Arguments("LastIndex('abc', '')", 4)]
    [Arguments("LastIndex('aabaa', 'aa')", 4)]
    [Arguments("LastIndex('привет', 'е')", 5)]
    public Task FuncLastIndexTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Len('')", 0)]
    [Arguments("Len('a')", 1)]
    [Arguments("Len(' x   ')", 5)]
    [Arguments("Len('abc')", 3)]
    [Arguments("Len('привет')", 6)]
    [Arguments("Len('😀')", 1)]
    [Arguments("Len('a\tb')", 3)]
    [Arguments("Len(null)", null)]
    public Task FuncLenTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("'hello'.Contains('ell')", true)]
    [Arguments("Contains('hello','world')", false)]
    [Arguments("'hello'.Contains('')", true)]
    [Arguments("''.Contains('a')", false)]
    [Arguments("''.Contains('')", true)]
    [Arguments("Contains('привет','иве')", true)]
    [Arguments("'😀👍👋'.Contains('👍')", true)]
    [Arguments("'Apple'.Contains('P')", false)]
    [Arguments("'Apple'.Contains('p')", true)]
    public Task FuncContainsTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("'hello'.StartsWith('hel')", true)]
    [Arguments("StartsWith('hello','world')", false)]
    [Arguments("'hello'.StartsWith('')", true)]
    [Arguments("''.StartsWith('a')", false)]
    [Arguments("''.StartsWith('')", true)]
    [Arguments("StartsWith('привет','при')", true)]
    [Arguments("'😀👍👋'.StartsWith('😀')", true)]
    [Arguments("'Apple'.StartsWith('A')", true)]
    [Arguments("'Apple'.StartsWith('a')", false)]
    public Task FuncStartsWithTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("'hello'.EndsWith('llo')", true)]
    [Arguments("EndsWith('hello','world')", false)]
    [Arguments("'hello'.EndsWith('')", true)]
    [Arguments("''.EndsWith('a')", false)]
    [Arguments("''.EndsWith('')", true)]
    [Arguments("EndsWith('привет','вет')", true)]
    [Arguments("'😀👍👋'.EndsWith('👋')", true)]
    [Arguments("'Apple'.EndsWith('e')", true)]
    [Arguments("'Apple'.EndsWith('E')", false)]
    public Task FuncEndsWithTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }

    [Test]
    [Arguments("Chr(65)", "A")]
    [Arguments("Chr(97)", "a")]
    [Arguments("Chr(32)", " ")]
    [Arguments("Chr(9)", "\t")]
    [Arguments("Chr(10)", "\n")]
    [Arguments("Chr(13)", "\r")]
    [Arguments("Chr(0)", "\0")]
    [Arguments("Chr(255)", "ÿ")]
    [Arguments("Chr(1055)", "П")]
    [Arguments("Chr(1087)", "п")]
    [Arguments("Chr(1040)", "А")]
    [Arguments("Chr(1072)", "а")]
    [Arguments("Chr(128512)", "😀")]
    [Arguments("Chr(128077)", "👍")]
    [Arguments("Chr(128075)", "👋")]
    [Arguments("Chr(8364)", "€")]
    [Arguments("Chr(163)", "£")]
    [Arguments("Chr(165)", "¥")]
    [Arguments("Chr(169)", "©")]
    [Arguments("Chr(-1)", null)]
    [Arguments("Chr(1114112)", null)]
    [Arguments("Chr(null)", null)]
    public Task FuncChrTests(string expr, object? expected)
    {
        return AssertExpressionAsync(expr, expected);
    }
}
