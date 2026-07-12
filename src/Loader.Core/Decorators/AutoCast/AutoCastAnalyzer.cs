namespace Loader.Core.Decorators.AutoCast;

/// <summary>
/// Контейнер результата анализа автокаста. Форматы не хранят состояние, меняется только набор живых кандидатов по текстовым полям.
/// </summary>
public sealed class AutoCastAnalyzer
{
    private readonly IReadOnlyList<IAutoCastFormat> _formats;
    private FieldState[] _fields = [];

    public AutoCastAnalyzer(IReadOnlyList<IAutoCastFormat>? formats = null)
    {
        _formats = formats ?? AutoCastDefaultFormats.Candidates;
    }

    public bool Success { get; private set; }

    public AutoCastSchema? Schema { get; private set; }

    internal void Begin(DataSchema schema)
    {
        // 1. Активные кандидаты заводим только для текстовых полей. Уже типизированным данным верим.
        _fields = schema.Fields
            .Select(field => new FieldState(field.Name, ShouldAnalyze(field), CreateActiveMask()))
            .ToArray();

        Success = false;
        Schema = null;
    }

    internal void Observe(int ordinal, object value)
    {
        var field = _fields[ordinal];
        if (!field.ShouldAnalyze || value == DBNull.Value)
        {
            return;
        }

        if (value is not string text)
        {
            return;
        }

        field.HasObservedValue = true;
        for (var i = 0; i < _formats.Count; i++)
        {
            if (!field.ActiveFormats[i])
            {
                continue;
            }

            if (!_formats[i].TryConvert(text, out _))
            {
                field.ActiveFormats[i] = false;
            }
        }
    }

    internal void Complete()
    {
        // 2. Побеждает первый выживший формат. Нетекстовые поля в AutoCastSchema не попадают.
        Schema = new AutoCastSchema
        {
            Fields = _fields
                .Where(static field => field.ShouldAnalyze)
                .Select(field => new AutoCastField
                {
                    Name = field.Name,
                    Format = ChooseFormat(field)
                })
                .ToArray()
        };

        Success = true;
    }

    private static bool ShouldAnalyze(DataField field)
    {
        return field.DataType == DataType.Text && field.ClrType == typeof(string);
    }

    private bool[] CreateActiveMask()
    {
        return Enumerable.Repeat(true, _formats.Count).ToArray();
    }

    private IAutoCastFormat ChooseFormat(FieldState field)
    {
        if (!field.HasObservedValue)
        {
            return AutoCastFormats.Text;
        }

        for (var i = 0; i < _formats.Count; i++)
        {
            if (field.ActiveFormats[i])
            {
                return _formats[i];
            }
        }

        return AutoCastFormats.Text;
    }

    private sealed record FieldState(string Name, bool ShouldAnalyze, bool[] ActiveFormats)
    {
        public bool HasObservedValue { get; set; }
    }
}
