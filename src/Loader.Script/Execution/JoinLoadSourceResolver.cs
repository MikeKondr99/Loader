using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Общий разрешатель источника для Join/LeftJoin/RightJoin/FullJoin.
/// Провайдер соединяет две уже загруженные script-таблицы по равенству ключей и возвращает читатель
/// с логической схемой результата, после чего обычный конвейер LOAD применяет WHERE/GROUP/ORDER/LIMIT.
/// </summary>
internal sealed class JoinLoadSourceResolver : LoadSourceResolverBase
{
    private readonly JoinKind kind;

    public JoinLoadSourceResolver(JoinKind kind)
    {
        this.kind = kind;
        Name = kind.ProviderName();
    }

    public override string Name { get; }

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        // Join работает только с четырьмя позиционными именами и не принимает source SQL.
        RejectNamedOptions(statement, errors);
        RejectSqlForFileProvider(Name.ToLowerInvariant(), statement, errors);

        // Разбираем аргументы как ссылки на две уже загруженные таблицы и два поля-ключа.
        var arguments = ResolveArguments(statement, options, errors);
        if (arguments is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        RejectSameTable(arguments, options, statement, errors);
        if (errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        // Находим таблицы в текущем ScriptContext: Join не открывает внешний provider.
        var left = ResolveTable(context, arguments.LeftTable, options, 0, statement, errors);
        var right = ResolveTable(context, arguments.RightTable, options, 2, statement, errors);
        if (left is null || right is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        // Проверяем, что ключевые поля существуют в соответствующих таблицах.
        var leftKeyIndex = ValidateKey(left, arguments.LeftField, options, 1, statement, errors);
        var rightKeyIndex = ValidateKey(right, arguments.RightField, options, 3, statement, errors);
        if (leftKeyIndex < 0 || rightKeyIndex < 0 || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        // Типы ключей должны совпадать до генерации SQL, иначе ClickHouse ошибка будет поздней и менее понятной.
        var leftKey = left.Fields[leftKeyIndex];
        var rightKey = right.Fields[rightKeyIndex];
        if (leftKey.DataType != rightKey.DataType)
        {
            errors.Add(new LangError
            {
                Message = $"Join keys должны иметь одинаковый тип. '{arguments.LeftField}' имеет тип {leftKey.DataType}, '{arguments.RightField}' имеет тип {rightKey.DataType}.",
                Span = statement.SourceCall.Span
            });
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        // Строим SQL чтения из DWH и логическую схему результата с разрешенными конфликтами имен.
        var joinSql = JoinSqlBuilder.Build(left, arguments.LeftField, right, arguments.RightField, kind, statement.SourceCall.Span, errors);
        if (joinSql is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        var source = new ConnectionStringSource { ConnectionString = context.TargetConnectionString };
        var config = new SqlTableConfig { Sql = joinSql.Sql };

        return ValueTask.FromResult(new LoadProviderSource
        {
            Kind = Name.ToLowerInvariant(),
            RequiresBuffer = false,
            OpenReaderAsync = async token =>
            {
                // ClickHouse возвращает служебные join_columnN, а наружу должен уйти доменный reader с пользовательскими именами.
                var reader = await new ClickHouseProvider()
                    .OpenReaderAsync(source, config, token)
                    .ConfigureAwait(false);

                var renamedReader = reader.RenameColumns(joinSql.Fields.Select(static field => field.Name).ToArray());
                return new LoadedTableDataReader(renamedReader, joinSql.Fields);
            }
        });
    }

    private static void RejectNamedOptions(LoadStatement statement, List<LangError> errors)
    {
        foreach (var option in statement.SourceCall.Options)
        {
            if (int.TryParse(option.Name, out _))
            {
                continue;
            }

            errors.Add(new LangError
            {
                Message = "Join provider принимает только позиционные аргументы: Join(table1, field1, table2, field2).",
                Span = option.Span
            });
        }
    }

    private JoinArguments? ResolveArguments(
        LoadStatement statement,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var positionals = options.PositionalOptions();
        if (positionals.Count != 4)
        {
            errors.Add(new LangError
            {
                Message = $"Provider '{Name}' требует ровно четыре аргумента: {Name}(table1, field1, table2, field2).",
                Span = statement.SourceCall.Span
            });
            return null;
        }

        var values = new string[4];
        for (var index = 0; index < positionals.Count; index++)
        {
            var option = positionals[index];
            if (option.Value is NameLiteral name)
            {
                values[index] = name.Value;
                continue;
            }

            errors.Add(new LangError
            {
                Message = $"Provider '{Name}' принимает только имена таблиц и полей без кавычек.",
                Span = option.Span
            });
        }

        return errors.Count > 0
            ? null
            : new JoinArguments(values[0], values[1], values[2], values[3]);
    }

    private static void RejectSameTable(
        JoinArguments arguments,
        LoadOptionReader options,
        LoadStatement statement,
        List<LangError> errors)
    {
        if (!string.Equals(arguments.LeftTable, arguments.RightTable, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(new LangError
        {
            Message = $"Join не поддерживает соединение таблицы '{arguments.LeftTable}' самой с собой. Загрузите её вторым LOAD под другим alias.",
            Span = options.GetOption("2")?.Span ?? statement.SourceCall.Span
        });
    }

    private static LoadedTable? ResolveTable(
        ScriptContext context,
        string name,
        LoadOptionReader options,
        int argumentIndex,
        LoadStatement statement,
        List<LangError> errors)
    {
        var table = context.FindLoadedTable(name);
        if (table is not null)
        {
            return table;
        }

        errors.Add(new LangError
        {
            Message = $"Таблица '{name}' не найдена среди уже загруженных LOAD таблиц.",
            Span = options.GetOption(argumentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))?.Span
                   ?? statement.SourceCall.Span
        });
        return null;
    }

    private static int ValidateKey(
        LoadedTable table,
        string fieldName,
        LoadOptionReader options,
        int argumentIndex,
        LoadStatement statement,
        List<LangError> errors)
    {
        var index = JoinSqlBuilder.FindFieldIndex(table, fieldName);
        if (index >= 0)
        {
            return index;
        }

        errors.Add(new LangError
        {
            Message = $"Поле '{fieldName}' не найдено в таблице '{table.Alias}'.",
            Span = options.GetOption(argumentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))?.Span
                   ?? statement.SourceCall.Span
        });
        return -1;
    }

    private sealed record JoinArguments(
        string LeftTable,
        string LeftField,
        string RightTable,
        string RightField);
}
