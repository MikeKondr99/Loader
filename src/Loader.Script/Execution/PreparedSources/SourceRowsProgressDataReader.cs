using Loader.Core.Decorators;
using Loader.Core.Models;

namespace Loader.Script.Execution;

/// <summary>
/// Считает строки reader-source во время записи во временную таблицу и,
/// если задан интервал, периодически отправляет обновляемое progress-сообщение.
/// </summary>
public sealed class SourceRowsProgressDataReader : DomainDataReader
{
    private readonly DomainDataReader inner;
    private readonly IProgressLogger logger;
    private readonly TimeSpan? interval;
    private readonly DateTimeOffset startedAt;
    private DateTimeOffset lastReportAt;

    public SourceRowsProgressDataReader(
        DomainDataReader inner,
        IProgressLogger logger,
        TimeSpan? interval)
        : base(inner)
    {
        this.inner = inner;
        this.logger = logger;
        this.interval = interval;
        MessageId = interval is null ? null : Guid.NewGuid().ToString("N");
        startedAt = DateTimeOffset.UtcNow;
        lastReportAt = startedAt;
    }

    public override DataSchema DataSchema => inner.DataSchema;

    public string? MessageId { get; }

    public long RowCount { get; private set; }

    public TimeSpan Elapsed => DateTimeOffset.UtcNow - startedAt;

    public override bool Read()
    {
        if (!inner.Read())
        {
            HasReadableRow = false;
            return false;
        }

        HasReadableRow = true;
        RowCount++;
        ReportIfDue(CancellationToken.None).GetAwaiter().GetResult();
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            HasReadableRow = false;
            return false;
        }

        HasReadableRow = true;
        RowCount++;
        await ReportIfDue(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        return inner.GetValue(ordinal);
    }

    public override int GetValues(object[] values)
    {
        EnsureReadableRow();
        return inner.GetValues(values);
    }

    private async ValueTask ReportIfDue(CancellationToken cancellationToken)
    {
        if (MessageId is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (interval > TimeSpan.Zero && now - lastReportAt < interval)
        {
            return;
        }

        lastReportAt = now;
        await logger.SourceRowsLoadedAsync(
            RowCount,
            MessageId,
            now - startedAt,
            completed: false,
            cancellationToken).ConfigureAwait(false);
    }
}
