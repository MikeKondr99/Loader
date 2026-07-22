using Loader.Core.Exceptions;
using Loader.Core.Providers.Xml;
using Loader.Core.Tests.Infrastructure;
using System.Text;

namespace Loader.Core.Tests;

public sealed class XmlProviderTests
{
    private static readonly XmlProvider Provider = new();

    [Test]
    [DisplayName("XML PIX BI PurchaseOrder читает Address потоково и возвращает все значения строками")]
    public async Task Reads_pix_bi_purchase_order_addresses_as_strings()
    {
        var source = new InlineXml(PurchaseOrderXml);
        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "Address");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new XmlTableConfig
            {
                FileName = "inline.xml",
                TableName = "Address",
                Schema = schema
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["Type", "Name", "Street", "City", "State", "Zip", "Country"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("Shipping", "Ellen Adams", "123 Maple Street", "Mill Valley", "CA", "10999", "USA"),
                ("Billing", "Tai Yee", "8 Oak Avenue", "Old Town", "PA", "95819", "USA")
            ]);
    }

    [Test]
    [DisplayName("XML отсутствующий элемент дает DBNull, пустой элемент дает пустую строку")]
    public async Task Distinguishes_missing_element_from_empty_element()
    {
        var source = new InlineXml(
            """
            <root>
              <row><id>1</id><value /></row>
              <row><id>2</id></row>
            </root>
            """);

        await using var rawReader = await OpenAsync(source, Schema("id", "value"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "value"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", ""),
                ("2", DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("XML пустой элемент таблицы является строкой со значениями DBNull")]
    public async Task Empty_table_element_is_row_with_dbnull_values()
    {
        var source = new InlineXml("<root><row /></root>");

        await using var rawReader = await OpenAsync(source, Schema("id"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id"],
            types: [DataType.Text],
            rows: [ValueTuple.Create((object)DBNull.Value)]);
    }

    [Test]
    [DisplayName("XML числа даты и boolean остаются исходными строками")]
    public async Task Primitive_looking_values_remain_strings()
    {
        var source = new InlineXml(
            "<root><row><integer>-42</integer><number>10.50</number><boolean>true</boolean><date>2026-01-02</date></row></root>");

        await using var rawReader = await OpenAsync(source, Schema("integer", "number", "boolean", "date"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["integer", "number", "boolean", "date"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [("-42", "10.50", "true", "2026-01-02")]);
    }

    [Test]
    [DisplayName("XML xsi:nil значение дает DBNull")]
    public async Task Xsi_nil_element_returns_dbnull()
    {
        var source = new InlineXml(
            """
            <root xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <row><id>1</id><value xsi:nil="true" /></row>
            </root>
            """);

        await using var rawReader = await OpenAsync(source, Schema("id", "value"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "value"],
            types: [DataType.Text, DataType.Text],
            rows: [("1", DBNull.Value)]);
    }

    [Test]
    [DisplayName("XML текст null остается строкой и отличается от xsi:nil")]
    public async Task Null_text_remains_string_and_differs_from_xsi_nil()
    {
        var source = new InlineXml(
            """
            <root xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <row><value>null</value></row>
              <row><value xsi:nil="true" /></row>
            </root>
            """);

        await using var rawReader = await OpenAsync(source, Schema("value"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows:
            [
                ValueTuple.Create((object)"null"),
                ValueTuple.Create((object)DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("XML читает Unicode, entity и CDATA как текст")]
    public async Task Reads_unicode_entities_and_cdata_as_text()
    {
        var source = new InlineXml(
            """
            <root>
              <row>
                <city>Москва &amp; London</city>
                <emoji>🚀</emoji>
                <rtl>مرحبا</rtl>
                <raw><![CDATA[<tag>value</tag>]]></raw>
              </row>
            </root>
            """);

        await using var rawReader = await OpenAsync(source, Schema("city", "emoji", "rtl", "raw"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["city", "emoji", "rtl", "raw"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [("Москва & London", "🚀", "مرحبا", "<tag>value</tag>")]);
    }

    [Test]
    [DisplayName("XML declaration позволяет XmlReader определить UTF-16 кодировку")]
    public async Task Reads_utf16_from_xml_declaration()
    {
        var source = new InlineXml(
            "<?xml version=\"1.0\" encoding=\"utf-16\"?><root><row><city>Москва</city></row></root>",
            Encoding.Unicode);

        await using var rawReader = await OpenAsync(source, Schema("city"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["city"],
            types: [DataType.Text],
            rows: [ValueTuple.Create("Москва")]);
    }

    [Test]
    [DisplayName("XML AnalyzeSchema затем OpenReader читает данные по полученной схеме")]
    public async Task Analyze_schema_then_reads_rows()
    {
        var source = new InlineXml(
            "<root><row source=\"api\"><id>1</id><name>Mike</name></row><row source=\"file\"><id>2</id></row></root>");
        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row");

        await using var rawReader = await OpenAsync(source, schema);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["source", "id", "name"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows:
            [
                ("api", "1", "Mike"),
                ("file", "2", DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("XML большое поле вне схемы пропускается и не влияет на следующую колонку")]
    public async Task Skips_large_unknown_element_before_known_value()
    {
        var largeValue = new string('x', 100_000);
        var source = new InlineXml($"<root><row><ignored>{largeValue}</ignored><id>42</id></row></root>");

        await using var rawReader = await OpenAsync(source, Schema("id"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id"],
            types: [DataType.Text],
            rows: [ValueTuple.Create("42")]);
    }

    [Test]
    [DisplayName("XML большое содержимое до выбранной таблицы пропускается")]
    public async Task Skips_large_content_before_selected_table()
    {
        var largeValue = new string('n', 100_000);
        var source = new InlineXml(
            $"<root><metadata><payload>{largeValue}</payload></metadata><row><id>42</id></row></root>");

        await using var rawReader = await OpenAsync(source, Schema("id"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id"],
            types: [DataType.Text],
            rows: [ValueTuple.Create("42")]);
    }

    [Test]
    [DisplayName("XML большое известное значение хранит только текущую строку")]
    public async Task Reads_large_known_value()
    {
        var largeValue = new string('y', 100_000);
        var source = new InlineXml($"<root><row><payload>{largeValue}</payload></row></root>");

        await using var rawReader = await OpenAsync(source, Schema("payload"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["payload"],
            types: [DataType.Text],
            rows: [ValueTuple.Create(largeValue)]);
    }

    [Test]
    [DisplayName("XML явная схема меняет порядок и имена колонок")]
    public async Task Explicit_schema_controls_column_order_and_names()
    {
        var source = new InlineXml("<root><row source=\"api\"><id>42</id><name>Mike</name></row></root>");
        var schema = new XmlTableSchema
        {
            Columns =
            [
                new XmlColumnSchema { Name = "display_name", Path = "name" },
                new XmlColumnSchema { Name = "origin", Path = "@source" },
                new XmlColumnSchema { Name = "identifier", Path = "id" }
            ]
        };

        await using var rawReader = await OpenAsync(source, schema);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["display_name", "origin", "identifier"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [("Mike", "api", "42")]);
    }

    [Test]
    [DisplayName("XML колонка схемы без значения в строке дает DBNull")]
    public async Task Schema_column_absent_from_xml_returns_dbnull()
    {
        var source = new InlineXml("<root><row><id>1</id></row></root>");

        await using var rawReader = await OpenAsync(source, Schema("id", "unknown"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "unknown"],
            types: [DataType.Text, DataType.Text],
            rows: [("1", DBNull.Value)]);
    }

    [Test]
    [DisplayName("XML nested элемент в явно заданной flat колонке дает DBNull")]
    public async Task Nested_element_requested_by_flat_schema_returns_dbnull()
    {
        var source = new InlineXml(
            "<root><row><id>1</id><user><name>Mike</name></user></row></root>");

        await using var rawReader = await OpenAsync(source, Schema("id", "user"));
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "user"],
            types: [DataType.Text, DataType.Text],
            rows: [("1", DBNull.Value)]);
    }

    [Test]
    [DisplayName("XML ReadAsync читает несколько строк асинхронно")]
    public async Task Read_async_reads_multiple_rows()
    {
        var source = new InlineXml("<root><row><id>1</id></row><row><id>2</id></row></root>");
        await using var reader = await OpenAsync(source, Schema("id"));

        await Assert.That(reader.HasRows).IsTrue();
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetString(0)).IsEqualTo("1");
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetFieldValue<string>(0)).IsEqualTo("2");
        await Assert.That(await reader.ReadAsync()).IsFalse();
    }

    [Test]
    [DisplayName("XML GetString на DBNull кидает InvalidCastException")]
    public async Task String_accessor_throws_for_dbnull()
    {
        var source = new InlineXml("<root><row><id>1</id></row></root>");
        await using var reader = await OpenAsync(source, Schema("id", "missing"));

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.IsDBNull(1)).IsTrue();
        await Assert.That(() => reader.GetString(1))
            .ThrowsExactly<InvalidCastException>()
            .WithMessage("Column 'missing' contains DBNull.");
    }

    [Test]
    [DisplayName("XML имя таблицы и колонок учитывает регистр")]
    public async Task Table_and_column_names_are_case_sensitive()
    {
        var source = new InlineXml("<root><row><Id>1</Id><id>2</id></row></root>");

        await using var reader = await OpenAsync(source, Schema("Id", "id"));

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader["Id"]).IsEqualTo("1");
        await Assert.That(reader["id"]).IsEqualTo("2");
        await Assert.That(() => reader.GetOrdinal("ID")).ThrowsExactly<IndexOutOfRangeException>();
    }

    [Test]
    [DisplayName("XML без выбранной таблицы кидает специальный provider exception")]
    public async Task Missing_table_throws_provider_exception()
    {
        var source = new InlineXml("<root><other><id>1</id></other></root>");

        await Assert.That(async () => await OpenAsync(source, Schema("id")))
            .ThrowsExactly<XmlTableNotFoundProviderException>()
            .WithMessage("XML file 'inline.xml' does not contain table element 'row'.");
    }

    [Test]
    [DisplayName("XML ошибка после первой строки нормализуется при следующем Read")]
    public async Task Malformed_xml_after_first_row_throws_file_exception_on_read()
    {
        var source = new InlineXml("<root><row><id>1</id></row><row><id>2</row></root>");
        await using var reader = await OpenAsync(source, Schema("id"));

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetString(0)).IsEqualTo("1");
        await Assert.That(() => reader.Read())
            .ThrowsExactly<XmlFileOpenProviderException>()
            .WithMessage("XML file 'inline.xml' could not be opened or parsed.");
    }

    [Test]
    [DisplayName("XML поврежденный до первой строки кидает XmlFileOpenProviderException при открытии")]
    public async Task Malformed_xml_before_first_row_throws_file_exception_on_open()
    {
        var source = new InlineXml("<root><row><id>1</row></root>");

        await Assert.That(async () => await OpenAsync(source, Schema("id")))
            .ThrowsExactly<XmlFileOpenProviderException>()
            .WithMessage("XML file 'inline.xml' could not be opened or parsed.");
    }

    [Test]
    [DisplayName("XML duplicate column name в схеме кидает invalid schema exception")]
    public async Task Duplicate_schema_column_name_throws_invalid_schema()
    {
        var source = new InlineXml("<root><row><id>1</id></row></root>");
        var schema = new XmlTableSchema
        {
            Columns =
            [
                new XmlColumnSchema { Name = "id", Path = "id" },
                new XmlColumnSchema { Name = "id", Path = "other" }
            ]
        };

        await Assert.That(async () => await OpenAsync(source, schema))
            .ThrowsExactly<XmlInvalidSchemaProviderException>()
            .WithMessage("XML file 'inline.xml' has invalid schema: duplicate column name 'id'.");
    }

    [Test]
    [DisplayName("XML duplicate column path в схеме кидает invalid schema exception")]
    public async Task Duplicate_schema_column_path_throws_invalid_schema()
    {
        var source = new InlineXml("<root><row><id>1</id></row></root>");
        var schema = new XmlTableSchema
        {
            Columns =
            [
                new XmlColumnSchema { Name = "first_id", Path = "id" },
                new XmlColumnSchema { Name = "second_id", Path = "id" }
            ]
        };

        await Assert.That(async () => await OpenAsync(source, schema))
            .ThrowsExactly<XmlInvalidSchemaProviderException>()
            .WithMessage("XML file 'inline.xml' has invalid schema: duplicate column path 'id'.");
    }

    [Test]
    [DisplayName("XML nested path в схеме кидает invalid schema exception")]
    public async Task Nested_schema_path_throws_invalid_schema()
    {
        var source = new InlineXml("<root><row><user><name>Mike</name></user></row></root>");
        var schema = new XmlTableSchema
        {
            Columns = [new XmlColumnSchema { Name = "name", Path = "user/name" }]
        };

        await Assert.That(async () => await OpenAsync(source, schema))
            .ThrowsExactly<XmlInvalidSchemaProviderException>()
            .WithMessage("XML file 'inline.xml' has invalid schema: column path 'user/name' is not a flat XML path.");
    }

    [Test]
    [DisplayName("XML после Normalize поддерживает Where по строковым колонкам")]
    public async Task Normalize_where_filters_xml_rows()
    {
        var source = new InlineXml(
            "<root><row><id>1</id><city>Moscow</city></row><row><id>2</id><city>London</city></row></root>");
        await using var rawReader = await OpenAsync(source, Schema("id", "city"));
        await using var reader = rawReader
            .Normalize()
            .Where(static row => row.Text("city") == "Moscow");

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Text, DataType.Text],
            rows: [("1", "Moscow")]);
    }

    private static ValueTask<System.Data.Common.DbDataReader> OpenAsync(
        InlineXml source,
        XmlTableSchema schema)
    {
        return Provider.OpenReaderAsync(
            source,
            new XmlTableConfig
            {
                FileName = "inline.xml",
                TableName = "row",
                Schema = schema
            });
    }

    private static XmlTableSchema Schema(params string[] paths)
    {
        return new XmlTableSchema
        {
            Columns = paths
                .Select(static path => new XmlColumnSchema { Name = path.TrimStart('@'), Path = path })
                .ToArray()
        };
    }

    private const string PurchaseOrderXml = """
        <?xml version="1.0"?>
        <PurchaseOrder PurchaseOrderNumber="99503" OrderDate="1999-10-20">
          <Address Type="Shipping">
            <Name>Ellen Adams</Name>
            <Street>123 Maple Street</Street>
            <City>Mill Valley</City>
            <State>CA</State>
            <Zip>10999</Zip>
            <Country>USA</Country>
          </Address>
          <Address Type="Billing">
            <Name>Tai Yee</Name>
            <Street>8 Oak Avenue</Street>
            <City>Old Town</City>
            <State>PA</State>
            <Zip>95819</Zip>
            <Country>USA</Country>
          </Address>
        </PurchaseOrder>
        """;
}
