using System.Diagnostics;
using System.Globalization;

namespace Loader.Demo;

/// <summary>
/// Пишет все сообщения с абсолютным временем от запуска процесса и длительностью завершенных этапов.
/// </summary>
internal sealed class DemoLog
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public DemoLogOperation Begin(string message)
    {
        Write(Console.Out, message);
        return new DemoLogOperation(this, _stopwatch.Elapsed);
    }

    public void Info(string message)
    {
        Write(Console.Out, message);
    }

    public void Error(string message)
    {
        Write(Console.Error, message);
    }

    private void Complete(string message, TimeSpan startedAt)
    {
        var duration = _stopwatch.Elapsed - startedAt;
        Write(Console.Out, $"{message} заняло [{Format(duration)}]");
    }

    private void Write(TextWriter writer, string message)
    {
        writer.WriteLine($"[{Format(_stopwatch.Elapsed)}] {message}");
    }

    private static string Format(TimeSpan value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value.TotalSeconds:0.###} sec");
    }

    internal readonly struct DemoLogOperation
    {
        private readonly DemoLog _log;
        private readonly TimeSpan _startedAt;

        public DemoLogOperation(DemoLog log, TimeSpan startedAt)
        {
            _log = log;
            _startedAt = startedAt;
        }

        public void Complete(string message)
        {
            _log.Complete(message, _startedAt);
        }
    }
}
