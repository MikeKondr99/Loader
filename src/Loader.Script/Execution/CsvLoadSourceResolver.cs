using Loader.Core.Providers.Csv;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal sealed class CsvLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Csv";

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = options.MapPositionals(Name, ["path"]);
        RejectUnknownOptions(Name, options, errors, ["path", "delimiter", "header"]);
        RejectSqlForFileProvider("csv", statement, errors);
        var path = RequiredPath("csv", statement, options, errors);
        var delimiter = options.Character("delimiter", ',');
        var hasHeader = options.Boolean("header", true);
        if (path is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        return ValueTask.FromResult(File(
            "csv",
            context.FileStorage,
            path,
            (source, fileName, token) => new CsvProvider().OpenReaderAsync(
                source,
                new CsvTableConfig
                {
                    FileName = fileName,
                    Delimiter = delimiter,
                    HasHeader = hasHeader
                },
                token)));
    }
}
