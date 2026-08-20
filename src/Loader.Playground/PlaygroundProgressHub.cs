using System.Collections.Concurrent;
using System.Threading.Channels;
using Loader.Script;

internal sealed class PlaygroundProgressHub
{
    private readonly ConcurrentDictionary<string, ProgressRun> runs = new(StringComparer.Ordinal);

    public ChannelReader<ScriptProgressEvent> Subscribe(string runId)
    {
        return runs.GetOrAdd(runId, static _ => new ProgressRun()).Subscribe();
    }

    public ValueTask PublishAsync(string runId, ScriptProgressEvent progressEvent)
    {
        runs.GetOrAdd(runId, static _ => new ProgressRun()).Publish(progressEvent);
        return ValueTask.CompletedTask;
    }

    public void Complete(string runId)
    {
        if (!runs.TryGetValue(runId, out var run))
        {
            return;
        }

        run.Complete();
        _ = RemoveCompletedLaterAsync(runId);
    }

    private async Task RemoveCompletedLaterAsync(string runId)
    {
        await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
        runs.TryRemove(runId, out _);
    }

    private sealed class ProgressRun
    {
        private readonly object sync = new();
        private readonly List<ScriptProgressEvent> history = [];
        private readonly List<Channel<ScriptProgressEvent>> subscribers = [];
        private bool completed;

        public ChannelReader<ScriptProgressEvent> Subscribe()
        {
            var channel = Channel.CreateUnbounded<ScriptProgressEvent>();
            lock (sync)
            {
                foreach (var progressEvent in history)
                {
                    channel.Writer.TryWrite(progressEvent);
                }

                if (completed)
                {
                    channel.Writer.TryComplete();
                }
                else
                {
                    subscribers.Add(channel);
                }
            }

            return channel.Reader;
        }

        public void Publish(ScriptProgressEvent progressEvent)
        {
            lock (sync)
            {
                if (completed)
                {
                    return;
                }

                history.Add(progressEvent);
                foreach (var subscriber in subscribers)
                {
                    subscriber.Writer.TryWrite(progressEvent);
                }
            }
        }

        public void Complete()
        {
            lock (sync)
            {
                completed = true;
                foreach (var subscriber in subscribers)
                {
                    subscriber.Writer.TryComplete();
                }

                subscribers.Clear();
            }
        }
    }
}

internal sealed class PlaygroundProgressLogger : IProgressLogger
{
    private readonly PlaygroundProgressHub hub;
    private readonly string runId;

    public PlaygroundProgressLogger(PlaygroundProgressHub hub, string runId)
    {
        this.hub = hub;
        this.runId = runId;
    }

    public ValueTask ReportAsync(ScriptProgressEvent progressEvent, CancellationToken cancellationToken = default)
    {
        return hub.PublishAsync(runId, progressEvent);
    }
}
