using Loader.Core.Providers.Csv;
using Loader.Lang;
using Loader.Lang.Statements;
using Sylvan.Data.Csv;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Csv</c>. Создает источник чтения CSV-файла из <see cref="ScriptContext.FileStorage"/>.
/// Параметры:
/// path: Text - путь к файлу внутри file storage.
/// delimiter: Text - один символ разделителя, по умолчанию <c>,</c>.
/// header: Boolean - указывает, содержит ли первая строка имена колонок, по умолчанию <c>true</c>.
/// </summary>
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
        RejectUnknownOptions(Name, options, errors, ["path", "delimiter", "header", "skipRows", "style", "encoding", "comment", "trimHeaders", "trimValues", "emptyAsNull"]);
        RejectSqlForFileProvider("csv", statement, errors);
        var path = RequiredPath("csv", statement, options, errors);
        var delimiter = options.Character("delimiter", ',');
        var hasHeader = options.Boolean("header", true);
        var skipRows = options.Integer("skipRows", 0);
        if (skipRows < 0)
        {
            errors.Add(new LangError
            {
                Message = "Опция 'skipRows' должна быть больше или равна 0.",
                Span = options.GetOption("skipRows")?.Span ?? statement.SourceCall.Span
            });
        }

        var style = CsvStyleResolver.Optional(Name, "style", options, errors, CsvStyle.Lax);
        var encoding = FileEncodingResolver.Optional(Name, "encoding", options, errors);
        var comment = options.GetOption("comment") is null
            ? (char?)null
            : options.Character("comment", '\0');
        var trimHeaders = options.Boolean("trimHeaders", false);
        var trimValues = options.Boolean("trimValues", false);
        var emptyAsNull = options.Boolean("emptyAsNull", false);
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
                    HasHeader = hasHeader,
                    SkipRows = skipRows,
                    Style = style,
                    Encoding = encoding,
                    Comment = comment,
                    TrimHeaders = trimHeaders,
                    TrimValues = trimValues,
                    EmptyAsNull = emptyAsNull
                },
                token)));
    }
}
