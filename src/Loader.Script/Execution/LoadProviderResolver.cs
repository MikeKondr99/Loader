using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

public sealed class LoadProviderResolver : ILoadProviderResolver
{
    private static readonly IReadOnlyDictionary<string, ILoadSourceResolver> Resolvers = CreateResolvers();

    public async ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<LangError>();
        var options = new LoadOptionReader(statement.SourceCall.Options, errors);

        if (!Resolvers.TryGetValue(statement.SourceCall.Name, out var resolver))
        {
            var message = NameSuggestion.AppendSuggestion(
                $"Provider '{statement.SourceCall.Name.ToLowerInvariant()}' не поддерживается.",
                statement.SourceCall.Name,
                Resolvers.Keys);
            errors.Add(new LangError
            {
                Message = message,
                Span = statement.SourceCall.NameSpan
            });
        }

        var hasInlineDataForNonInlineProvider = statement.SourceCall.InlineData is not null &&
                                               !string.Equals(
                                                   statement.SourceCall.Name,
                                                   "Inline",
                                                   StringComparison.OrdinalIgnoreCase);
        if (hasInlineDataForNonInlineProvider)
        {
            errors.Add(new LangError
            {
                Message = $"Provider '{statement.SourceCall.Name}' не поддерживает inline-данные. Используйте Inline(...).",
                Span = statement.SourceCall.Span
            });
        }

        LoadProviderSource? source = null;
        if (resolver is not null && !hasInlineDataForNonInlineProvider)
        {
            try
            {
                source = await resolver.ResolveAsync(statement, context, options, errors, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ProviderResolutionException)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                errors.Add(new LangError
                {
                    Message = $"Не удалось подготовить provider '{statement.SourceCall.Name}': {exception.Message}",
                    Span = statement.SourceCall.Span
                });
                throw new ProviderResolutionException(errors, exception);
            }
        }

        if (errors.Count > 0)
        {
            throw new ProviderResolutionException(errors);
        }

        return source!;
    }

    private static IReadOnlyDictionary<string, ILoadSourceResolver> CreateResolvers()
    {
        ILoadSourceResolver[] resolvers =
        [
            new CsvLoadSourceResolver(),
            new ExcelLoadSourceResolver(),
            new JsonLoadSourceResolver(),
            new XmlLoadSourceResolver(),
            new QvdLoadSourceResolver(),
            new InlineLoadSourceResolver(),
            new NumbersLoadSourceResolver(),
            new CalendarLoadSourceResolver(),
            new TableLoadSourceResolver(),
            new ConnectLoadSourceResolver()
        ];

        return resolvers.ToDictionary(
            static resolver => resolver.Name,
            static resolver => resolver,
            StringComparer.OrdinalIgnoreCase);
    }
}
