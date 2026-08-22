using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Template;

namespace Loader.Query.Resolve;

/// <summary>
/// Минимальное описание функции, достаточное resolver-у для типа и SQL-шаблона.
/// </summary>
public sealed record FunctionDefinition
{
    /// <summary>
    /// Имя функции в выражениях Loader.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Пользовательское описание функции для документации и подсказок.
    /// </summary>
    public string? Doc { get; init; }

    /// <summary>
    /// Описание аргументов функции и их требований к типам/nullability/константности.
    /// </summary>
    public required IReadOnlyList<FunctionArgument> Arguments { get; init; }

    /// <summary>
    /// Тип результата функции после успешного выбора overload-а.
    /// </summary>
    public required FunctionReturnType ReturnType { get; init; }

    /// <summary>
    /// Динамический тип результата, которому кроме аргументов нужен context resolve-а выражений.
    /// Используется редкими функциями, чей тип зависит от внешней metadata.
    /// </summary>
    public Func<IReadOnlyList<ResolvedExpression>, ExpressionResolutionContext, FunctionReturnType>? ContextReturnTypeProvider { get; init; }

    /// <summary>
    /// Синтаксический вид функции: обычная функция, method, unary или binary operator.
    /// </summary>
    public required FuncExprKind Kind { get; init; }

    /// <summary>
    /// Статический SQL-шаблон функции. Используется большинством функций.
    /// </summary>
    public required ITemplate Template { get; init; }

    /// <summary>
    /// Динамический SQL-шаблон, которому кроме аргументов может понадобиться внешний read-only context.
    /// Используется редкими функциями, чей SQL зависит от resolved типов или внешней metadata.
    /// </summary>
    public Func<IReadOnlyList<ResolvedExpression>, ExpressionResolutionContext, ITemplate>? TemplateProvider { get; init; }

    /// <summary>
    /// Метаданные implicit cast-а, если эта definition описывает не пользовательскую функцию, а cast.
    /// </summary>
    public ImplicitCastMetadata? ImplicitCast { get; init; }

    /// <summary>
    /// Переопределение стандартного null propagation для функций с особой семантикой.
    /// </summary>
    public Func<IEnumerable<bool>, bool>? CustomNullPropagation { get; init; }

    /// <summary>
    /// Правила вывода константности результата функции.
    /// </summary>
    public required ConstPropagation ConstPropagation { get; init; }

    public override string ToString()
    {
        return Kind is FuncExprKind.Binary
            ? $"({Arguments[0].Type} {Name} {Arguments[1].Type}) -> {ReturnType}"
            : $"{Name}({string.Join(", ", Arguments.Select(static argument => argument.ToString()))}) -> {ReturnType}";
    }
}
