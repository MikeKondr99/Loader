using Loader.Core.Exceptions;
using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Json;

public sealed class ClickHouseJsonFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseJsonFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGet возвращает raw JSON или NULL")]
    [Arguments("'{\"user\":{\"name\":\"Mike\"}}'.JsonGet('$.user')", "{\"name\":\"Mike\"}")]
    [Arguments("'{\"items\":[{\"id\":1},{\"id\":2}]}'.JsonGet('$.items[1]')", "{\"id\":2}")]
    [Arguments("'[10,20]'.JsonGet('$[1]')", "20")]
    [Arguments("'{\"name\":\"Mike\"}'.JsonGet('$.name')", "\"Mike\"")]
    [Arguments("'{\"id\":42}'.JsonGet('$.id')", "42")]
    [Arguments("'{\"active\":true}'.JsonGet('$.active')", "true")]
    [Arguments("'{\"id\":42}'.JsonGet('$.missing')", null)]
    [Arguments("'not-json'.JsonGet('$.name')", null)]
    [Arguments("null.JsonGet('$.name')", null)]
    public Task Json_get_returns_raw_json_or_null(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGet показывает поддержанные варианты JSONPath")]
    [Arguments("'{\"id\":42,\"name\":\"Mike\"}'.JsonGet('$')", "{\"id\":42,\"name\":\"Mike\"}")]
    [Arguments("'{\"user\":{\"name\":\"Mike\"}}'.JsonGet('$.user.name')", "\"Mike\"")]
    [Arguments("'{\"items\":[10,20]}'.JsonGet('$.items')", "[10,20]")]
    [Arguments("'{\"items\":[10,20]}'.JsonGet('$.items[0]')", "10")]
    [Arguments("'{\"items\":[{\"id\":1},{\"id\":2}]}'.JsonGet('$.items[*].id')", "1")]
    public Task Json_get_supports_json_path_variations(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGet без $ в пути падает ошибкой ClickHouse")]
    public async Task Json_get_path_without_root_marker_fails()
    {
        await Assert.That(async () => await AssertExpressionAsync("'{\"name\":\"Mike\"}'.JsonGet('name')", null))
            .ThrowsExactly<DbExecutionException>();
    }

    [Test]
    [DisplayName("ClickHouse JSON функции не принимают NULL вместо JSONPath")]
    [Arguments("'{\"name\":\"Mike\"}'.JsonGet(null)", "JsonGet")]
    [Arguments("'{\"name\":\"Mike\"}'.JsonGetText(null)", "JsonGetText")]
    [Arguments("'{\"id\":42}'.JsonGetInt(null)", "JsonGetInt")]
    [Arguments("'{\"price\":12.34}'.JsonGetNum(null)", "JsonGetNum")]
    [Arguments("'{\"active\":true}'.JsonGetBool(null)", "JsonGetBool")]
    [Arguments("'{\"name\":\"Mike\"}'.JsonHas(null)", "JsonHas")]
    [Arguments("'{\"name\":\"Mike\"}'.JsonType(null)", "JsonType")]
    [Arguments("'{\"items\":[1,2]}'.JsonLength(null)", "JsonLength")]
    public async Task Json_functions_reject_null_path(string expression, string functionName)
    {
        await Assert.That(async () => await AssertExpressionAsync(expression, null))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage($"Функция '{functionName}' с указанными аргументами не найдена");
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonHas проверяет существование значения по пути")]
    [Arguments("'{\"geometry\":{\"x\":1}}'.JsonHas('$.geometry')", true)]
    [Arguments("'{\"geometry\":null}'.JsonHas('$.geometry')", true)]
    [Arguments("'{\"geometry\":{\"x\":1}}'.JsonHas('$.geometry.x')", true)]
    [Arguments("'{\"geometry\":{\"x\":1}}'.JsonHas('$.missing')", false)]
    [Arguments("'not-json'.JsonHas('$.geometry')", false)]
    [Arguments("null.JsonHas('$.geometry')", null)]
    public Task Json_has_returns_path_existence(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonType возвращает тип JSON значения")]
    [Arguments("'{\"value\":1}'.JsonType('$.value')", "Int64")]
    [Arguments("'{\"value\":12.34}'.JsonType('$.value')", "Double")]
    [Arguments("'{\"value\":true}'.JsonType('$.value')", "Bool")]
    [Arguments("'{\"value\":\"text\"}'.JsonType('$.value')", "String")]
    [Arguments("'{\"value\":[1,2]}'.JsonType('$.value')", "Array")]
    [Arguments("'{\"value\":{\"x\":1}}'.JsonType('$.value')", "Object")]
    [Arguments("'{\"value\":null}'.JsonType('$.value')", "Null")]
    [Arguments("'{\"value\":1}'.JsonType('$.missing')", null)]
    [Arguments("null.JsonType('$.value')", null)]
    public Task Json_type_returns_value_type_or_null(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonType без пути возвращает тип root JSON значения")]
    [Arguments("'1'.JsonType()", "Int64")]
    [Arguments("'true'.JsonType()", "Bool")]
    [Arguments("'\"text\"'.JsonType()", "String")]
    [Arguments("'[1,2]'.JsonType()", "Array")]
    [Arguments("'{\"x\":1}'.JsonType()", "Object")]
    [Arguments("'null'.JsonType()", "Null")]
    [Arguments("null.JsonType()", null)]
    public Task Json_type_without_path_returns_root_value_type(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonLength возвращает длину JSON array/object")]
    [Arguments("'{\"items\":[1,2,3]}'.JsonLength('$.items')", 3)]
    [Arguments("'{\"obj\":{\"a\":1,\"b\":2}}'.JsonLength('$.obj')", 2)]
    [Arguments("'{\"value\":1}'.JsonLength('$.value')", 0)]
    [Arguments("'{\"value\":1}'.JsonLength('$.missing')", null)]
    [Arguments("null.JsonLength('$.items')", null)]
    public Task Json_length_returns_array_or_object_length(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonLength без пути возвращает длину root JSON array/object")]
    [Arguments("'[1,2,3]'.JsonLength()", 3)]
    [Arguments("'{\"a\":1,\"b\":2}'.JsonLength()", 2)]
    [Arguments("'1'.JsonLength()", 0)]
    [Arguments("null.JsonLength()", null)]
    public Task Json_length_without_path_returns_root_array_or_object_length(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetText возвращает текст или NULL")]
    [Arguments("'{\"user\":{\"name\":\"Mike\"}}'.JsonGetText('$.user.name')", "Mike")]
    [Arguments("'{\"items\":[{\"name\":\"first\"},{\"name\":\"second\"}]}'.JsonGetText('$.items[1].name')", "second")]
    [Arguments("'{\"empty\":\"\"}'.JsonGetText('$.empty')", "")]
    [Arguments("'{\"value\":true}'.JsonGetText('$.value')", "true")]
    [Arguments("'{\"value\":false}'.JsonGetText('$.value')", "false")]
    [Arguments("'{\"value\":42}'.JsonGetText('$.value')", "42")]
    [Arguments("'{\"value\":12.34}'.JsonGetText('$.value')", "12.34")]
    [Arguments("'{\"user\":{\"name\":\"Mike\"}}'.JsonGet('$.user.name').JsonGetText()", "Mike")]
    [Arguments("'{\"user\":{\"name\":\"Mike\"}}'.JsonGetText('$.missing')", null)]
    [Arguments("'not-json'.JsonGetText('$.name')", null)]
    [Arguments("null.JsonGetText('$.name')", null)]
    public Task Json_get_text_returns_text_or_null(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetText без пути читает root scalar")]
    [Arguments("'\"Mike\"'.JsonGetText()", "Mike")]
    [Arguments("'42'.JsonGetText()", "42")]
    [Arguments("'true'.JsonGetText()", "true")]
    [Arguments("null.JsonGetText()", null)]
    public Task Json_get_text_without_path_reads_root_scalar(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetInt возвращает целое число или NULL")]
    [Arguments("'{\"id\":42}'.JsonGetInt('$.id')", 42)]
    [Arguments("'{\"items\":[10,20]}'.JsonGetInt('$.items[1]')", 20)]
    [Arguments("'{\"id\":\"42\"}'.JsonGetInt('$.id')", 42)]
    [Arguments("'{\"id\":12.34}'.JsonGetInt('$.id')", null)]
    [Arguments("'{\"id\":\"not-int\"}'.JsonGetInt('$.id')", null)]
    [Arguments("'{\"id\":true}'.JsonGetInt('$.id')", null)]
    [Arguments("'{\"id\":false}'.JsonGetInt('$.id')", null)]
    [Arguments("'{\"id\":42}'.JsonGetInt('$.missing')", null)]
    [Arguments("'not-json'.JsonGetInt('$.id')", null)]
    [Arguments("null.JsonGetInt('$.id')", null)]
    public Task Json_get_int_returns_integer_or_null(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetInt без пути читает root scalar")]
    [Arguments("'42'.JsonGetInt()", 42)]
    [Arguments("'12.34'.JsonGetInt()", null)]
    [Arguments("'\"42\"'.JsonGetInt()", 42)]
    [Arguments("null.JsonGetInt()", null)]
    public Task Json_get_int_without_path_reads_root_scalar(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetNum возвращает дробное число или NULL")]
    [Arguments("'{\"price\":12.34}'.JsonGetNum('$.price')", 12.34)]
    [Arguments("'{\"price\":42}'.JsonGetNum('$.price')", 42.0)]
    [Arguments("'{\"price\":\"12.34\"}'.JsonGetNum('$.price')", 12.34)]
    [Arguments("'{\"price\":\"12,34\"}'.JsonGetNum('$.price')", null)]
    [Arguments("'{\"price\":\"42\"}'.JsonGetNum('$.price')", 42.0)]
    [Arguments("'{\"price\":\"not-num\"}'.JsonGetNum('$.price')", null)]
    [Arguments("'{\"price\":true}'.JsonGetNum('$.price')", null)]
    [Arguments("'{\"price\":false}'.JsonGetNum('$.price')", null)]
    [Arguments("'{\"price\":12.34}'.JsonGetNum('$.missing')", null)]
    [Arguments("'not-json'.JsonGetNum('$.price')", null)]
    [Arguments("null.JsonGetNum('$.price')", null)]
    public Task Json_get_num_returns_number_or_null(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetNum без пути читает root scalar")]
    [Arguments("'12.34'.JsonGetNum()", 12.34)]
    [Arguments("'42'.JsonGetNum()", 42.0)]
    [Arguments("'\"12.34\"'.JsonGetNum()", 12.34)]
    [Arguments("'\"12,34\"'.JsonGetNum()", null)]
    [Arguments("null.JsonGetNum()", null)]
    public Task Json_get_num_without_path_reads_root_scalar(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetBool возвращает логическое значение или NULL")]
    [Arguments("'{\"active\":true}'.JsonGetBool('$.active')", true)]
    [Arguments("'{\"active\":false}'.JsonGetBool('$.active')", false)]
    [Arguments("'{\"active\":1}'.JsonGetBool('$.active')", true)]
    [Arguments("'{\"active\":0}'.JsonGetBool('$.active')", false)]
    [Arguments("'{\"active\":\"true\"}'.JsonGetBool('$.active')", true)]
    [Arguments("'{\"active\":\"false\"}'.JsonGetBool('$.active')", false)]
    [Arguments("'{\"active\":\"1\"}'.JsonGetBool('$.active')", true)]
    [Arguments("'{\"active\":\"0\"}'.JsonGetBool('$.active')", false)]
    [Arguments("'{\"active\":\"not-bool\"}'.JsonGetBool('$.active')", null)]
    [Arguments("'{\"active\":true}'.JsonGetBool('$.missing')", null)]
    [Arguments("'not-json'.JsonGetBool('$.active')", null)]
    [Arguments("null.JsonGetBool('$.active')", null)]
    public Task Json_get_bool_returns_boolean_or_null(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("ClickHouse JSON JsonGetBool без пути читает root scalar")]
    [Arguments("'true'.JsonGetBool()", true)]
    [Arguments("'false'.JsonGetBool()", false)]
    [Arguments("'1'.JsonGetBool()", true)]
    [Arguments("'0'.JsonGetBool()", false)]
    [Arguments("'\"true\"'.JsonGetBool()", true)]
    [Arguments("'\"false\"'.JsonGetBool()", false)]
    [Arguments("'\"not-bool\"'.JsonGetBool()", null)]
    [Arguments("null.JsonGetBool()", null)]
    public Task Json_get_bool_without_path_reads_root_scalar(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}
