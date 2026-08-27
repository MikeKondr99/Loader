using Loader.Core.Providers.Excel;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Excel</c>. Создает источник чтения Excel workbook-а из <see cref="ScriptContext.FileStorage"/>.
/// Параметры:
/// path: Text - путь к файлу внутри file storage.
/// sheet: Text - имя листа; если не задано, provider выбирает лист по своей логике.
/// header: Boolean - указывает, содержит ли первая строка имена колонок, по умолчанию <c>true</c>.
/// </summary>
internal sealed class ExcelLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Excel";

    public override ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = options.MapPositionals(Name, ["path"]);
        RejectUnknownOptions(Name, options, errors, ["path", "sheet", "header"]);
        RejectSqlForFileProvider("excel", statement, errors);
        var path = RequiredPath("excel", statement, options, errors);
        var sheet = options.String("sheet");
        var hasHeader = options.Boolean("header", true);
        if (path is null || errors.Count > 0)
        {
            return Error();
        }

        return ValueTask.FromResult<LoadFromSource>(File(
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
