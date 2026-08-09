using System.Data.Common;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal abstract class LoadSourceResolverBase : ILoadSourceResolver
{
    public abstract string Name { get; }

    public abstract ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken);

    protected static LoadProviderSource File(
        string kind,
        IFileSource source,
        string fileName,
        Func<IFileSource, string, CancellationToken, ValueTask<DbDataReader>> open)
    {
        return new LoadProviderSource
        {
            Kind = kind,
            RequiresBuffer = false,
            OpenReaderAsync = token => open(source, fileName, token)
        };
    }

    protected static string? RequiredPath(
        string kind,
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        return options.RequiredString(
            "path",
            statement.SourceCall.Span,
            $"Для file provider-а '{kind}' требуется опция path='relative/path'.");
    }

    protected static string? RequiredConnection(
        string kind,
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        return options.RequiredString(
            "connection",
            statement.SourceCall.Span,
            $"Для DB provider-а '{kind}' требуется опция connection='connection string'.");
    }

    protected static void RejectUnknownOptions(
        string providerName,
        LoadOptionReader options,
        List<LangError> errors,
        ReadOnlySpan<string> allowedNames)
    {
        options.RejectUnknownOptions(providerName, allowedNames);
    }

    protected static string? SourceSql(
        string kind,
        LoadStatement statement,
        List<LangError> errors)
    {
        var sql = statement.Sql;
        if (sql is not null && sql.Trim().Length == 0)
        {
            errors.Add(new LangError
            {
                Message = $"Для provider-а БД '{kind}' SQL не должен быть пустым.",
                Span = statement.SqlPart?.Span ?? statement.FromSpan
            });
        }

        if (sql is null)
        {
            errors.Add(new LangError
            {
                Message = $"Для provider-а БД '{kind}' требуется SQL после FROM.",
                Span = statement.FromSpan
            });
        }

        return sql;
    }

    protected static void RejectSqlForFileProvider(
        string kind,
        LoadStatement statement,
        List<LangError> errors)
    {
        if (statement.SqlPart is not null)
        {
            errors.Add(new LangError
            {
                Message = $"Файловый provider '{kind}' не поддерживает SQL после FROM.",
                Span = statement.SqlPart.Span
            });
        }
    }
}
