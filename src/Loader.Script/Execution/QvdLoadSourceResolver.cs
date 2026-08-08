using Loader.Core.Providers.Qvd;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal sealed class QvdLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Qvd";

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        RejectUnknownOptions(Name, options, errors, ["path"]);
        RejectSqlForFileProvider("qvd", statement, errors);
        var path = RequiredPath("qvd", statement, options, errors);
        if (path is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        return ValueTask.FromResult(File(
            "qvd",
            context.FileStorage,
            path,
            static (source, fileName, token) => new QvdProvider().OpenReaderAsync(
                source,
                new QvdTableConfig { FileName = fileName },
                token)));
    }
}
