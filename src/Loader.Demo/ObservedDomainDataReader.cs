using Loader.Core.Decorators;
using Loader.Core.Models;

namespace Loader.Demo;

/// <summary>
/// Наблюдающий reader для demo pipeline: считает реально прочитанные строки,
/// периодически пишет прогресс и снимает память процесса.
/// Значения, схема и accessor-ы полностью остаются у внутреннего DomainDataReader.
/// </summary>
internal sealed class ObservedDomainDataReader : DomainDataReader
{
    private readonly DomainDataReader _inner;
    private readonly DemoLog _log;
    private readonly string _operationName;
    private readonly long _progressInterval;
    private long _nextProgressAt;

    public ObservedDomainDataReader(
        DomainDataReader inner,
        DemoLog log,
        string operationName,
        long progressInterval)
        : base(inner)
    {
        _inner = inner;
        _log = log;
        _operationName = operationName;
        _progressInterval = progressInterval;
        _nextProgressAt = progressInterval;
    }

    public long RowsRead { get; private set; }

    public DemoMemorySnapshot PeakMemory { get; private set; } = DemoMemorySnapshot.Capture();

    public DemoMemorySnapshot CaptureMemory()
    {
        PeakMemory = Max(PeakMemory, DemoMemorySnapshot.Capture());
        return PeakMemory;
    }

    public override DataSchema DataSchema => _inner.DataSchema;

    public override bool Read()
    {
        var hasRow = _inner.Read();
        TrackRead(hasRow);
        return hasRow;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        var hasRow = await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);
        TrackRead(hasRow);
        return hasRow;
    }

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        return _inner.GetValue(ordinal);
    }

    public override int GetValues(object[] values)
    {
        EnsureReadableRow();
        return _inner.GetValues(values);
    }

    public override bool IsDBNull(int ordinal)
    {
        EnsureReadableRow();
        return _inner.IsDBNull(ordinal);
    }

    private void TrackRead(bool hasRow)
    {
        HasReadableRow = hasRow;
        if (!hasRow)
        {
            return;
        }

        RowsRead++;
        if (_progressInterval <= 0 || RowsRead < _nextProgressAt)
        {
            return;
        }

        var memory = CaptureMemory();
        _log.Info($"{_operationName}: прочитано строк {RowsRead:N0}; память: {memory.ToLogString()}");
        _nextProgressAt += _progressInterval;
    }

    private static DemoMemorySnapshot Max(DemoMemorySnapshot left, DemoMemorySnapshot right)
    {
        return new DemoMemorySnapshot(Math.Max(left.ManagedHeapBytes, right.ManagedHeapBytes));
    }
}
