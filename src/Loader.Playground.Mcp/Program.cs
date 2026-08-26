using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args
});
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new PlaygroundClient(
    new HttpClient
    {
        BaseAddress = ResolvePlaygroundUri()
    }));

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInstructions = """
        Before using Loader.Playground tools to create or fix a Loader script, call GetKnowledge and GetContext first. Use GetFile before guessing file delimiters or JSON/XML shape. Iterate with TestRun until the script parses, executes, and preview data confirms reasonable typing and cleaning. TestRun is ephemeral and cleans created tables; normal playground state is not updated.
        """;
    })
    .WithStdioServerTransport()
    .WithTools<PlaygroundTools>();

await builder.Build().RunAsync().ConfigureAwait(false);

static Uri ResolvePlaygroundUri()
{
    var value = Environment.GetEnvironmentVariable("LOADER_PLAYGROUND_URL");
    if (string.IsNullOrWhiteSpace(value))
    {
        value = "http://localhost:5000";
    }

    return Uri.TryCreate(value, UriKind.Absolute, out var uri)
        ? uri
        : throw new InvalidOperationException("LOADER_PLAYGROUND_URL must be an absolute URI.");
}

[McpServerToolType]
public sealed class PlaygroundTools(PlaygroundClient client)
{
    [McpServerTool]
    [Description("Call this before script work. Return Loader.Playground files, connections, and last-run snapshot. Connection strings are not exposed.")]
    public Task<string> GetContext(CancellationToken cancellationToken = default)
    {
        return client.GetContextAsync(cancellationToken);
    }

    [McpServerTool]
    [Description("Call this first for Loader script work. Return syntax, cleaning, casts, source probing, and iteration guidance.")]
    public static string GetKnowledge()
    {
        return """
        Loader.Playground agent workflow:
        1. Call GetContext to inspect available files, connection names/types, and last-run diagnostics.
        2. Call GetFile before guessing text file delimiters, headers, JSON roots, XML row tags, or encodings.
        3. Start with a small exploratory LOAD * and LIMIT, then replace it with explicit fields, aliases, casts, trimming, null handling, and filters.
        4. Use TestRun after every meaningful change. Treat success only as "schema + preview rows look correct", not merely "no exception".
        5. Final answer must contain the final Loader script in one fenced code block and briefly state what TestRun verified.

        Script shape:
        table_alias:
        LOAD
            expression AS output_field,
            other_field
        FROM SourceName(option='value', flag=true)
        SQL SELECT ... -- only for Connect(...)
        WHERE condition
        GROUP BY expression
        ORDER BY expression ASC
        LIMIT 100;

        Multiple statements are allowed. Later statements can reference earlier loaded aliases through providers such as Join/Union when supported by the project. Use unique output aliases.

        Source examples:
        csv_orders:
        LOAD *
        FROM Csv(path='orders.csv', delimiter=',', header=true)
        LIMIT 20;

        xlsx_sheet:
        LOAD *
        FROM Excel(path='book.xlsx', sheet='Sheet1')
        LIMIT 20;

        json_items:
        LOAD *
        FROM Json(path='data.json', root='items')
        LIMIT 20;

        xml_rows:
        LOAD *
        FROM Xml(path='data.xml', table='row')
        LIMIT 20;

        qvd_table:
        LOAD *
        FROM Qvd(path='data.qvd')
        LIMIT 20;

        pg_probe:
        LOAD *
        FROM Connect(name='pg')
        SQL SELECT table_schema, table_name
        FROM information_schema.tables
        WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY table_schema, table_name
        LIMIT 50;

        Postgres column discovery:
        columns:
        LOAD *
        FROM Connect(name='pg')
        SQL SELECT table_schema, table_name, column_name, data_type, is_nullable
        FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'iris'
        ORDER BY ordinal_position;

        ClickHouse discovery:
        ch_columns:
        LOAD *
        FROM Connect(name='clickhouse')
        SQL SELECT database, table, name, type
        FROM system.columns
        WHERE database = currentDatabase()
        ORDER BY table, position
        LIMIT 100;

        Data quality defaults:
        - Prefer explicit fields over final LOAD * after exploration.
        - Trim text fields that come from files or user-entered DB columns.
        - Convert empty strings to null before numeric/date casts when the source is text.
        - Cast obvious numbers, booleans, dates, and timestamps; do not leave all CSV columns as text.
        - Filter invalid records only when the user intent implies clean analytical data or previews show junk rows.
        - Preserve original values when uncertain; add cleaned fields with clear aliases instead of silently losing data.

        Common functions:
        - Trim(text), Lower(text), Upper(text)
        - Replace(text, old, new)
        - EmptyIsNull(text)
        - Num(value), Int(value), Text(value), Bool(value), Date(value), Time(value)
        - Len(text), Contains(text, part), StartsWith(text, part), EndsWith(text, part)
        - If(condition, true_value, false_value), Alt(a, b, c), IsNull(value), IsNotNull(value)
        - Round(number, digits), Abs(number)

        Cleaning/casting examples:
        iris:
        LOAD
            Num(EmptyIsNull(Trim(sepal_length))) AS sepal_length,
            Num(EmptyIsNull(Trim(sepal_width))) AS sepal_width,
            Num(EmptyIsNull(Trim(petal_length))) AS petal_length,
            Num(EmptyIsNull(Trim(petal_width))) AS petal_width,
            Lower(Trim(species)) AS species
        FROM Csv(path='iris.csv', delimiter=',', header=true)
        WHERE IsNotNull(EmptyIsNull(Trim(species)));

        orders:
        LOAD
            Int(EmptyIsNull(Trim(order_id))) AS order_id,
            Date(EmptyIsNull(Trim(order_date))) AS order_date,
            Num(Replace(EmptyIsNull(Trim(amount)), ',', '.')) AS amount,
            Upper(Trim(country)) AS country
        FROM Csv(path='orders.csv', delimiter=';', header=true)
        WHERE IsNotNull(EmptyIsNull(Trim(order_id)));

        Frequent errors and fixes:
        - Duplicate source field names: select explicit fields and aliases.
        - Duplicate output aliases: make every `AS name` unique.
        - Unknown field: run LOAD * LIMIT 20 and use exact source column names from preview.
        - Wrong CSV delimiter/header: call GetFile and inspect the first lines.
        - JSON array path not found: call GetFile and choose the actual array root.
        - DB table not found: probe information_schema/system tables through TestRun.
        - Cast failures: first preview raw strings, then add EmptyIsNull/Trim/Replace before Num/Int/Date.

        Safety:
        - Use only connection names from GetContext.
        - Do not ask for or expose connection strings.
        - Use TestRun for exploration; it is intended to clean up created tables.
        - Keep exploratory LIMIT values small.
        """;
    }

    [McpServerTool]
    [Description("Read the first lines of a playground file. Use this to infer CSV delimiters or inspect JSON/XML text.")]
    public Task<string> GetFile(
        [Description("File name from get_context files, without path traversal.")]
        string fileName,
        [Description("Maximum number of lines to return.")]
        int maxLines = 50,
        [Description("Maximum bytes to read before line trimming.")]
        int maxBytes = 16384,
        CancellationToken cancellationToken = default)
    {
        return client.GetFileAsync(fileName, maxLines, maxBytes, cancellationToken);
    }

    [McpServerTool]
    [Description("Run a Loader script in playground test mode, return fields and preview rows, then clean created tables.")]
    public Task<string> TestRun(
        [Description("Full Loader script to execute.")]
        string script,
        [Description("Preview row count per output table.")]
        int previewRows = 20,
        CancellationToken cancellationToken = default)
    {
        return client.TestRunAsync(script, previewRows, cancellationToken);
    }
}

public sealed class PlaygroundClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<string> GetContextAsync(CancellationToken cancellationToken)
    {
        var files = await GetStringOrErrorAsync("/api/files", cancellationToken).ConfigureAwait(false);
        var connections = await GetStringOrErrorAsync("/api/connections", cancellationToken).ConfigureAwait(false);
        var lastRun = await GetStringOrErrorAsync("/api/last-run", cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            PlaygroundUrl = http.BaseAddress?.ToString(),
            Files = JsonDocument.Parse(files).RootElement,
            Connections = JsonDocument.Parse(connections).RootElement,
            LastRun = JsonDocument.Parse(lastRun).RootElement
        }, JsonOptions);
    }

    public Task<string> GetFileAsync(
        string fileName,
        int maxLines,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var path = new StringBuilder()
            .Append("/api/files/")
            .Append(Uri.EscapeDataString(fileName))
            .Append("/content?maxLines=")
            .Append(maxLines)
            .Append("&maxBytes=")
            .Append(maxBytes)
            .ToString();
        return GetStringOrErrorAsync(path, cancellationToken);
    }

    public async Task<string> TestRunAsync(
        string script,
        int previewRows,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
                "/api/test-run",
                new
                {
                    Script = script,
                    PreviewRows = previewRows
                },
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetStringOrErrorAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return body;
        }

        return JsonSerializer.Serialize(new
        {
            Ok = false,
            StatusCode = (int)response.StatusCode,
            response.ReasonPhrase,
            Body = body
        }, JsonOptions);
    }
}
