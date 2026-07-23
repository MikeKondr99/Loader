using System.Globalization;

namespace Loader.Demo;

internal readonly record struct DemoMemorySnapshot(long ManagedHeapBytes)
{
    public static DemoMemorySnapshot Capture()
    {
        return new DemoMemorySnapshot(GC.GetTotalMemory(forceFullCollection: false));
    }

    public string ToLogString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"managed={ToMb(ManagedHeapBytes):0.#} MB");
    }

    private static double ToMb(long bytes)
    {
        return bytes / 1024d / 1024d;
    }
}
