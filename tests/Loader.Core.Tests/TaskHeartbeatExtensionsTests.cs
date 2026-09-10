using Loader.Core.Tasks;

namespace Loader.Core.Tests;

public sealed class TaskHeartbeatExtensionsTests
{
    [Test]
    [DisplayName("Task heartbeat вызывает callback пока ValueTask выполняется")]
    public async Task ValueTask_heartbeat_reports_while_task_is_running()
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ticked = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = new ValueTask<int>(source.Task).WithHeartbeatAsync(
            TimeSpan.FromMilliseconds(10),
            (elapsed, _) =>
            {
                ticked.TrySetResult(elapsed);
                return ValueTask.CompletedTask;
            });

        var completed = await Task.WhenAny(ticked.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await Assert.That(ReferenceEquals(completed, ticked.Task)).IsTrue();

        source.SetResult(42);

        var result = await task;
        await Assert.That(result.Value).IsEqualTo(42);
        await Assert.That(result.Elapsed).IsGreaterThan(TimeSpan.Zero);
        await Assert.That(await ticked.Task).IsGreaterThan(TimeSpan.Zero);
    }

    [Test]
    [DisplayName("Task heartbeat не вызывает callback если interval не задан")]
    public async Task ValueTask_heartbeat_does_not_report_without_interval()
    {
        var ticks = 0;

        var result = await new ValueTask<int>(Task.FromResult(42)).WithHeartbeatAsync(
            null,
            (_, _) =>
            {
                ticks++;
                return ValueTask.CompletedTask;
            });

        await Assert.That(result.Value).IsEqualTo(42);
        await Assert.That(result.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.Zero);
        await Assert.That(ticks).IsEqualTo(0);
    }
}
