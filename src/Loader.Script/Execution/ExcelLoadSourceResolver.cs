using Loader.Core.Providers.Excel;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal sealed class ExcelLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Excel";

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        RejectUnknownOptions(Name, options, errors, ["path", "sheet", "header"]);
        RejectSqlForFileProvider("excel", statement, errors);
        var path = RequiredPath("excel", statement, options, errors);
        var sheet = options.String("sheet");
        var hasHeader = options.Boolean("header", true);
        if (path is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        return ValueTask.FromResult(File(
            "excel",
            context.FileStorage,
            path,
            (source, fileName, token) => new ExcelProvider().OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = sheet,
                    HasHeader = hasHeader
                },
                token)));
    }
}
