using System.Globalization;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Lang;

internal sealed partial class StatementParser : LangParserBaseVisitor<Statement>
{
    private readonly ExpressionParser expressionParser = new();

    public static ParseResult<Statement> Parse(string text)
    {
        try
        {
            var parser = CreateParser(text);
            var statement = new StatementParser().VisitFull_statement(parser.full_statement());
            return ParseResult<Statement>.Success(statement);
        }
        catch (LangErrorException ex)
        {
            return ParseResult<Statement>.Failure(ex.Error);
        }
        catch (Exception ex)
        {
            return ParseResult<Statement>.Failure(new LangError
            {
                Span = new LangSpan(1, 1, 100, 100),
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Корневой statement parser.
    /// Пример: <c>LOAD * FROM [orders.csv];</c>
    /// </summary>
    public override Statement VisitFull_statement(LangParser.Full_statementContext context)
    {
        // 1. Отбрасываем EOF-обертку.
        return Visit(context.statement());
    }

    /// <summary>
    /// Диспетчер statement.
    /// Пример: <c>LOAD amount AS amount FROM [orders.csv];</c>
    /// </summary>
    public override Statement VisitStatement(LangParser.StatementContext context)
    {
        // 1. Пока в языке есть только LOAD statement.
        return Visit(context.load_statement());
    }

    /// <summary>
    /// LOAD statement целиком.
    /// Пример: <c>LOAD amount AS amount, city FROM [orders.csv] (csv, delimiter=',');</c>
    /// </summary>
    public override Statement VisitLoad_statement(LangParser.Load_statementContext context)
    {
        // 1. Разбираем поля LOAD: либо "*", либо список полей.
        var fields = VisitLoadFields(context.load_fields());

        // 2. Source хранится как blocked name, поэтому снимаем квадратные скобки и escape.
        var source = UnescapeName(context.BLOCKED_NAME().GetText());

        // 3. Options необязательны: FROM [x] и FROM [x] (...) обе формы валидны.
        var options = VisitSourceOptions(context.source_options());

        // 4. WHERE необязателен и хранится как обычное expression tree.
        var where = VisitLoadWhere(context.load_where());

        // 5. GROUP BY необязателен и хранит список expression группировки.
        var groupBy = VisitLoadGroupBy(context.load_group_by());

        // 6. ORDER BY необязателен и хранит список expression с направлением сортировки.
        var orderBy = VisitLoadOrderBy(context.load_order_by());

        // 7. LIMIT/OFFSET необязательны и специально ограничены integer literal, как в SQL-форме LIMIT 10 OFFSET 20.
        var limit = VisitLoadLimit(context.load_limit());
        var offset = VisitLoadOffset(context.load_limit()?.load_offset());

        return new LoadStatement
        {
            Fields = fields,
            Source = source,
            Options = options,
            Where = where,
            GroupBy = groupBy,
            OrderBy = orderBy,
            Limit = limit,
            Offset = offset
        };
    }

    /// <summary>
    /// Список полей LOAD.
    /// Примеры: <c>*</c>, <c>amount AS amount, city,</c>.
    /// </summary>
    private List<LoadField>? VisitLoadFields(LangParser.Load_fieldsContext context)
    {
        // 1. LOAD * не содержит явных field expressions.
        if (context.load_all_fields() is not null)
        {
            return null;
        }

        // 2. Для явного списка сохраняем порядок полей из script.
        return context.load_field().Select(VisitLoadField).ToList();
    }

    /// <summary>
    /// Одно поле LOAD.
    /// Примеры: <c>amount * 1.2 AS gross_amount</c>, <c>city</c>.
    /// </summary>
    private LoadField VisitLoadField(LangParser.Load_fieldContext context)
    {
        // 1. Короткая форма "LOAD id" на уровне парсинга превращается в "LOAD id AS id".
        if (context.expr() is null)
        {
            var fieldName = UnescapeName(context.name().GetText());
            return new LoadField
            {
                Name = fieldName,
                Expression = new NameExpr(fieldName)
                {
                    Span = Span(context.name())
                }
            };
        }

        // 2. Полная форма "expr AS name" разбирает expression обычным expression visitor.
        var expression = expressionParser.Visit(context.expr());

        // 3. Alias может быть обычным или blocked name.
        var name = UnescapeName(context.name().GetText());

        return new LoadField
        {
            Name = name,
            Expression = expression
        };
    }

    /// <summary>
    /// Source options после FROM.
    /// Пример: <c>(csv, delimiter=',', header=true)</c>.
    /// </summary>
    private List<LoadOption> VisitSourceOptions(LangParser.Source_optionsContext? context)
    {
        // 1. Отсутствующий options block означает пустой список options.
        if (context?.option_list() is null)
        {
            return [];
        }

        // 2. Options сохраняем в исходном порядке, чтобы provider resolver мог читать marker первым.
        return context.option_list().load_option().Select(VisitLoadOption).ToList();
    }

    /// <summary>
    /// Optional WHERE part of LOAD.
    /// Пример: <c>WHERE amount &gt; 0 AND active</c>.
    /// </summary>
    private Expr? VisitLoadWhere(LangParser.Load_whereContext? context)
    {
        // 1. WHERE отсутствует: LOAD читает все строки source.
        if (context is null)
        {
            return null;
        }

        // 2. WHERE expression разбирается тем же expression visitor, что и поля LOAD.
        return expressionParser.Visit(context.expr());
    }

    /// <summary>
    /// Optional GROUP BY part of LOAD.
    /// Пример: <c>GROUP BY city, created.Date()</c>.
    /// </summary>
    private List<Expr> VisitLoadGroupBy(LangParser.Load_group_byContext? context)
    {
        // 1. GROUP BY отсутствует: LOAD не выполняет группировку.
        if (context is null)
        {
            return [];
        }

        // 2. Expressions группировки сохраняются в исходном порядке.
        return context.expr().Select(expressionParser.Visit).ToList();
    }

    /// <summary>
    /// Optional ORDER BY part of LOAD.
    /// Пример: <c>ORDER BY amount DESC, city ASC</c>.
    /// </summary>
    private List<LoadOrderField> VisitLoadOrderBy(LangParser.Load_order_byContext? context)
    {
        // 1. ORDER BY отсутствует: порядок строк остается provider/source-native.
        if (context is null)
        {
            return [];
        }

        // 2. Поля сортировки сохраняются в исходном порядке.
        return context.order_by_field().Select(VisitOrderByField).ToList();
    }

    /// <summary>
    /// Одно поле ORDER BY.
    /// Примеры: <c>amount</c>, <c>amount DESC</c>.
    /// </summary>
    private LoadOrderField VisitOrderByField(LangParser.Order_by_fieldContext context)
    {
        var direction = context.order_direction()?.DESC() is not null
            ? LoadOrderDirection.Descending
            : LoadOrderDirection.Ascending;

        return new LoadOrderField
        {
            Expression = expressionParser.Visit(context.expr()),
            Direction = direction
        };
    }

    /// <summary>
    /// Optional LIMIT part of LOAD.
    /// Пример: <c>LIMIT 100</c>.
    /// </summary>
    private static long? VisitLoadLimit(LangParser.Load_limitContext? context)
    {
        // 1. LIMIT отсутствует: ограничение количества строк не задано.
        if (context is null)
        {
            return null;
        }

        // 2. LIMIT принимает только INTEGER, без expression, чтобы не смешивать синтаксис с вычислениями.
        return long.Parse(context.INTEGER().GetText(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Optional OFFSET part of LOAD.
    /// Пример: <c>OFFSET 100</c>.
    /// </summary>
    private static long? VisitLoadOffset(LangParser.Load_offsetContext? context)
    {
        // 1. OFFSET отсутствует: чтение начинается с первой строки результата.
        if (context is null)
        {
            return null;
        }

        // 2. OFFSET принимает только INTEGER и по грамматике разрешен только после LIMIT.
        return long.Parse(context.INTEGER().GetText(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Одна source option.
    /// Примеры: <c>csv</c>, <c>delimiter=','</c>, <c>header=true</c>.
    /// </summary>
    private LoadOption VisitLoadOption(LangParser.Load_optionContext context)
    {
        // 1. NAME всегда является именем option.
        var name = context.NAME().GetText();

        // 2. Value есть только у формы "name=value"; marker option вроде "csv" остается без value.
        var value = context.option_literal() is null
            ? null
            : VisitOptionLiteral(context.option_literal());

        return new LoadOption
        {
            Name = name,
            Value = value
        };
    }

    /// <summary>
    /// Literal value внутри source option.
    /// Примеры: <c>','</c>, <c>true</c>, <c>125</c>, <c>10.5</c>.
    /// </summary>
    private Literal VisitOptionLiteral(LangParser.Option_literalContext context)
    {
        // 1. option_literal специально ограничен literal-ами без name/null.
        var literalContext = context.children.OfType<ParserRuleContext>().Single();

        // 2. Expression visitor уже умеет строить String/Integer/Number/Boolean literal.
        return (Literal)expressionParser.Visit(literalContext);
    }

    private static LangParser CreateParser(string text)
    {
        var input = new AntlrInputStream(text);
        var lexer = new LangLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new TokenErrorListener());

        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        var parser = new LangParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ErrorListener());
        return parser;
    }

    private static LangSpan Span(ParserRuleContext context)
    {
        return new LangSpan(
            (uint)context.Start.Line,
            (uint)context.Start.Column,
            (uint)context.Stop.Line,
            (uint)(context.Stop.Column + context.Stop.Text.Length));
    }

    [GeneratedRegex(@"\\\]")]
    private static partial Regex EscapeRegex();

    private static string UnescapeName(string name)
    {
        if (name[0] == '[' && name[^1] == ']')
        {
            name = name[1..^1];
        }

        return EscapeRegex().Replace(name, "]");
    }
}
