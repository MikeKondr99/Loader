using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Resolver provider-а <c>Connect</c>. Создает источник чтения БД через подключение из <see cref="ScriptContext.ConnectionRegistry"/>.
/// Параметры:
/// name: Text - имя подключения в registry.
/// SQL после FROM: Text - запрос, который будет выполнен на стороне подключенной БД.
/// </summary>
internal sealed class ConnectLoadSourceResolver : LoadSourceResolverBase
{
    public override string Name => "Connect";

    public override async ValueTask<LoadFromSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        options = options.MapPositionals(Name, ["name"]);
        var nameOption = options.GetOption("name");
        var name = options.RequiredString(
            "name",
            statement.SourceCall.Span,
            "Для Connect требуется опция name='connection_name'.");
        var sql = SourceSql("connect", statement, errors);
        if (name is null || sql is null || errors.Count > 0)
        {
            return null!;
        }

        var connection = await context.ConnectionRegistry.GetAsync(name, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            var names = await context.ConnectionRegistry.FindNamesAsync(cancellationToken).ConfigureAwait(false);
            var message = NameSuggestion.AppendSuggestion(
                $"Connection '{name}' не найден.",
                name,
                names);
            errors.Add(new LangError
            {
                Message = message,
                Span = nameOption?.Span ?? statement.SourceCall.Span
            });
            return null!;
        }

        if (!DatabaseLoadProviderFactory.TryGet(connection.Provider, out var factory))
        {
            errors.Add(new LangError
            {
                Message = $"Connection '{connection.Name}' использует неподдерживаемый provider '{connection.Provider}'.",
                Span = nameOption?.Span ?? statement.SourceCall.Span
            });
            return null!;
        }

        return factory.CreateSource(connection.ConnectionString, sql);
    }
}
