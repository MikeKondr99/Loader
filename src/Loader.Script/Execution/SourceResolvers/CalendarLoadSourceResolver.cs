using System.Globalization;
using Loader.Core.Models;
using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Calendar</c>. Создает календарную таблицу из явного диапазона или диапазона уже загруженной таблицы.
/// Параметры:
/// min: Text - начальная дата в формате <c>yyyy-MM-dd</c> для явного диапазона.
/// max: Text - конечная дата в формате <c>yyyy-MM-dd</c> для явного диапазона.
/// table: Name - имя уже загруженной таблицы, из которой нужно взять диапазон дат.
/// field: Name - поле Date/DateTime в таблице <c>table</c>.
/// </summary>
internal sealed class CalendarLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Calendar";

    public override ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = MapPositionals(options);
        RejectUnknownOptions(Name, options, errors, ["min", "max", "table", "field"]);
        RejectSql(statement, errors);

        var hasMin = options.GetOption("min") is not null;
        var hasMax = options.GetOption("max") is not null;
        var hasTable = options.GetOption("table") is not null;
        var hasField = options.GetOption("field") is not null;
        var usesExplicitRange = hasMin || hasMax;
        var usesTableRange = hasTable || hasField;

        if (usesExplicitRange && usesTableRange)
        {
            errors.Add(new LangError
            {
                Message = "Provider 'Calendar' принимает либо min/max, либо table/field, но не оба режима одновременно.",
                Span = statement.SourceCall.Span
            });
        }

        string? sql = null;
        if (!usesExplicitRange && !usesTableRange)
        {
            errors.Add(new LangError
            {
                Message = "Для provider-а Calendar требуется min/max или table/field.",
                Span = statement.SourceCall.Span
            });
        }
        else if (usesExplicitRange)
        {
            sql = ResolveExplicitRangeSql(statement, options, errors);
        }
        else
        {
            sql = ResolveTableRangeSql(statement, context, options, errors);
        }

        if (sql is null || errors.Count > 0)
        {
            return Error();
        }
        return ValueTask.FromResult<LoadFromSource>(new SqlLoadFromSource
        {
            Sql = $"({sql})",
            Fields = CalendarSqlBuilder.Fields.Select(field => new LoadFromSqlField
            {
                Name = field.Name,
                PhysicalName = field.Name,
                DataType = field.DataType,
                CanBeNull = field.CanBeNull
            }).ToArray()
        });
    }

    private static LoadOptionReader MapPositionals(LoadOptionReader options)
    {
        if (options.PositionalCount != 2)
        {
            return options.MapPositionals("Calendar", ["max"]);
        }

        var positionals = options.PositionalOptions();
        if (positionals is [LoadOption { Value: NameLiteral }, LoadOption { Value: NameLiteral }])
        {
            return options.MapPositionals("Calendar", ["table", "field"]);
        }

        return options.MapPositionals("Calendar", ["min", "max"]);
    }

    private static string? ResolveExplicitRangeSql(
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var min = RequiredDate("min", statement, options, errors);
        var max = RequiredDate("max", statement, options, errors);
        if (min is null || max is null)
        {
            return null;
        }

        ValidateRange(min.Value, max.Value, options.GetOption("min")?.Span ?? statement.SourceCall.Span, errors);
        return CalendarSqlBuilder.BuildExplicitRangeSql(min.Value, max.Value);
    }

    private static string? ResolveTableRangeSql(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var tableAlias = options.RequiredName(
            "table",
            statement.SourceCall.Span,
            "Для provider-а Calendar в режиме table/field требуется option table=table_name.");
        var fieldName = options.RequiredName(
            "field",
            statement.SourceCall.Span,
            "Для provider-а Calendar в режиме table/field требуется option field=field_name.");

        if (tableAlias is null || fieldName is null)
        {
            return null;
        }

        var table = context.FindLoadedTable(tableAlias);
        if (table is null)
        {
            errors.Add(new LangError
            {
                Message = $"Таблица '{tableAlias}' не найдена среди уже загруженных LOAD таблиц.",
                Span = options.GetOption("table")?.Span ?? statement.SourceCall.Span
            });
            return null;
        }

        var fieldIndex = table.Fields.FindIndex(field => string.Equals(field.Name, fieldName, StringComparison.Ordinal));
        if (fieldIndex < 0)
        {
            errors.Add(new LangError
            {
                Message = $"Поле '{fieldName}' не найдено в таблице '{tableAlias}'.",
                Span = options.GetOption("field")?.Span ?? statement.SourceCall.Span
            });
            return null;
        }

        var field = table.Fields[fieldIndex];
        if (field.DataType is not (DataType.Date or DataType.DateTime))
        {
            errors.Add(new LangError
            {
                Message = $"Поле '{fieldName}' в таблице '{tableAlias}' должно иметь тип Date или DateTime.",
                Span = options.GetOption("field")?.Span ?? statement.SourceCall.Span
            });
            return null;
        }

        return CalendarSqlBuilder.BuildLoadedTableRangeSql(table.Name, $"column{fieldIndex + 1}");
    }

    private static DateOnly? RequiredDate(
        string name,
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var text = options.RequiredString(
            name,
            statement.SourceCall.Span,
            $"Для provider-а Calendar требуется option {name}='yyyy-MM-dd'.");
        if (text is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            return value;
        }

        errors.Add(new LangError
        {
            Message = $"Опция '{name}' должна быть датой в формате yyyy-MM-dd.",
            Span = options.GetOption(name)?.Span ?? statement.SourceCall.Span
        });
        return null;
    }

    private static void ValidateRange(
        DateOnly min,
        DateOnly max,
        LangSpan span,
        List<LangError> errors)
    {
        if (max < min)
        {
            errors.Add(new LangError
            {
                Message = "Опция 'max' должна быть больше или равна 'min'.",
                Span = span
            });
        }

        if (min < CalendarSqlBuilder.MinSupportedDate || max > CalendarSqlBuilder.MaxSupportedDate)
        {
            errors.Add(new LangError
            {
                Message = $"Диапазон Calendar должен помещаться в безопасный диапазон ClickHouse Date для всех календарных полей: {CalendarSqlBuilder.MinSupportedDate:yyyy-MM-dd}..{CalendarSqlBuilder.MaxSupportedDate:yyyy-MM-dd}.",
                Span = span
            });
        }
    }

    private static void RejectSql(LoadStatement statement, List<LangError> errors)
    {
        if (statement.SqlPart is null)
        {
            return;
        }

        errors.Add(new LangError
        {
            Message = "Provider 'Calendar' не поддерживает SQL после FROM.",
            Span = statement.SqlPart.Span
        });
    }
}
