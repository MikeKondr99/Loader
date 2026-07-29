using Loader.Core.Models;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;
using Loader.Script.Execution;
using Loader.Script.Execution.Calendar;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loader.Script.Tests;

public sealed class CalendarStatementTests
{
    private static readonly string[] ExpectedFieldNames =
    [
        "Date", "Year", "QuarterNumber", "Quarter", "YearQuarterNumber", "YearQuarter",
        "MonthNumber", "MonthName", "MonthShortName", "YearMonthNumber", "YearMonth",
        "MonthYear", "WeekNumber", "YearWeek", "StartOfWeek", "LastDayOfWeek",
        "DayOfWeek", "DayOfWeekName", "DayOfMonth", "DayOfYear", "StartOfYear",
        "EndOfYear", "StartOfQuarter", "EndOfQuarter", "StartOfMonth", "EndOfMonth",
        "DayMonth", "WeekPeriod"
    ];

    private static readonly DataType[] ExpectedFieldTypes =
    [
        DataType.Date,
        DataType.Integer,
        DataType.Integer,
        DataType.Text,
        DataType.Integer,
        DataType.Text,
        DataType.Integer,
        DataType.Text,
        DataType.Text,
        DataType.Integer,
        DataType.Text,
        DataType.Text,
        DataType.Integer,
        DataType.Integer,
        DataType.Date,
        DataType.Date,
        DataType.Integer,
        DataType.Text,
        DataType.Integer,
        DataType.Integer,
        DataType.Date,
        DataType.Date,
        DataType.Date,
        DataType.Date,
        DataType.Date,
        DataType.Date,
        DataType.Text,
        DataType.Text
    ];

    [Test]
    public async Task Literal_calendar_materializes_inclusive_range_and_registers_fixed_metadata()
    {
        var executor = new TestCalendarStatementExecutor
        {
            FinalTablePrefix = "calendar_"
        };
        var context = CreateContext();
        var statement = LiteralStatement(
            new DateOnly(2024, 2, 28),
            new DateOnly(2024, 3, 1));

        var result = await executor.ExecuteAsync(context, statement);

        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
        await Assert.That(executor.DropCalls).IsEqualTo(0);
        await Assert.That(executor.FinalTable!.Table).StartsWith("calendar_");
        await Assert.That(executor.CreateSql).Contains("ENGINE = MergeTree");
        await Assert.That(executor.CreateSql).Contains("ORDER BY `column1`");
        await Assert.That(executor.CreateSql).Contains("FROM numbers(");
        await Assert.That(executor.CreateSql).Contains("dateDiff('day'");
        await Assert.That(executor.CreateSql).DoesNotContain("arrayJoin");
        await Assert.That(result.Alias).IsEqualTo("Calendar");
        await Assert.That(result.RowCount).IsEqualTo(3);
        await Assert.That(result.Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(ExpectedFieldNames, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Fields.Select(static field => field.DataType).ToArray())
            .IsEquivalentTo(ExpectedFieldTypes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Fields).Count().IsEqualTo(28);
        await Assert.That(result.Fields.All(static field => !field.CanBeNull)).IsTrue();
        await Assert.That(result.Fields[0].DataType).IsEqualTo(DataType.Date);
        await Assert.That(result.Fields[0].GetMin<DateOnly>()).IsEqualTo(new DateOnly(2024, 2, 28));
        await Assert.That(result.Fields[0].GetMax<DateOnly>()).IsEqualTo(new DateOnly(2024, 3, 1));
        await Assert.That(result.Fields[1].DataType).IsEqualTo(DataType.Integer);
        await Assert.That(result.Fields[3].DataType).IsEqualTo(DataType.Text);
        await Assert.That(context.LoadedTables).Count().IsEqualTo(1);
        await Assert.That(context.LoadedTables[0]).IsSameReferenceAs(result);
    }

    [Test]
    public async Task Resident_calendar_uses_prior_table_field_ordinal_and_date_range()
    {
        var executor = new TestCalendarStatementExecutor
        {
            ResidentRange = (new DateOnly(2024, 1, 2), new DateOnly(2024, 1, 5))
        };
        var context = CreateContext();
        context.AddLoadedTable(Table(
            "Orders",
            ("Id", DataType.Integer),
            ("CreatedAt", DataType.DateTime)));
        var statement = ResidentStatement("Orders", "CreatedAt");

        var result = await executor.ExecuteAsync(context, statement);

        await Assert.That(executor.ReadResidentCalls).IsEqualTo(1);
        await Assert.That(executor.ResidentTable!.Alias).IsEqualTo("Orders");
        await Assert.That(executor.ResidentPhysicalField).IsEqualTo("column2");
        await Assert.That(result.RowCount).IsEqualTo(4);
        await Assert.That(context.LoadedTables).Count().IsEqualTo(2);
    }

    [Test]
    [Arguments("Text")]
    [Arguments("Integer")]
    public async Task Resident_calendar_rejects_non_date_field(string dataType)
    {
        var executor = new TestCalendarStatementExecutor();
        var context = CreateContext();
        context.AddLoadedTable(Table(
            "Orders",
            ("CreatedAt", Enum.Parse<DataType>(dataType))));

        await Assert.That(async () => await executor.ExecuteAsync(
                context,
                ResidentStatement("Orders", "CreatedAt")))
            .ThrowsExactly<QueryResolutionException>()
            .WithMessageContaining("Date или DateTime");

        await Assert.That(executor.ReadResidentCalls).IsEqualTo(0);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Resident_calendar_rejects_empty_or_all_null_field()
    {
        var executor = new TestCalendarStatementExecutor
        {
            ResidentRange = (null, null)
        };
        var context = CreateContext();
        context.AddLoadedTable(Table("Orders", ("CreatedAt", DataType.Date)));

        await Assert.That(async () => await executor.ExecuteAsync(
                context,
                ResidentStatement("Orders", "CreatedAt")))
            .ThrowsExactly<QueryResolutionException>()
            .WithMessageContaining("не содержит ни одной даты");
    }

    [Test]
    public async Task Resident_calendar_rejects_missing_table()
    {
        await Assert.That(async () => await new TestCalendarStatementExecutor().ExecuteAsync(
                CreateContext(),
                ResidentStatement("Missing", "CreatedAt")))
            .ThrowsExactly<QueryResolutionException>()
            .WithMessageContaining("не найдена");
    }

    [Test]
    public async Task Resident_calendar_rejects_missing_or_ambiguous_field()
    {
        var missingContext = CreateContext();
        missingContext.AddLoadedTable(Table("Orders", ("Id", DataType.Integer)));
        await Assert.That(async () => await new TestCalendarStatementExecutor().ExecuteAsync(
                missingContext,
                ResidentStatement("Orders", "CreatedAt")))
            .ThrowsExactly<QueryResolutionException>()
            .WithMessageContaining("не найдено");

        var ambiguousContext = CreateContext();
        ambiguousContext.AddLoadedTable(Table(
            "Orders",
            ("CreatedAt", DataType.Date),
            ("CreatedAt", DataType.DateTime)));
        await Assert.That(async () => await new TestCalendarStatementExecutor().ExecuteAsync(
                ambiguousContext,
                ResidentStatement("Orders", "CreatedAt")))
            .ThrowsExactly<QueryResolutionException>()
            .WithMessageContaining("неоднозначно");
    }

    [Test]
    [Arguments("1969-12-31", "1970-01-01")]
    [Arguments("2149-06-06", "2149-06-07")]
    [Arguments("2024-02-02", "2024-02-01")]
    public async Task Calendar_rejects_invalid_clickhouse_date_range(string startText, string endText)
    {
        var start = DateOnly.ParseExact(startText, "yyyy-MM-dd");
        var end = DateOnly.ParseExact(endText, "yyyy-MM-dd");
        var executor = new TestCalendarStatementExecutor();

        await Assert.That(async () => await executor.ExecuteAsync(
                CreateContext(),
                LiteralStatement(start, end)))
            .ThrowsExactly<QueryResolutionException>();

        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Calendar_rolls_back_final_table_when_materialization_fails()
    {
        var executor = new TestCalendarStatementExecutor
        {
            ThrowOnMaterialize = true
        };
        var context = CreateContext();

        await Assert.That(async () => await executor.ExecuteAsync(
                context,
                LiteralStatement(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2))))
            .ThrowsExactly<FinalTableWriteException>();

        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
        await Assert.That(executor.DropCalls).IsEqualTo(1);
        await Assert.That(executor.DroppedTable).IsSameReferenceAs(executor.FinalTable);
        await Assert.That(context.LoadedTables).IsEmpty();
    }

    [Test]
    public async Task Script_executor_wraps_calendar_resolution_error_with_statement_index()
    {
        var script = new Loader.Lang.Script
        {
            Statements =
            [
                LiteralStatement(new DateOnly(2024, 2, 2), new DateOnly(2024, 2, 1))
            ]
        };

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                CalendarStatementExecutor = new TestCalendarStatementExecutor()
            }
            .ExecuteAsync(CreateContext(), script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.StatementType).IsEqualTo(nameof(CalendarStatement));
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
    }

    private static CalendarStatement LiteralStatement(DateOnly startDate, DateOnly endDate)
    {
        return new CalendarStatement
        {
            TableName = "Calendar",
            Range = new CalendarLiteralRange
            {
                StartDate = startDate,
                EndDate = endDate
            }
        };
    }

    private static CalendarStatement ResidentStatement(string tableName, string fieldName)
    {
        return new CalendarStatement
        {
            TableName = "Calendar",
            Range = new CalendarResidentRange
            {
                TableName = tableName,
                FieldName = fieldName
            }
        };
    }

    private static LoadedTable Table(string alias, params (string Name, DataType DataType)[] fields)
    {
        return new LoadedTable
        {
            Name = new ClickHouseTableName
            {
                Table = $"physical_{alias}"
            },
            Alias = alias,
            Fields = fields.Select(field => new LoadedTableField
            {
                Name = field.Name,
                DataType = field.DataType
            }).ToList()
        };
    }

    private static ScriptContext CreateContext()
    {
        return new ScriptContext
        {
            FileStorage = new StubFileSource(),
            TargetConnectionString = "Host=localhost",
            Logger = NullLogger.Instance
        };
    }

    private sealed class TestCalendarStatementExecutor : CalendarStatementExecutor
    {
        public (DateOnly? StartDate, DateOnly? EndDate) ResidentRange { get; init; } =
            (new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2));

        public int ReadResidentCalls { get; private set; }

        public LoadedTable? ResidentTable { get; private set; }

        public string? ResidentPhysicalField { get; private set; }

        public int MaterializeCalls { get; private set; }

        public string? CreateSql { get; private set; }

        public ClickHouseTableName? FinalTable { get; private set; }

        public bool ThrowOnMaterialize { get; init; }

        public int DropCalls { get; private set; }

        public ClickHouseTableName? DroppedTable { get; private set; }

        protected override ValueTask<(DateOnly? StartDate, DateOnly? EndDate)> ReadResidentRangeAsync(
            ScriptContext context,
            LoadedTable table,
            string physicalFieldName,
            CancellationToken cancellationToken)
        {
            ReadResidentCalls++;
            ResidentTable = table;
            ResidentPhysicalField = physicalFieldName;
            return ValueTask.FromResult(ResidentRange);
        }

        protected override ValueTask MaterializeFinalTableAsync(
            ScriptContext context,
            string createSql,
            ClickHouseTableName finalTable,
            CancellationToken cancellationToken)
        {
            MaterializeCalls++;
            CreateSql = createSql;
            FinalTable = finalTable;
            if (ThrowOnMaterialize)
            {
                throw new InvalidOperationException("materialize failed");
            }

            return ValueTask.CompletedTask;
        }

        protected override ValueTask DropFinalTableAsync(
            ScriptContext context,
            ClickHouseTableName finalTable,
            CancellationToken cancellationToken)
        {
            DropCalls++;
            DroppedTable = finalTable;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubFileSource : IFileSource
    {
        public Stream OpenRead(string fileName)
        {
            return new MemoryStream();
        }
    }
}
