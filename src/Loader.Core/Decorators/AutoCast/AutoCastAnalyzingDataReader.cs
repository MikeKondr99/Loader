namespace Loader.Core.Decorators.AutoCast;

internal sealed class AutoCastAnalyzingDataReader : DomainDataReaderDecorator
{
    private readonly AutoCastAnalyzer _analyzer;

    public AutoCastAnalyzingDataReader(DomainDataReader inner, AutoCastAnalyzer analyzer)
        : base(inner)
    {
        _analyzer = analyzer;
        _analyzer.Begin(inner.DataSchema);
    }

    public override bool Read()
    {
        if (!InnerDomain.Read())
        {
            HasReadableRow = false;
            _analyzer.Complete();
            return false;
        }

        ObserveCurrentRow();
        HasReadableRow = true;
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await InnerDomain.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            HasReadableRow = false;
            _analyzer.Complete();
            return false;
        }

        ObserveCurrentRow();
        HasReadableRow = true;
        return true;
    }

    private void ObserveCurrentRow()
    {
        // 1. Читаем значения уже нормализованного reader и только сужаем набор форматов.
        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            _analyzer.Observe(ordinal, InnerDomain.GetValue(ordinal));
        }
    }
}
