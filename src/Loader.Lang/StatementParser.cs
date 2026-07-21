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

        return new LoadStatement
        {
            Fields = fields,
            Source = source,
            Options = options
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
