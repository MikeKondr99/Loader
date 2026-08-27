using System.Data.Common;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Numbers</c>. Создает источник с последовательностью целых чисел.
/// Параметры:
/// max: Integer - последнее допустимое значение последовательности.
/// min: Integer - первое значение последовательности, по умолчанию <c>0</c>.
/// step: Integer - шаг последовательности, должен быть больше <c>0</c>, по умолчанию <c>1</c>.
/// </summary>
internal sealed class NumbersLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Numbers";

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        var positionalNames = options.PositionalCount <= 1
            ? new[] { "max" }
            : ["min", "max"];
        options = options.MapPositionals(Name, positionalNames);
        RejectUnknownOptions(Name, options, errors, ["max", "min", "step"]);
        RejectSql(statement, errors);

        var max = options.RequiredInteger(
            "max",
            statement.SourceCall.Span,
            "Для provider-а Numbers требуется опция max=1000.");
        var min = options.Integer("min", 0);
        var step = options.Integer("step", 1);

        if (step <= 0)
        {
            errors.Add(new LangError
            {
                Message = "Опция 'step' должна быть больше 0.",
                Span = options.GetOption("step")?.Span ?? statement.SourceCall.Span
            });
        }

        if (max is not null && max.Value < min)
        {
            errors.Add(new LangError
            {
                Message = "Опция 'max' должна быть больше или равна 'min'.",
                Span = options.GetOption("max")?.Span ?? statement.SourceCall.Span
            });
        }

        if (max is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        return ValueTask.FromResult(new LoadProviderSource
        {
            Kind = "numbers",
            RequiresBuffer = false,
            OpenReaderAsync = _ => ValueTask.FromResult<DbDataReader>(new NumbersDataReader(min, max.Value, step))
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
            Message = "Provider 'Numbers' не поддерживает SQL после FROM.",
            Span = statement.SqlPart.Span
        });
    }
}
