using Loader.Lang.Statements;

namespace Loader.Lang.Tests;

public sealed class CalendarParsingTests
{
    [Test]
    [Arguments("Calendar: CALENDAR FROM '2024-01-01' TO '2024-12-31';")]
    [Arguments("Calendar:calendar from'2024-01-01'to'2024-12-31';")]
    [Arguments("Calendar : CaLeNdAr\r\nFrOm '2024-01-01'\nTo '2024-12-31' ;")]
    [DisplayName("CALENDAR с явным диапазоном не зависит от пробелов и регистра keywords")]
    public async Task Calendar_literal_range_parses(string text)
    {
        var statement = ParseCalendar(text);

        await Assert.That(statement.TableName).IsEqualTo("Calendar");
        await Assert.That(statement.Range).IsTypeOf<CalendarLiteralRange>();
        var range = (CalendarLiteralRange)statement.Range;
        await Assert.That(range.StartDate).IsEqualTo(new DateOnly(2024, 1, 1));
        await Assert.That(range.EndDate).IsEqualTo(new DateOnly(2024, 12, 31));
    }

    [Test]
    [Arguments(
        "Calendar: CALENDAR FROM FIELD CreatedAt RESIDENT Orders;",
        "CreatedAt",
        "Orders")]
    [Arguments(
        @"Calendar: CALENDAR FROM FIELD [Created at] RESIDENT [Order facts];",
        "Created at",
        "Order facts")]
    [Arguments(
        @"Calendar: CALENDAR FROM FIELD [Created\]At] RESIDENT [Orders\]2024];",
        "Created]At",
        "Orders]2024")]
    [DisplayName("CALENDAR RESIDENT сохраняет логические имена поля и таблицы")]
    public async Task Calendar_resident_range_parses(
        string text,
        string expectedField,
        string expectedTable)
    {
        var statement = ParseCalendar(text);

        await Assert.That(statement.TableName).IsEqualTo("Calendar");
        await Assert.That(statement.Range).IsTypeOf<CalendarResidentRange>();
        var range = (CalendarResidentRange)statement.Range;
        await Assert.That(range.FieldName).IsEqualTo(expectedField);
        await Assert.That(range.TableName).IsEqualTo(expectedTable);
    }

    [Test]
    [Arguments("CALENDAR FROM '2024-01-01' TO '2024-12-31';")]
    [Arguments("Calendar: CALENDAR FROM '01.01.2024' TO '2024-12-31';")]
    [Arguments("Calendar: CALENDAR FROM '2024-01-01' TO '2024-02-30';")]
    [Arguments("Calendar: CALENDAR FROM '${StartDate}' TO '2024-12-31';")]
    [Arguments("Calendar: CALENDAR FROM FIELD CreatedAt Orders;")]
    [Arguments("Calendar: CALENDAR FROM FIELD CreatedAt RESIDENT Orders")]
    [DisplayName("CALENDAR отклоняет неполный синтаксис и не-ISO даты")]
    public async Task Calendar_rejects_invalid_syntax(string text)
    {
        var result = Statement.Parse(text);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Message).IsNotEmpty();
    }

    [Test]
    [DisplayName("Script.Parse сохраняет порядок LOAD и CALENDAR")]
    public async Task Script_parses_load_followed_by_calendar()
    {
        var result = Script.Parse(
            """
            Orders:
            LOAD CreatedAt FROM [orders.csv];

            Calendar:
            CALENDAR FROM FIELD CreatedAt RESIDENT Orders;
            """);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Statements).Count().IsEqualTo(2);
        await Assert.That(result.Value.Statements[0]).IsTypeOf<LoadStatement>();
        await Assert.That(result.Value.Statements[1]).IsTypeOf<CalendarStatement>();
    }

    private static CalendarStatement ParseCalendar(string text)
    {
        var result = Statement.Parse(text);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        return (CalendarStatement)result.Value;
    }
}
