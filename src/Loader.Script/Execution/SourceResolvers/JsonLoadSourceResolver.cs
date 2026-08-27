using Loader.Core.Providers.Json;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Json</c>. Создает источник чтения JSON-массива из <see cref="ScriptContext.FileStorage"/>.
/// Параметры:
/// path: Text - путь к файлу внутри file storage.
/// root: Text - dot-path до массива записей внутри JSON; если не задан, корнем считается весь документ.
/// </summary>
internal sealed class JsonLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Json";

    public override async ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = options.MapPositionals(Name, ["path"]);
        RejectUnknownOptions(Name, options, errors, ["path", "root"]);
        RejectSqlForFileProvider("json", statement, errors);
        var path = RequiredPath("json", statement, options, errors);
        var arrayPath = JsonRootPath(options, errors);
        if (path is null || errors.Count > 0)
        {
            return null!;
        }

        var provider = new JsonProvider();
        var schema = await provider
            .AnalyzeSchemaAsync(context.FileStorage, path, arrayPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new LoadProviderSource
        {
            Kind = "json",
            RequiresBuffer = false,
            OpenReaderAsync = token => provider.OpenReaderAsync(
                context.FileStorage,
                new JsonTableConfig
                {
                    FileName = path,
                    ArrayPath = arrayPath,
                    Schema = schema
                },
                token)
        };
    }

    private static IReadOnlyList<string> JsonRootPath(
        LoadOptionReader options,
        List<LangError> errors)
    {
        var root = options.String("root");
        if (root is null)
        {
            return [];
        }

        var path = root
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (path.Length > 0)
        {
            return path;
        }

        errors.Add(new LangError
        {
            Message = "Опция 'root' должна указывать путь к JSON-массиву.",
            Span = options.GetOption("root")?.Span ?? new LangSpan(1, 1, 1, 1)
        });
        return [];
    }
}
