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
    /// Пример: <c>LOAD * FROM Csv(path='orders.csv');</c>
    /// </summary>
    public override Statement VisitFull_statement(LangParser.Full_statementContext context)
    {
        // 1. Отбрасываем EOF-обертку.
        return Visit(context.statement());
    }

    /// <summary>
    /// Диспетчер statement.
    /// Пример: <c>LOAD amount AS amount FROM Csv(path='orders.csv');</c>
    /// </summary>
    public override Statement VisitStatement(LangParser.StatementContext context)
    {
        return Visit(context.GetChild(0));
    }

    public override Statement VisitDrop_statement(LangParser.Drop_statementContext context)
    {
        return new DropStatement
        {
            DropSpan = Span(context.DROP()),
            Name = UnescapeName(context.name().GetText()),
            NameSpan = Span(context.name())
        };
    }

    /// <summary>
    /// LOAD statement целиком.
    /// Пример: <c>LOAD amount AS amount, city FROM Csv(path='orders.csv', delimiter=',');</c>
    /// </summary>
    public override Statement VisitLoad_statement(LangParser.Load_statementContext context)
    {
        // 1. Table name необязателен и задается только обычным NAME перед LOAD: "orders: LOAD ...".
        var tableName = VisitLoadTableName(context.load_table_name());

        // 2. Разбираем поля LOAD: либо "*", либо список полей.
        var fields = VisitLoadFields(context.load_fields());

        // 3. Source хранится как provider call: Csv(path='orders.csv').
        var sourceCall = VisitLoadSource(context.load_source());

        // 5. SQL source query необязателен и взаимоисключен с LOAD-level WHERE/GROUP/ORDER/LIMIT.
        var sql = VisitLoadSql(context.load_sql());

        // 6. WHERE необязателен и хранится как обычное expression tree.
        var where = VisitLoadWhere(context.load_where());

        // 7. GROUP BY необязателен и хранит список expression группировки.
        var groupBy = VisitLoadGroupBy(context.load_group_by());

        // 8. ORDER BY необязателен и хранит список expression с направлением сортировки.
        var orderBy = VisitLoadOrderBy(context.load_order_by());

        // 9. LIMIT/OFFSET необязательны и специально ограничены integer literal, как в SQL-форме LIMIT 10 OFFSET 20.
        var limitContext = context.load_limit();
        var limit = VisitLoadLimit(limitContext);
        var offset = VisitLoadOffset(limitContext?.load_offset());

        return new LoadStatement
        {
            LoadSpan = Span(context.LOAD()),
            TableName = tableName,
            Fields = fields,
            FromSpan = Span(context.FROM()),
            SourceCall = sourceCall,
            SqlPart = sql,
            Where = where,
            WhereSpan = context.load_where() is null ? null : Span(context.load_where().WHERE()),
            GroupBy = groupBy,
            GroupBySpan = context.load_group_by() is null
                ? null
                : Span(context.load_group_by().GROUP().Symbol, context.load_group_by().BY().Symbol),
            OrderBy = orderBy,
            OrderBySpan = context.load_order_by() is null
                ? null
                : Span(context.load_order_by().ORDER().Symbol, context.load_order_by().BY().Symbol),
            LimitPart = limit,
            Offset = offset,
            OffsetSpan = limitContext?.load_offset() is null ? null : Span(limitContext.load_offset().OFFSET())
        };
    }

    /// <summary>
    /// Optional source SQL after FROM.
    /// Пример: <c>SQL SELECT * FROM public.orders WHERE amount &gt; 0</c>.
    /// </summary>
    private static SqlPart? VisitLoadSql(LangParser.Load_sqlContext? context)
    {
        // 1. SQL отсутствует: source задается provider-specific options.
        if (context is null)
        {
            return null;
        }

        // 2. Lexer mode отдает весь текст до ; как SQL_TEXT, не пытаясь парсить SQL нашим языком.
        var text = context.SQL_TEXT()?.GetText().Trim() ?? string.Empty;
        return new SqlPart
        {
            Value = text,
            Span = Span(context)
        };
    }

    /// <summary>
    /// Table name prefix before LOAD.
    /// Пример: <c>orders: LOAD * FROM Csv(path='orders.csv');</c>.
    /// </summary>
    private static string VisitLoadTableName(LangParser.Load_table_nameContext context)
    {
        // 1. Имя таблицы может быть обычным NAME или blocked name: "[table name]".
        return UnescapeName(context.name().GetText());
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
                Span = Span(context.name()),
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
            Span = Span(context.name()),
            Expression = expression
        };
    }

    private LoadSourceCall VisitLoadSource(LangParser.Load_sourceContext context)
    {
        if (context.source_call() is { } sourceCall)
        {
            return VisitSourceCall(sourceCall);
        }

        return VisitSourceTable(context.source_table());
    }

    private static LoadSourceCall VisitSourceTable(LangParser.Source_tableContext context)
    {
        var nameSpan = Span(context.name());
        var tableName = UnescapeName(context.name().GetText());
        return new LoadSourceCall
        {
            Name = "Table",
            NameSpan = nameSpan,
            Options =
            [
                new LoadOption
                {
                    Name = "name",
                    Span = nameSpan,
                    Value = new StringLiteral(tableName, nameSpan)
                }
            ],
            Span = Span(context)
        };
    }

    private LoadSourceCall VisitSourceCall(LangParser.Source_callContext context)
    {
        return new LoadSourceCall
        {
            Name = context.NAME().GetText(),
            NameSpan = Span(context.NAME()),
            Options = VisitSourceOptions(context.option_list()),
            InlineData = VisitInlineData(context.inline_data()),
            Span = Span(context)
        };
    }

    private InlineData? VisitInlineData(LangParser.Inline_dataContext? context)
    {
        if (context is null)
        {
            return null;
        }

        return new InlineData
        {
            Columns = context.inline_header().name().Select(name => new InlineColumn
            {
                Name = UnescapeName(name.GetText()),
                Span = Span(name)
            }).ToArray(),
            Rows = context.inline_row().Select(row => new InlineRow
            {
                Values = row.inline_value().Select(VisitInlineValue).ToArray(),
                Span = Span(row)
            }).ToArray(),
            Span = Span(context)
        };
    }

    private Literal VisitInlineValue(LangParser.Inline_valueContext context)
    {
        if (context.inline_integer() is { } integer)
        {
            var value = long.Parse(integer.INTEGER().GetText(), CultureInfo.InvariantCulture);
            if (integer.MINUS() is not null)
            {
                value = -value;
            }

            return new IntegerLiteral(value)
            {
                Span = Span(integer)
            };
        }

        if (context.inline_number() is { } number)
        {
            var value = double.Parse(number.NUMBER().GetText(), CultureInfo.InvariantCulture);
            if (number.MINUS() is not null)
            {
                value = -value;
            }

            return new NumberLiteral(value)
            {
                Span = Span(number)
            };
        }

        var literalContext = context.children.OfType<ParserRuleContext>().Single();
        return (Literal)expressionParser.Visit(literalContext);
    }

    /// <summary>
    /// Source options внутри provider call.
    /// Пример: <c>path='orders.csv', delimiter=',', header=true</c>.
    /// </summary>
    private List<LoadOption> VisitSourceOptions(LangParser.Option_listContext? context)
    {
        // 1. Пустой provider call означает пустой список options.
        if (context is null)
        {
            return [];
        }

        // 2. Options сохраняем в исходном порядке для диагностики duplicate options и span-ов.
        return context.load_option().Select(VisitLoadOption).ToList();
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
    private List<Expr>? VisitLoadGroupBy(LangParser.Load_group_byContext? context)
    {
        // 1. GROUP BY отсутствует: LOAD не выполняет группировку.
        if (context is null)
        {
            return null;
        }

        // 2. Expressions группировки сохраняются в исходном порядке.
        return context.expr().Select(expressionParser.Visit).ToList();
    }

    /// <summary>
    /// Optional ORDER BY part of LOAD.
    /// Пример: <c>ORDER BY amount DESC, city ASC</c>.
    /// </summary>
    private List<LoadOrderField>? VisitLoadOrderBy(LangParser.Load_order_byContext? context)
    {
        // 1. ORDER BY отсутствует: порядок строк остается provider/source-native.
        if (context is null)
        {
            return null;
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
    private static LimitPart? VisitLoadLimit(LangParser.Load_limitContext? context)
    {
        // 1. LIMIT отсутствует: ограничение количества строк не задано.
        if (context is null)
        {
            return null;
        }

        // 2. LIMIT принимает только INTEGER, без expression, чтобы не смешивать синтаксис с вычислениями.
        return new LimitPart
        {
            Value = long.Parse(context.INTEGER().GetText(), CultureInfo.InvariantCulture),
            Span = Span(context)
        };
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
    /// Примеры: <c>path='orders.csv'</c>, <c>delimiter=','</c>, <c>header=true</c>.
    /// </summary>
    private LoadOption VisitLoadOption(LangParser.Load_optionContext context)
    {
        // 1. NAME всегда является именем option.
        var name = context.NAME().GetText();

        return new LoadOption
        {
            Name = name,
            Span = Span(context),
            Value = VisitOptionLiteral(context.option_literal())
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
        return Span(context.Start, context.Stop);
    }

    private static LangSpan Span(ITerminalNode node)
    {
        var token = node.Symbol;
        return Span(token, token);
    }

    private static LangSpan Span(IToken start, IToken stop)
    {
        var endRow = stop.Line;
        var endColumn = stop.Column;
        foreach (var character in stop.Text)
        {
            if (character == '\n')
            {
                endRow++;
                endColumn = 0;
                continue;
            }

            if (character != '\r')
            {
                endColumn++;
            }
        }

        return new LangSpan(
            (uint)start.Line,
            (uint)start.Column,
            (uint)endRow,
            (uint)endColumn);
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
