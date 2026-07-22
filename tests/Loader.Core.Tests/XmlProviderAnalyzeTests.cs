using Loader.Core.Exceptions;
using Loader.Core.Providers.Xml;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions.Enums;

namespace Loader.Core.Tests;

public sealed class XmlProviderAnalyzeTests
{
    private static readonly XmlProvider Provider = new();

    [Test]
    [DisplayName("XML AnalyzeSchema для PIX BI PurchaseOrder находит атрибуты и leaf-элементы Address")]
    public async Task Analyze_schema_reads_pix_bi_purchase_order_shape()
    {
        var source = new InlineXml(PurchaseOrderXml);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "Address");

        await Assert.That(schema.Columns.Select(static column => column.Name).ToArray())
            .IsEquivalentTo(
                ["Type", "Name", "Street", "City", "State", "Zip", "Country"],
                CollectionOrdering.Matching);
        await Assert.That(schema.Columns.Select(static column => column.Path).ToArray())
            .IsEquivalentTo(
                ["@Type", "Name", "Street", "City", "State", "Zip", "Country"],
                CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("XML AnalyzeSchema собирает union колонок в порядке первого появления")]
    public async Task Analyze_schema_collects_union_in_first_seen_order()
    {
        var source = new InlineXml(
            """
            <root>
              <row source="first"><id>1</id><name>Mike</name></row>
              <row source="second"><amount>10.50</amount><id>2</id></row>
            </root>
            """);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row");

        await Assert.That(schema.Columns.Select(static column => column.Name).ToArray())
            .IsEquivalentTo(["source", "id", "name", "amount"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("XML AnalyzeSchema находит колонку со значением большого размера")]
    public async Task Analyze_schema_collects_column_with_large_value()
    {
        var largeValue = new string('x', 100_000);
        var source = new InlineXml($"<root><row><id>1</id><payload>{largeValue}</payload></row></root>");

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row");

        await Assert.That(schema.Columns.Select(static column => column.Name).ToArray())
            .IsEquivalentTo(["id", "payload"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("XML AnalyzeSchema flat режима не добавляет вложенные структуры в схему")]
    public async Task Analyze_schema_skips_nested_elements()
    {
        var source = new InlineXml(
            """
            <root>
              <row><id>1</id><user><name>Mike</name></user><city>Moscow</city></row>
            </root>
            """);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row");

        await Assert.That(schema.Columns.Select(static column => column.Name).ToArray())
            .IsEquivalentTo(["id", "city"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("XML AnalyzeSchema находит строки выбранной таблицы на разной глубине")]
    public async Task Analyze_schema_finds_table_elements_at_different_depths()
    {
        var source = new InlineXml(
            """
            <root>
              <row><id>1</id></row>
              <wrapper><items><row><name>nested</name></row></items></wrapper>
            </root>
            """);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row");

        await Assert.That(schema.Columns.Select(static column => column.Name).ToArray())
            .IsEquivalentTo(["id", "name"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("XML AnalyzeSchema не превращает namespace declarations в колонки")]
    public async Task Analyze_schema_ignores_namespace_declarations()
    {
        var source = new InlineXml(
            "<root><row xmlns=\"urn:rows\" xmlns:meta=\"urn:meta\" meta:source=\"api\"><id>1</id></row></root>");

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row");

        await Assert.That(schema.Columns.Select(static column => column.Name).ToArray())
            .IsEquivalentTo(["source", "id"], CollectionOrdering.Matching);
        await Assert.That(schema.Columns.Select(static column => column.Path).ToArray())
            .IsEquivalentTo(["@source", "id"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("XML AnalyzeSchema сравнивает имя таблицы с учетом регистра")]
    public async Task Analyze_schema_table_name_is_case_sensitive()
    {
        var source = new InlineXml("<root><Row><id>1</id></Row></root>");

        await Assert.That(async () => await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row"))
            .ThrowsExactly<XmlTableNotFoundProviderException>()
            .WithMessage("XML file 'inline.xml' does not contain table element 'row'.");
    }

    [Test]
    [DisplayName("XML AnalyzeSchema при совпадении имени атрибута и элемента сообщает неоднозначную схему")]
    public async Task Analyze_schema_attribute_and_element_name_collision_throws_invalid_schema()
    {
        var source = new InlineXml("<root><row id=\"attribute\"><id>element</id></row></root>");

        await Assert.That(async () => await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row"))
            .ThrowsExactly<XmlInvalidSchemaProviderException>()
            .WithMessage("XML file 'inline.xml' has invalid schema: duplicate column name 'id'.");
    }

    [Test]
    [DisplayName("XML AnalyzeSchema без выбранной таблицы кидает специальный provider exception")]
    public async Task Analyze_schema_missing_table_throws_provider_exception()
    {
        var source = new InlineXml("<root><other><id>1</id></other></root>");

        await Assert.That(async () => await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row"))
            .ThrowsExactly<XmlTableNotFoundProviderException>()
            .WithMessage("XML file 'inline.xml' does not contain table element 'row'.");
    }

    [Test]
    [DisplayName("XML AnalyzeSchema поврежденного документа кидает XmlFileOpenProviderException")]
    public async Task Analyze_schema_malformed_xml_throws_file_exception()
    {
        var source = new InlineXml("<root><row><id>1</row></root>");

        await Assert.That(async () => await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row"))
            .ThrowsExactly<XmlFileOpenProviderException>()
            .WithMessage("XML file 'inline.xml' could not be opened or parsed.");
    }

    [Test]
    [DisplayName("XML AnalyzeSchema запрещает DTD и внешние сущности")]
    public async Task Analyze_schema_rejects_dtd()
    {
        var source = new InlineXml(
            "<!DOCTYPE root [<!ENTITY value SYSTEM \"file:///secret.txt\">]><root><row><id>&value;</id></row></root>");

        await Assert.That(async () => await Provider.AnalyzeSchemaAsync(source, "inline.xml", "row"))
            .ThrowsExactly<XmlFileOpenProviderException>()
            .WithMessage("XML file 'inline.xml' could not be opened or parsed.");
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
