using TUnit.Core;
using TUnit.Core.Interfaces;

namespace Loader.Tests.Common;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public abstract class DatabaseTestResourceAttribute<TDatabase, TParallelLimit> :
    AsyncUntypedDataSourceGeneratorAttribute,
    ITestDiscoveryEventReceiver,
    ITestRegisteredEventReceiver
    where TParallelLimit : IParallelLimit, new()
{
    private readonly ClassDataSourceAttribute<TDatabase> dataSource = new()
    {
        Shared = SharedType.PerTestSession
    };

    protected DatabaseTestResourceAttribute(string category)
    {
        Category = category;
    }

    public int Order => 0;

    public string Category { get; }

    protected override async IAsyncEnumerable<Func<Task<object?[]?>>> GenerateDataSourcesAsync(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        await foreach (var row in dataSource.GetDataRowsAsync(dataGeneratorMetadata).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    public ValueTask OnTestDiscovered(DiscoveredTestContext context)
    {
        context.AddCategory(Category);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnTestRegistered(TestRegisteredContext context)
    {
        context.SetParallelLimiter(new TParallelLimit());
        return ValueTask.CompletedTask;
    }
}
