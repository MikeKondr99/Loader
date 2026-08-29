using Loader.Core.Providers.Qvd;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Qvd</c>. Создает reader-source для чтения QVD-файла из <see cref="ScriptContext.FileStorage"/>.
/// Параметры:
/// path: Text - путь к файлу внутри file storage.
/// </summary>
internal sealed class QvdLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Qvd";

    public override ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = options.MapPositionals(Name, ["path"]);
        RejectUnknownOptions(Name, options, errors, ["path"]);
        RejectSqlForFileProvider("qvd", statement, errors);
        var path = RequiredPath("qvd", statement, options, errors);
        if (path is null || errors.Count > 0)
        {
            return Error();
        }

        return ValueTask.FromResult<LoadFromSource>(File(
            context.FileStorage,
            path,
            static (source, fileName, token) => new QvdProvider().OpenReaderAsync(
                source,
                new QvdTableConfig { FileName = fileName },
                token)));
    }
}
