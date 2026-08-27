using Loader.Core.Providers.Xml;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Xml</c>. Создает источник чтения XML-файла из <see cref="ScriptContext.FileStorage"/>.
/// Параметры:
/// path: Text - путь к файлу внутри file storage.
/// table: Text - имя XML-node, которая считается строкой таблицы.
/// </summary>
internal sealed class XmlLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Xml";

    public override async ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = options.MapPositionals(Name, ["path"]);
        RejectUnknownOptions(Name, options, errors, ["path", "table"]);
        RejectSqlForFileProvider("xml", statement, errors);
        var path = RequiredPath("xml", statement, options, errors);
        var tableName = options.RequiredString(
            "table",
            statement.SourceCall.Span,
            "Для XML-источника требуется опция table='имя-строки'.");
        if (path is null || tableName is null || errors.Count > 0)
        {
            return null!;
        }

        var provider = new XmlProvider();
        var schema = await provider
            .AnalyzeSchemaAsync(context.FileStorage, path, tableName, cancellationToken)
            .ConfigureAwait(false);

        return new ReaderLoadFromSource
        {
            RequiresBuffer = false,
            OpenReaderAsync = token => provider.OpenReaderAsync(
                context.FileStorage,
                new XmlTableConfig
                {
                    FileName = path,
                    TableName = tableName,
                    Schema = schema
                },
                token)
        };
    }
}
