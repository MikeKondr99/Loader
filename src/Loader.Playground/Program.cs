using System.Diagnostics;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Script;
using Loader.Script.Execution;
using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using LangScript = Loader.Lang.Script;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPlaygroundOpenTelemetry(builder.Configuration, builder.Environment);
builder.Logging.AddPlaygroundOpenTelemetry(builder.Configuration, builder.Environment);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});
builder.Services.AddSingleton<PlaygroundLastRunStore>();
builder.Services.AddSingleton<PlaygroundProgressHub>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

Directory.CreateDirectory(PlaygroundFiles.RootPath);

app.MapGet("/api/config", (IConfiguration configuration, IWebHostEnvironment environment) =>
{
    var config = PlaygroundConfig.From(configuration, environment);
    return Results.Ok(new
    {
        Environment = environment.EnvironmentName,
        config.TargetConnectionString,
        config.FileRoot,
        PixBi = new
        {
            config.PixBi.Enabled,
            BaseUri = config.PixBi.BaseUri?.ToString()
        },
        Connections = config.Connections.Select(static connection => new
        {
            connection.Name,
            Type = connection.Provider.ToString()
        }).ToArray()
    });
});

app.MapGet("/api/files", () => Results.Ok(new
{
    RootPath = PlaygroundFiles.RootPath,
    Files = PlaygroundFiles.ListFiles()
}));

app.MapGet("/api/connections", async (IConfiguration configuration, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    var config = PlaygroundConfig.From(configuration, environment);
    var connections = new List<object>();
    connections.AddRange(config.Connections.Select(static connection => new
    {
        connection.Name,
        Type = connection.Provider.ToString()
    }));

    string? warning = null;
    try
    {
        var registry = config.CreateConnectionRegistry();
        var knownNames = new HashSet<string>(
            config.Connections.Select(static connection => connection.Name),
            StringComparer.OrdinalIgnoreCase);
        var names = await registry.FindNamesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var name in names)
        {
            if (knownNames.Contains(name))
            {
                continue;
            }

            var connection = await registry.GetAsync(name, cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                continue;
            }

            connections.Add(new
            {
                connection.Name,
                Type = connection.Provider.ToString()
            });
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        warning = ex.Message;
    }

    return Results.Ok(new
    {
        Connections = connections,
        Warning = warning
    });
});

app.MapPost("/api/files", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { Error = "Expected multipart/form-data." });
    }

    var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null || file.FileName.Trim().Length == 0)
    {
        return Results.BadRequest(new { Error = "File is required." });
    }

    var targetPath = PlaygroundFiles.ResolveSafeFilePath(file.FileName);
    await using var source = file.OpenReadStream();
    await using var target = File.Create(targetPath);
    await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    return Results.Ok(new
    {
        File = PlaygroundFiles.ToFileInfo(targetPath),
        Files = PlaygroundFiles.ListFiles()
    });
});

app.MapDelete("/api/files/{fileName}", (string fileName) =>
{
    var path = PlaygroundFiles.ResolveSafeFilePath(fileName);
    if (File.Exists(path))
    {
        File.Delete(path);
    }

    return Results.Ok(new
    {
        Files = PlaygroundFiles.ListFiles()
    });
});

app.MapPost("/api/parse", (ScriptRequest request) =>
{
    var result = LangScript.Parse(request.Script);
    if (!result.TryGetValue(out var script, out var error))
    {
        return Results.Ok(PlaygroundResponse.Fail("parse", ToPlaygroundError(error!)));
    }

    return Results.Ok(PlaygroundResponse.Success(new
    {
        StatementCount = script!.Statements.Count,
        Statements = script.Statements.Select(static (statement, index) => new
        {
            Index = index,
            Type = statement.GetType().Name
        }).ToArray()
    }));
});

app.MapGet("/api/last-run", (PlaygroundLastRunStore lastRunStore) =>
{
    var snapshot = lastRunStore.Get();
    return snapshot is null
        ? Results.NotFound(new { Error = "Успешный запуск еще не выполнялся." })
        : Results.Ok(PlaygroundResponse.Success(ToPlaygroundRunData(snapshot)));
});

app.MapGet("/api/progress/{runId}", async (
    string runId,
    PlaygroundProgressHub progressHub,
    HttpResponse response,
    CancellationToken cancellationToken) =>
{
    response.Headers.CacheControl = "no-cache";
    response.Headers.Connection = "keep-alive";
    response.ContentType = "text/event-stream";

    var reader = progressHub.Subscribe(runId);
    await foreach (var progressEvent in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
    {
        await response.WriteAsync("event: progress\n", cancellationToken).ConfigureAwait(false);
        await response.WriteAsync("data: ", cancellationToken).ConfigureAwait(false);
        await response.WriteAsync(
            JsonSerializer.Serialize(ToPlaygroundProgress(progressEvent), PlaygroundJson.Compact),
            cancellationToken).ConfigureAwait(false);
        await response.WriteAsync("\n\n", cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
});

app.MapPost("/api/run", async (
    ScriptRequest request,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    PlaygroundLastRunStore lastRunStore,
    PlaygroundProgressHub progressHub,
    CancellationToken cancellationToken) =>
{
    var parsed = LangScript.Parse(request.Script);
    if (!parsed.TryGetValue(out var script, out var parseError))
    {
        return Results.Ok(PlaygroundResponse.Fail("parse", ToPlaygroundError(parseError!)));
    }

    var config = PlaygroundConfig.From(configuration, environment);
    var context = new ScriptContext
    {
        FileStorage = new FileSystemSource(config.FileRoot),
        TargetConnectionString = config.TargetConnectionString,
        ConnectionRegistry = config.CreateConnectionRegistry(),
        Logger = string.IsNullOrWhiteSpace(request.RunId)
            ? NullProgressLogger.Instance
            : new PlaygroundProgressLogger(progressHub, request.RunId),
        Options = new ScriptContextOptions()
    };

    var stopwatch = Stopwatch.StartNew();
    try
    {
        var tables = await new ScriptExecutor()
            .ExecuteAsync(context, script!, cancellationToken)
            .ConfigureAwait(false);
        var loadedTables = tables.ToArray();

        stopwatch.Stop();
        var snapshot = new PlaygroundRunSnapshot(
            config.TargetConnectionString,
            stopwatch.ElapsedMilliseconds,
            loadedTables);
        lastRunStore.Set(snapshot);
        return Results.Ok(PlaygroundResponse.Success(ToPlaygroundRunData(snapshot)));
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        stopwatch.Stop();
        return Results.Ok(PlaygroundResponse.Fail(
            "execute",
            ToPlaygroundException(exception),
            new
            {
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                LoadedTables = context.LoadedTables.Select(static table => table.Name.ToSql()).ToArray()
            }));
    }
    finally
    {
        if (!string.IsNullOrWhiteSpace(request.RunId))
        {
            progressHub.Complete(request.RunId);
        }
    }
});

app.MapGet("/api/last-run/tables/{index:int}/preview", async (
    int index,
    PlaygroundLastRunStore lastRunStore,
    CancellationToken cancellationToken) =>
{
    var snapshot = lastRunStore.Get();
    if (snapshot is null)
    {
        return Results.NotFound(new { Error = "Успешный запуск еще не выполнялся." });
    }

    if (index < 0 || index >= snapshot.Tables.Count)
    {
        return Results.BadRequest(new { Error = $"Таблица с индексом {index} не найдена в последнем результате." });
    }

    var table = snapshot.Tables[index];
    var sql = BuildPreviewSql(table, limit: 15);
    try
    {
        var source = new ConnectionStringSource
        {
            ConnectionString = snapshot.TargetConnectionString
        };
        await using var rawReader = await new ClickHouseProvider()
            .OpenReaderAsync(source, new SqlTableConfig { Sql = sql }, cancellationToken)
            .ConfigureAwait(false);
        await using var reader = rawReader.Normalize();
        var rows = await ReadPreviewRowsAsync(reader, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new
        {
            Table = table.Name.ToSql(),
            table.Alias,
            Columns = ResolvePreviewColumns(table, rows.Columns),
            Fields = table.Fields.Select(static field => new PlaygroundFieldData(
                field.Name,
                FormatQueryType(field.DataType, field.CanBeNull))).ToArray(),
            Rows = rows.Rows
        });
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        return Results.Ok(PlaygroundResponse.Fail("preview", ToPlaygroundException(exception), new { Sql = sql }));
    }
});

app.Run();

static string BuildPreviewSql(LoadedTable table, int limit)
{
    var builder = new StringBuilder();
    builder
        .Append("SELECT *")
        .Append(" FROM ")
        .Append(table.Name.ToSql())
        .Append(" LIMIT ")
        .Append(limit);
    return builder.ToString();
}

static string[] ResolvePreviewColumns(LoadedTable table, string[] physicalColumns)
{
    return table.Fields.Count == physicalColumns.Length
        ? table.Fields.Select(static field => field.Name).ToArray()
        : physicalColumns;
}

static async Task<PlaygroundPreviewRows> ReadPreviewRowsAsync(
    DbDataReader reader,
    CancellationToken cancellationToken)
{
    var columns = Enumerable.Range(0, reader.FieldCount)
        .Select(reader.GetName)
        .ToArray();
    var rows = new List<object?[]>();
    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        var row = new object?[reader.FieldCount];
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            row[ordinal] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
        }

        rows.Add(row);
    }

    return new PlaygroundPreviewRows(columns, rows);
}

static PlaygroundRunData ToPlaygroundRunData(PlaygroundRunSnapshot snapshot)
{
    return new PlaygroundRunData(
        snapshot.ElapsedMs,
        snapshot.Tables.Select(static (table, index) => new PlaygroundTableData(
            index,
            table.Name.ToSql(),
            table.Alias,
            table.RowCount)).ToArray());
}

static string FormatQueryType(Loader.Core.Models.DataType dataType, bool canBeNull)
{
    var name = dataType switch
    {
        Loader.Core.Models.DataType.Text => "text",
        Loader.Core.Models.DataType.Integer => "int",
        Loader.Core.Models.DataType.Number => "num",
        Loader.Core.Models.DataType.DateTime => "datetime",
        Loader.Core.Models.DataType.Date => "date",
        Loader.Core.Models.DataType.Time => "time",
        Loader.Core.Models.DataType.Boolean => "bool",
        _ => dataType.ToString().ToLowerInvariant()
    };

    return canBeNull ? name : $"{name}!";
}

static PlaygroundError ToPlaygroundError(LangError error)
{
    return new PlaygroundError
    {
        Type = nameof(LangError),
        Message = error.Message,
        Span = ToPlaygroundSpan(error.Span)
    };
}

static PlaygroundProgressData ToPlaygroundProgress(ScriptProgressEvent progressEvent)
{
    return new PlaygroundProgressData(
        progressEvent.Kind,
        progressEvent.Level.ToString(),
        progressEvent.Message,
        progressEvent.OccurredAt);
}

static PlaygroundError ToPlaygroundException(Exception exception)
{
    var scriptException = exception as LoadScriptException;
    var stageException = exception as LoadScriptStageException;

    return new PlaygroundError
    {
        Type = exception.GetType().FullName ?? exception.GetType().Name,
        Message = exception.Message,
        ScriptStage = scriptException?.Stage.ToString() ?? stageException?.Stage.ToString(),
        StatementIndex = scriptException?.StatementIndex,
        StatementType = scriptException?.StatementType,
        Errors = ToPlaygroundDiagnostics(scriptException?.Errors ?? stageException?.Errors),
        Span = scriptException?.Span is { } scriptSpan
            ? ToPlaygroundSpan(scriptSpan)
            : stageException?.Span is { } stageSpan
                ? ToPlaygroundSpan(stageSpan)
                : null,
        StackTrace = exception.ToString(),
        Inner = exception.InnerException is null ? null : ToPlaygroundException(exception.InnerException)
    };
}

static IReadOnlyList<PlaygroundDiagnostic>? ToPlaygroundDiagnostics(IReadOnlyList<LangError>? errors)
{
    return errors is null || errors.Count == 0
        ? null
        : errors.Select(static error => new PlaygroundDiagnostic
        {
            Message = error.Message,
            Span = ToPlaygroundSpan(error.Span)
        }).ToArray();
}

static PlaygroundSpan ToPlaygroundSpan(LangSpan span)
{
    return new PlaygroundSpan
    {
        StartRow = span.StartRow,
        StartColumn = span.StartColumn,
        EndRow = span.EndRow,
        EndColumn = span.EndColumn
    };
}

internal sealed record ScriptRequest(string Script, string? RunId = null);

internal sealed record PlaygroundProgressData(
    string Kind,
    string Level,
    string Message,
    DateTimeOffset OccurredAt);

internal static class PlaygroundJson
{
    public static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
}

internal sealed record PlaygroundRunSnapshot(
    string TargetConnectionString,
    long ElapsedMs,
    IReadOnlyList<LoadedTable> Tables);

internal sealed record PlaygroundRunData(
    long ElapsedMs,
    IReadOnlyList<PlaygroundTableData> Tables);

internal sealed record PlaygroundTableData(
    int Index,
    string Name,
    string? Alias,
    long? RowCount);

internal sealed record PlaygroundFieldData(
    string Name,
    string Type);

internal sealed class PlaygroundLastRunStore
{
    private readonly object sync = new();
    private PlaygroundRunSnapshot? snapshot;

    public void Set(PlaygroundRunSnapshot value)
    {
        lock (sync)
        {
            snapshot = value;
        }
    }

    public PlaygroundRunSnapshot? Get()
    {
        lock (sync)
        {
            return snapshot;
        }
    }
}

internal sealed record PlaygroundPreviewRows(string[] Columns, IReadOnlyList<object?[]> Rows);

internal sealed record PlaygroundConfig(
    string TargetConnectionString,
    string FileRoot,
    IReadOnlyList<ScriptConnection> Connections,
    PlaygroundPixBiConfig PixBi)
{
    public static PlaygroundConfig From(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var targetConnectionString =
            configuration["TargetConnectionString"] ??
            Environment.GetEnvironmentVariable("LOADER_PLAYGROUND_CLICKHOUSE") ??
            "Host=localhost;Port=8123;Protocol=http;Database=loader_playground;Username=loader;Password=loader";

        return new PlaygroundConfig(
            targetConnectionString,
            PlaygroundFiles.RootPath,
            ReadConnections(configuration),
            ReadPixBi(configuration));
    }

    public IConnectionRegistry CreateConnectionRegistry()
    {
        var registries = new List<IConnectionRegistry>
        {
            new InMemoryConnectionRegistry(Connections)
        };

        if (PixBi.Enabled)
        {
            registries.Add(new PixBiConnectionRegistry(
                new HttpClient(),
                new PixBiConnectionRegistryOptions
                {
                    BaseUri = PixBi.BaseUri ?? throw new InvalidOperationException("PixBi:BaseUri is required."),
                    Username = PixBi.Username ?? throw new InvalidOperationException("PixBi:Username is required."),
                    Password = PixBi.Password ?? throw new InvalidOperationException("PixBi:Password is required."),
                    PageSize = PixBi.PageSize
                }));
        }

        return new AggregateConnectionRegistry(registries);
    }

    private static IReadOnlyList<ScriptConnection> ReadConnections(IConfiguration configuration)
    {
        return configuration
            .GetSection("Connections")
            .GetChildren()
            .Select(ReadConnection)
            .ToArray();
    }

    private static ScriptConnection ReadConnection(IConfigurationSection section)
    {
        var name = section["Name"];
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Playground connection requires non-empty Name.");
        }

        var typeText = section["Type"];
        if (!Enum.TryParse<ScriptConnectionType>(typeText, ignoreCase: true, out var type))
        {
            throw new InvalidOperationException($"Playground connection '{name}' has unknown Type '{typeText}'.");
        }

        var connectionString = section["ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Playground connection '{name}' requires non-empty ConnectionString.");
        }

        return new ScriptConnection
        {
            Name = name,
            Provider = type,
            ConnectionString = connectionString
        };
    }

    private static PlaygroundPixBiConfig ReadPixBi(IConfiguration configuration)
    {
        var section = configuration.GetSection("PixBi");
        var enabled = section.GetValue("Enabled", false);
        if (!enabled)
        {
            return new PlaygroundPixBiConfig(
                false,
                null,
                null,
                null,
                section.GetValue("PageSize", 50));
        }

        Uri? baseUri = null;
        var baseUriText = section["BaseUri"];
        if (!string.IsNullOrWhiteSpace(baseUriText))
        {
            if (!Uri.TryCreate(baseUriText, UriKind.Absolute, out baseUri))
            {
                throw new InvalidOperationException("PixBi:BaseUri must be an absolute URI when PixBi is enabled.");
            }
        }

        return new PlaygroundPixBiConfig(
            enabled,
            baseUri,
            section["Username"],
            section["Password"],
            section.GetValue("PageSize", 50));
    }
}

internal sealed record PlaygroundPixBiConfig(
    bool Enabled,
    Uri? BaseUri,
    string? Username,
    string? Password,
    int PageSize);

internal static class PlaygroundFiles
{
    public static string RootPath { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "loaded_files"));

    public static IReadOnlyList<PlaygroundFile> ListFiles()
    {
        Directory.CreateDirectory(RootPath);
        return Directory
            .EnumerateFiles(RootPath)
            .Select(ToFileInfo)
            .OrderBy(static file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ResolveSafeFilePath(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (safeName.Length == 0 ||
            safeName != fileName ||
            safeName is "." or ".." ||
            safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException($"File name '{fileName}' is not allowed.");
        }

        var path = Path.GetFullPath(Path.Combine(RootPath, safeName));
        if (!path.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"File name '{fileName}' must stay inside loaded_files.");
        }

        return path;
    }

    public static PlaygroundFile ToFileInfo(string path)
    {
        var file = new FileInfo(path);
        return new PlaygroundFile(
            file.Name,
            file.Length,
            file.LastWriteTimeUtc);
    }
}

internal sealed record PlaygroundFile(string Name, long Size, DateTime ModifiedUtc);

internal sealed record PlaygroundResponse
{
    public required bool Ok { get; init; }

    public string? Stage { get; init; }

    public object? Data { get; init; }

    public PlaygroundError? Error { get; init; }

    public object? Context { get; init; }

    public static PlaygroundResponse Success(object data)
    {
        return new PlaygroundResponse
        {
            Ok = true,
            Data = data
        };
    }

    public static PlaygroundResponse Fail(string stage, PlaygroundError error, object? context = null)
    {
        return new PlaygroundResponse
        {
            Ok = false,
            Stage = stage,
            Error = error,
            Context = context
        };
    }
}

internal sealed record PlaygroundError
{
    public required string Type { get; init; }

    public required string Message { get; init; }

    public string? ScriptStage { get; init; }

    public int? StatementIndex { get; init; }

    public string? StatementType { get; init; }

    public IReadOnlyList<PlaygroundDiagnostic>? Errors { get; init; }

    public PlaygroundSpan? Span { get; init; }

    public string? StackTrace { get; init; }

    public PlaygroundError? Inner { get; init; }
}

internal sealed record PlaygroundDiagnostic
{
    public required string Message { get; init; }

    public required PlaygroundSpan Span { get; init; }
}

internal sealed record PlaygroundSpan
{
    public required uint StartRow { get; init; }

    public required uint StartColumn { get; init; }

    public required uint EndRow { get; init; }

    public required uint EndColumn { get; init; }
}

internal static class PlaygroundOpenTelemetry
{
    public static IServiceCollection AddPlaygroundOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var section = configuration.GetSection("OpenTelemetry");
        if (!section.GetValue("Enabled", false))
        {
            return services;
        }

        var serviceName = section["ServiceName"] ?? environment.ApplicationName;
        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options => ConfigureOtlp(options, section));
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(
                        "LoadScript",
                        "Loader.Lang",
                        "Loader.Core.Odbc");

                tracing.AddOtlpExporter(options => ConfigureOtlp(options, section));
            });

        return services;
    }

    public static ILoggingBuilder AddPlaygroundOpenTelemetry(
        this ILoggingBuilder logging,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var section = configuration.GetSection("OpenTelemetry");
        if (!section.GetValue("Enabled", false))
        {
            return logging;
        }

        var serviceName = section["ServiceName"] ?? environment.ApplicationName;
        logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName));
            options.AddOtlpExporter(exporter => ConfigureOtlp(exporter, section));
        });

        return logging;
    }

    private static void ConfigureOtlp(OtlpExporterOptions options, IConfiguration section)
    {
        var otlp = section.GetSection("Otlp");
        if (Uri.TryCreate(otlp["Endpoint"], UriKind.Absolute, out var endpoint))
        {
            options.Endpoint = endpoint;
        }

        if (Enum.TryParse<OtlpExportProtocol>(otlp["Protocol"], ignoreCase: true, out var protocol))
        {
            options.Protocol = protocol;
        }
    }
}
