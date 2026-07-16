using System.Text;
using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Core.Tests;

public sealed class ClickHouseSqlTests
{
    [Test]
    [MethodDataSource(nameof(IdentifierCases))]
    [DisplayName("ClickHouseSql WriteIdentifier экранирует identifier")]
    public async Task Write_identifier_quotes_and_escapes_identifier(string value, string expected)
    {
        var builder = new StringBuilder();

        ClickHouseSql.WriteIdentifier(builder, value);

        await Assert.That(builder.ToString()).IsEqualTo(expected);
    }

    [Test]
    [DisplayName("ClickHouseSql WriteTableNameSql пишет только table без database")]
    public async Task Write_table_name_without_database()
    {
        var builder = new StringBuilder();

        ClickHouseSql.WriteTableNameSql(
            builder,
            new ClickHouseTableName
            {
                Table = "target"
            });

        await Assert.That(builder.ToString()).IsEqualTo("`target`");
    }

    [Test]
    [DisplayName("ClickHouseSql WriteTableNameSql пишет database и table явно")]
    public async Task Write_table_name_with_database()
    {
        var builder = new StringBuilder();

        ClickHouseSql.WriteTableNameSql(
            builder,
            new ClickHouseTableName
            {
                Database = "loader",
                Table = "target"
            });

        await Assert.That(builder.ToString()).IsEqualTo("`loader`.`target`");
    }

    [Test]
    [DisplayName("ClickHouseSql InsertHeader сохраняет порядок колонок")]
    public async Task Insert_header_preserves_column_order()
    {
        var schema = Schema(
            ("id", DataType.Integer, typeof(int)),
            ("city", DataType.Text, typeof(string)),
            ("amount", DataType.Number, typeof(decimal)));

        var sql = ClickHouseSql.InsertHeader(
            new ClickHouseTableName
            {
                Database = "db",
                Table = "target"
            },
            schema);

        await Assert.That(sql).IsEqualTo("INSERT INTO `db`.`target` (`id`, `city`, `amount`)");
    }

    [Test]
    [DisplayName("ClickHouseSql WriteInsertHeader дописывает в существующий StringBuilder")]
    public async Task Write_insert_header_appends_to_existing_builder()
    {
        var schema = Schema(("id", DataType.Integer, typeof(int)));
        var builder = new StringBuilder("prefix ");

        ClickHouseSql.WriteInsertHeader(
            builder,
            new ClickHouseTableName
            {
                Table = "target"
            },
            schema);

        await Assert.That(builder.ToString()).IsEqualTo("prefix INSERT INTO `target` (`id`)");
    }

    [Test]
    [DisplayName("ClickHouseSql CreateTable собирает create table с engine")]
    public async Task Create_table_builds_create_table_sql_with_engine()
    {
        var schema = Schema(
            ("id", DataType.Integer, typeof(int)),
            ("city", DataType.Text, typeof(string)),
            ("active", DataType.Boolean, typeof(bool)));
        var options = Options("target");

        var sql = ClickHouseSql.CreateTable(schema, meta: null, options, Resolver(options));

        await Assert.That(sql).IsEqualTo(
            "CREATE TABLE `target`" + Environment.NewLine +
            "(" + Environment.NewLine +
            "    `id` Int32," + Environment.NewLine +
            "    `city` String," + Environment.NewLine +
            "    `active` Bool" + Environment.NewLine +
            ")" + Environment.NewLine +
            "ENGINE = Log");
    }

    [Test]
    [DisplayName("ClickHouseSql CreateTable учитывает IF NOT EXISTS database и custom engine")]
    public async Task Create_table_uses_if_not_exists_database_and_custom_engine()
    {
        var schema = Schema(("id", DataType.Integer, typeof(int)));
        var options = new ClickHouseWriteOptions
        {
            TableName = new ClickHouseTableName
            {
                Database = "loader",
                Table = "target"
            },
            IfNotExists = true,
            Engine = "MergeTree ORDER BY tuple()"
        };

        var sql = ClickHouseSql.CreateTable(schema, meta: null, options, Resolver(options));

        await Assert.That(sql).IsEqualTo(
            "CREATE TABLE IF NOT EXISTS `loader`.`target`" + Environment.NewLine +
            "(" + Environment.NewLine +
            "    `id` Int32" + Environment.NewLine +
            ")" + Environment.NewLine +
            "ENGINE = MergeTree ORDER BY tuple()");
    }

    [Test]
    [DisplayName("ClickHouseSql CreateTable экранирует имена колонок")]
    public async Task Create_table_escapes_column_names()
    {
        var schema = Schema(("we`ird", DataType.Text, typeof(string)));
        var options = Options("target");

        var sql = ClickHouseSql.CreateTable(schema, meta: null, options, Resolver(options));

        await Assert.That(sql).Contains("`we``ird` String");
    }

    public static IEnumerable<(string Value, string Expected)> IdentifierCases()
    {
        yield return ("id", "`id`");
        yield return ("city name", "`city name`");
        yield return ("we`ird", "`we``ird`");
        yield return ("select", "`select`");
        yield return (string.Empty, "``");
    }

    private static ClickHouseWriteOptions Options(string table)
    {
        return new ClickHouseWriteOptions
        {
            TableName = new ClickHouseTableName
            {
                Table = table
            }
        };
    }

    private static ClickHouseColumnTypeResolver Resolver(ClickHouseWriteOptions options)
    {
        return new ClickHouseColumnTypeResolver(options);
    }

    private static DataSchema Schema(params (string Name, DataType DataType, Type ClrType)[] fields)
    {
        return new DataSchema
        {
            Fields = fields
                .Select(static (field, index) => new DataField
                {
                    Ordinal = index,
                    Name = field.Name,
                    DataType = field.DataType,
                    ClrType = field.ClrType,
                    Convert = null,
                    ReadValue = true
                })
                .ToArray()
        };
    }
}
