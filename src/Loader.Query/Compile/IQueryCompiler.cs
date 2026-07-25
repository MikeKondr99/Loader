using Loader.Query.Models;

namespace Loader.Query.Compile;

public interface IQueryCompiler
{
    string Compile(ResolvedQuery query);
}
