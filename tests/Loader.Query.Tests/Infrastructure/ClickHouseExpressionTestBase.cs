using System.Data.Common;
using System.Globalization;
using Loader.Core.Decorators;
using Loader.Core.Models;
using Loader.Core.Providers;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Lang.Expressions;
using Loader.Query.Compile;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;

namespace Loader.Query.Tests.Infrastructure;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerAssembly)]
[NotInParallel("ClickHouse")]
public abstract class ClickHouseExpressionTestBase
{
    private static readonly ClickHouseProvider Provider = new();
    private readonly ClickHouseTestDatabase database;

    protected ClickHouseExpressionTestBase(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    protected async Task AssertExpressionAsync(string expressionText, object? expected)
    {
        var sql = CompileExpression(expressionText);
        await using var rawReader = await OpenReaderAsync($"select {sql} as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        var value = reader.GetValue(0);
        if (expected is null)
        {
            await Assert.That(value).IsEqualTo(DBNull.Value);
            return;
        }

        if (expected is string { Length: > 1 } text && text[0] == '@')
        {
            await AssertDateAsync(value, text[1..]);
            return;
        }

        if (expected is bool expectedBool)
        {
            await AssertBooleanAsync(value, expectedBool);
            return;
        }

        if (IsFloating(value) || IsFloating(expected))
        {
            var actual = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var expectedDouble = Convert.ToDouble(expected, CultureInfo.InvariantCulture);
            await Assert.That(actual).IsEqualTo(expectedDouble).Within(0.000001);
            return;
        }

        if (IsNumber(value) && IsNumber(expected))
        {
            await Assert.That(Convert.ToDecimal(value, CultureInfo.InvariantCulture))
                .IsEqualTo(Convert.ToDecimal(expected, CultureInfo.InvariantCulture));
            return;
        }

        await Assert.That(value).IsEqualTo(expected);
    }

    private static async Task AssertBooleanAsync(object value, bool expected)
    {
        var actual = value switch
        {
            bool boolValue => boolValue,
            byte byteValue => byteValue != 0,
            sbyte sbyteValue => sbyteValue != 0,
            short shortValue => shortValue != 0,
            ushort ushortValue => ushortValue != 0,
            int intValue => intValue != 0,
            uint uintValue => uintValue != 0,
            long longValue => longValue != 0,
            ulong ulongValue => ulongValue != 0,
            _ => throw new InvalidOperationException($"Expected boolean-like value, got '{value.GetType()}'.")
        };

        await Assert.That(actual).IsEqualTo(expected);
    }

    protected async Task<string> GetStringAsync(string expressionText)
    {
        var sql = CompileExpression(expressionText);
        await using var rawReader = await OpenReaderAsync($"select {sql} as value");
        await using var reader = rawReader.Normalize();

        if (!reader.Read())
        {
            throw new InvalidOperationException("Expression query returned no rows.");
        }

        return reader.GetString(0);
    }

    protected async Task<object?> GetScalarAsync(Query.Models.Query query)
    {
        var rows = await GetRowsAsync(query).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Query returned no rows.");
        }

        return rows[0].Values.First();
    }

    protected async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetRowsAsync(Query.Models.Query query)
    {
        var sql = CompileQuery(query);
        await using var rawReader = await OpenReaderAsync(sql).ConfigureAwait(false);
        await using var reader = rawReader.Normalize();

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                var value = reader.GetValue(ordinal);
                row[reader.GetName(ordinal)] = value == DBNull.Value ? null : value;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task AssertDateAsync(object value, string expectedText)
    {
        var expected = DateTime.Parse(expectedText, CultureInfo.InvariantCulture);
        var actual = value switch
        {
            DateTime dateTime => dateTime,
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            _ => throw new InvalidOperationException($"Expected date-like value, got '{value.GetType()}'.")
        };

        await Assert.That(actual).IsEqualTo(expected);
    }

    private static bool IsNumber(object? value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private static bool IsFloating(object? value)
    {
        return value is float or double;
    }

    private static string CompileExpression(string expressionText)
    {
        var expression = Expr.Parse(expressionText).Value;
        var context = new ResolutionContext
        {
            Source = new QuerySource
            {
                Sql = "stage",
                Alias = "stage",
                Fields = []
            },
            Functions = ClickHouseFunctions.CreateResolver(),
            Errors = []
        };
        var resolved = new ExpressionResolver().Resolve(expression, context);
        if (resolved is null)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, context.Errors.Select(error => error.Message)));
        }

        return new ExpressionCompiler().Compile(resolved);
    }

    private static string CompileQuery(Query.Models.Query query)
    {
        var result = new QueryResolver().Resolve(query, ClickHouseFunctions.CreateResolver());
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        return new ClickHouseQueryCompiler
        {
            ExpressionCompiler = new ExpressionCompiler()
        }.Compile(result.Value!);
    }

    private ValueTask<DbDataReader> OpenReaderAsync(string sql)
    {
        return Provider.OpenReaderAsync(
            new ConnectionStringSource
            {
                ConnectionString = database.ConnectionString
            },
            new SqlTableConfig
            {
                Sql = sql
            });
    }
}
