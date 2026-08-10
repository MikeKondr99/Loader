using TUnit.Core;
using TUnit.Core.Interfaces;

namespace Loader.Tests.Common;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public abstract class DriverTestAttribute<TParallelLimit> :
    Attribute,
    ITestDiscoveryEventReceiver,
    ITestRegisteredEventReceiver
    where TParallelLimit : IParallelLimit, new()
{
    protected DriverTestAttribute(string category)
    {
        Category = category;
    }

    public int Order => 0;

    public string Category { get; }

    public ValueTask OnTestDiscovered(DiscoveredTestContext context)
    {
        context.AddCategory(Category);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnTestRegistered(TestRegisteredContext context)
    {
        context.SetParallelLimiter(new TParallelLimit());
        var skipReason = GetSkipReason();
        if (skipReason is not null)
        {
            context.SetSkipped(skipReason);
        }

        return ValueTask.CompletedTask;
    }

    protected virtual string? GetSkipReason()
    {
        return null;
    }
}
