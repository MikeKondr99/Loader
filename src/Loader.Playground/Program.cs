using System.Data;
using Loader.Core.Data;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Csv;
using Loader.Core.Providers.Excel;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Sylvan.Data.Excel;

var fileSource = new FileSystemSource("./files");
var csvProvider = new CsvProvider();

await using var ordersReader = await csvProvider.OpenReaderAsync(
    fileSource,
    new CsvTableConfig
    {
        FileName = "orders.csv",
        Delimiter = ';',
        HasHeader = true
    });

await using var orders = ordersReader.AsTyped();
PrintReader("orders", orders);

EnsureProductsWorkbook(fileSource.ResolveFilePath("products.xlsx"));

var excelProvider = new ExcelProvider();
await using var productsReader = await excelProvider.OpenReaderAsync(
    fileSource,
    new ExcelTableConfig
    {
        FileName = "products.xlsx",
        WorksheetName = "Products",
        HasHeader = true,
        FormulaErrorHandling = ExcelFormulaErrorMode.Null
    });

await using var products = productsReader.AsTyped();
PrintReader("products", products);

// Pipeline API shape:
// var filtered = products.Where(row => row.Text("name").ToLowerInvariant() == "moscow");

var postgresConnectionString = Environment.GetEnvironmentVariable("LOADER_PG_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    var postgresProvider = new PostgresProvider();
    var postgresSource = new ConnectionStringSource
    {
        ConnectionString = postgresConnectionString
    };

    await using var postgresReader = await postgresProvider.OpenReaderAsync(
        postgresSource,
        new SqlTableConfig
        {
            Sql = "select 1::int as id, 'postgres'::text as name"
        });

    await using var postgresRows = postgresReader.AsTyped();
    PrintReader("postgres_sample", postgresRows);
}

var clickHouseConnectionString = Environment.GetEnvironmentVariable("LOADER_CH_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(clickHouseConnectionString))
{
    var clickHouseProvider = new ClickHouseProvider();
    var clickHouseSource = new ConnectionStringSource
    {
        ConnectionString = clickHouseConnectionString
    };

    await using var clickHouseReader = await clickHouseProvider.OpenReaderAsync(
        clickHouseSource,
        new SqlTableConfig
        {
            Sql = "select 1 as id, 'clickhouse' as name"
        });

    await using var clickHouseRows = clickHouseReader.AsTyped();
    PrintReader("clickhouse_sample", clickHouseRows);
}

static void PrintReader(string tableName, DomainDataReader reader)
{
    Console.WriteLine($"table: {tableName}");

    foreach (var field in reader.DataSchema.Fields)
    {
        Console.WriteLine($"  field={field.Name}, type={field.DataType}");
    }

    while (reader.Read())
    {
        var values = new object[reader.FieldCount];
        reader.GetValues(values);
        Console.WriteLine("  row: " + string.Join(", ", values));
    }
}

static void EnsureProductsWorkbook(string filePath)
{
    if (File.Exists(filePath))
    {
        return;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

    using var table = new DataTable();
    table.Columns.Add("id", typeof(int));
    table.Columns.Add("name", typeof(string));
    table.Columns.Add("price", typeof(decimal));
    table.Columns.Add("active", typeof(bool));

    table.Rows.Add(1, "Keyboard", 79.90m, true);
    table.Rows.Add(2, "Mouse", 34.50m, true);
    table.Rows.Add(3, "Monitor", 299.00m, false);

    using var writer = ExcelDataWriter.Create(filePath);
    using var reader = table.CreateDataReader();
    writer.Write(reader, "Products");
}
