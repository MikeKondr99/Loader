using System.Diagnostics;

namespace Loader.Core.Tasks;

public readonly record struct TaskHeartbeatResult(TimeSpan Elapsed);

public readonly record struct TaskHeartbeatResult<T>(T Value, TimeSpan Elapsed);

/// <summary>
/// Общий heartbeat для долгих async-операций: пока task выполняется, периодически вызывает callback с прошедшим временем.
/// </summary>
public static class TaskHeartbeatExtensions
{
    public static async Task<TaskHeartbeatResult> WithHeartbeatAsync(
        this Task task,
        TimeSpan? interval,
        Func<TimeSpan, CancellationToken, ValueTask> onTick,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var heartbeatTask = interval is null || interval <= TimeSpan.Zero
            ? null
            : RunHeartbeatAsync(task, interval.Value, stopwatch, onTick, cancellationToken);

        try
        {
            await task.ConfigureAwait(false);
            return new TaskHeartbeatResult(stopwatch.Elapsed);
        }
        finally
        {
            if (heartbeatTask is not null)
            {
                await heartbeatTask.ConfigureAwait(false);
            }
        }
    }

    public static async Task<TaskHeartbeatResult<T>> WithHeartbeatAsync<T>(
        this Task<T> task,
        TimeSpan? interval,
        Func<TimeSpan, CancellationToken, ValueTask> onTick,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var heartbeatTask = interval is null || interval <= TimeSpan.Zero
            ? null
            : RunHeartbeatAsync(task, interval.Value, stopwatch, onTick, cancellationToken);

        try
        {
            var value = await task.ConfigureAwait(false);
            return new TaskHeartbeatResult<T>(value, stopwatch.Elapsed);
        }
        finally
        {
            if (heartbeatTask is not null)
            {
                await heartbeatTask.ConfigureAwait(false);
            }
        }
    }

    public static async ValueTask<TaskHeartbeatResult> WithHeartbeatAsync(
        this ValueTask task,
        TimeSpan? interval,
        Func<TimeSpan, CancellationToken, ValueTask> onTick,
        CancellationToken cancellationToken = default)
    {
        if (interval is null || interval <= TimeSpan.Zero)
        {
            var stopwatch = Stopwatch.StartNew();
            await task.ConfigureAwait(false);
            return new TaskHeartbeatResult(stopwatch.Elapsed);
        }

        return await task.AsTask().WithHeartbeatAsync(interval, onTick, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<TaskHeartbeatResult<T>> WithHeartbeatAsync<T>(
        this ValueTask<T> task,
        TimeSpan? interval,
        Func<TimeSpan, CancellationToken, ValueTask> onTick,
        CancellationToken cancellationToken = default)
    {
        if (interval is null || interval <= TimeSpan.Zero)
        {
            var stopwatch = Stopwatch.StartNew();
            var value = await task.ConfigureAwait(false);
            return new TaskHeartbeatResult<T>(value, stopwatch.Elapsed);
        }

        return await task.AsTask().WithHeartbeatAsync(interval, onTick, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunHeartbeatAsync(
        Task task,
        TimeSpan interval,
        Stopwatch stopwatch,
        Func<TimeSpan, CancellationToken, ValueTask> onTick,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!task.IsCompleted)
            {
                var delayTask = Task.Delay(interval, cancellationToken);
                var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

                if (completed != task)
                {
                    await onTick(stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
