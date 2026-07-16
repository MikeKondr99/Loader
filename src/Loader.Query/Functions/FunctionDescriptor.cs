using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using Loader.Query.Template;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Functions;

/// <summary>
/// Базовый поставщик группы функций. Повторяет подход ReData: группа описывает функции через builder.
/// </summary>
public abstract class FunctionDescriptor
{
    private readonly List<FunctionBuilder> builders = [];

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        builders.Clear();
        DefineFunctions();
        return builders.Select(static builder => builder.Build()).ToArray();
    }

    protected abstract void DefineFunctions();

    protected FunctionBuilder Function(string name)
    {
        var builder = FunctionBuilder.Function(name);
        builders.Add(builder);
        return builder;
    }

    protected FunctionBuilder Method(string name)
    {
        var builder = FunctionBuilder.Method(name);
        builders.Add(builder);
        return builder;
    }

    protected FunctionBuilder Binary(string name)
    {
        var builder = FunctionBuilder.Binary(name);
        builders.Add(builder);
        return builder;
    }

    protected FunctionBuilder Binary(string name, DataType left, DataType right)
    {
        var builder = FunctionBuilder.Binary(name)
            .Arg("left", left)
            .Arg("right", right);
        builders.Add(builder);
        return builder;
    }

    protected FunctionBuilder Unary(string name)
    {
        var builder = FunctionBuilder.Unary(name);
        builders.Add(builder);
        return builder;
    }
}

public sealed class FunctionBuilder
{
    private readonly List<FunctionArgument> arguments = [];
    private string? doc;
    private FunctionReturnType? returnType;
    private QueryTemplate? template;
    private Func<IReadOnlyList<ResolvedExpression>, ITemplate>? templateProvider;
    private Func<IEnumerable<bool>, bool>? customNullPropagation;
    private uint? implicitCastCost;
    private ConstPropagation constPropagation = ConstPropagation.Default;

    private FunctionBuilder(string name, FuncExprKind kind)
    {
        Name = name;
        Kind = kind;
    }

    private string Name { get; }

    private FuncExprKind Kind { get; }

    public static FunctionBuilder Function(string name)
    {
        return new FunctionBuilder(name, FuncExprKind.Default);
    }

    public static FunctionBuilder Method(string name)
    {
        return new FunctionBuilder(name, FuncExprKind.Method);
    }

    public static FunctionBuilder Binary(string name)
    {
        return new FunctionBuilder(name, FuncExprKind.Binary);
    }

    public static FunctionBuilder Unary(string name)
    {
        return new FunctionBuilder(name, FuncExprKind.Unary);
    }

    public FunctionBuilder Doc(string value)
    {
        doc = value;
        return this;
    }

    public FunctionBuilder Arg(string name, DataType type)
    {
        return Arg(name, type, propagateNull: true);
    }

    public FunctionBuilder Arg(string name, DataType type, bool propagateNull)
    {
        arguments.Add(new FunctionArgument
        {
            Name = name,
            Type = new FunctionArgumentType
            {
                DataType = type,
                CanBeNull = true
            },
            PropagateNull = propagateNull
        });
        return this;
    }

    public FunctionBuilder ReqArg(string name, DataType type)
    {
        arguments.Add(new FunctionArgument
        {
            Name = name,
            Type = new FunctionArgumentType
            {
                DataType = type,
                CanBeNull = false
            },
            PropagateNull = false
        });
        return this;
    }

    public FunctionBuilder Returns(DataType type)
    {
        return Returns(type, ConstPropagation.Default);
    }

    public FunctionBuilder Returns(DataType type, ConstPropagation propagation)
    {
        returnType = new FunctionReturnType
        {
            DataType = type,
            CanBeNull = true
        };
        constPropagation = propagation;
        return this;
    }

    public FunctionBuilder ReturnsNotNull(DataType type)
    {
        return ReturnsNotNull(type, ConstPropagation.Default);
    }

    public FunctionBuilder ReturnsNotNull(DataType type, ConstPropagation propagation)
    {
        returnType = new FunctionReturnType
        {
            DataType = type,
            CanBeNull = false
        };
        constPropagation = propagation;
        return this;
    }

    public FunctionBuilder ImplicitCast(uint cost)
    {
        implicitCastCost = cost;
        return this;
    }

    public FunctionBuilder CustomNullPropagation(Func<IEnumerable<bool>, bool> propagation)
    {
        customNullPropagation = propagation;
        return this;
    }

    public FunctionBuilder Template(TemplateInterpolatedStringHandler value)
    {
        template = value.Compile();
        return this;
    }

    public FunctionBuilder Template(Func<IReadOnlyList<ResolvedExpression>, ITemplate> provider)
    {
        template = QueryTemplate.Text(string.Empty);
        templateProvider = provider;
        return this;
    }

    public FunctionDefinition Build()
    {
        if (returnType is null)
        {
            throw new InvalidOperationException($"Function '{Name}' has no return type.");
        }

        if (template is null)
        {
            throw new InvalidOperationException($"Function '{Name}' has no template.");
        }

        return new FunctionDefinition
        {
            Name = Name,
            Doc = doc,
            Arguments = arguments,
            ReturnType = returnType,
            Kind = Kind,
            Template = template.Value,
            TemplateProvider = templateProvider,
            ImplicitCast = implicitCastCost is null
                ? null
                : new ImplicitCastMetadata
                {
                    Cost = implicitCastCost.Value
                },
            CustomNullPropagation = customNullPropagation,
            ConstPropagation = constPropagation
        };
    }
}
