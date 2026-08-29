using System.Data.Common;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Резолвит provider <c>Inline(...)</c> в reader без внешнего источника данных.
/// </summary>
internal sealed class InlineLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Inline";

    public override ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options.RejectUnknownOptions(Name, []);
        RejectSql(statement, errors);
        RejectTransformations(statement, errors);

        var inline = statement.SourceCall.InlineData;
        if (inline is null)
        {
            errors.Add(new LangError
            {
                Message = "Provider 'Inline' требует inline-данные: Inline(col1, col2; value1, value2).",
                Span = statement.SourceCall.Span
            });
            return Error();
        }

        ValidateColumns(inline, errors);
        ValidateRows(inline, errors);

        if (errors.Count > 0)
        {
            return Error();
        }

        return ValueTask.FromResult<LoadFromSource>(new ReaderLoadFromSource
        {
            RequiresBuffer = false,
            OpenReaderAsync = _ => ValueTask.FromResult<DbDataReader>(new InlineDataReader(inline))
        });
    }

    private static void RejectSql(LoadStatement statement, List<LangError> errors)
    {
        if (statement.SqlPart is null)
        {
            return;
        }

        errors.Add(new LangError
        {
            Message = "Provider 'Inline' не поддерживает SQL после FROM. Если нужны преобразования, загрузите Inline в таблицу и сделайте отдельный LOAD FROM этой таблицы.",
            Span = statement.SqlPart.Span
        });
    }

    private static void RejectTransformations(LoadStatement statement, List<LangError> errors)
    {
        AddError(statement.Where is null ? null : ("WHERE", statement.WhereSpan ?? statement.Where.Span), errors);
        AddError(statement.GroupBy is null ? null : ("GROUP BY", statement.GroupBySpan ?? statement.GroupBy[0].Span), errors);
        AddError(statement.OrderBy is null ? null : ("ORDER BY", statement.OrderBySpan ?? statement.OrderBy[0].Expression.Span), errors);
        AddError(statement.LimitPart is null ? null : ("LIMIT", statement.LimitPart.Span), errors);

        if (statement.Offset is not null)
        {
            AddError(("OFFSET", statement.OffsetSpan ?? statement.LimitPart?.Span ?? statement.FromSpan), errors);
        }
    }

    private static void AddError((string Clause, LangSpan Span)? clause, List<LangError> errors)
    {
        if (clause is null)
        {
            return;
        }

        errors.Add(new LangError
        {
            Message = $"Provider 'Inline' не поддерживает {clause.Value.Clause} после FROM. Если нужны преобразования, загрузите Inline в таблицу и сделайте отдельный LOAD FROM этой таблицы.",
            Span = clause.Value.Span
        });
    }

    private static void ValidateColumns(InlineData inline, List<LangError> errors)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in inline.Columns)
        {
            if (names.Add(column.Name))
            {
                continue;
            }

            errors.Add(new LangError
            {
                Message = $"Inline column '{column.Name}' указана несколько раз.",
                Span = column.Span
            });
        }
    }

    private static void ValidateRows(InlineData inline, List<LangError> errors)
    {
        foreach (var row in inline.Rows)
        {
            if (row.Values.Count == inline.Columns.Count)
            {
                continue;
            }

            errors.Add(new LangError
            {
                Message = $"Inline row содержит {row.Values.Count} values, ожидалось {inline.Columns.Count}.",
                Span = row.Span
            });
        }
    }
}
