using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loader.Script;

internal sealed class PixBiConnectionRegistry : IConnectionRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<int, ScriptConnectionType> SupportedTypes =
        new Dictionary<int, ScriptConnectionType>
        {
            [1] = ScriptConnectionType.Postgres,
            [2] = ScriptConnectionType.ClickHouse,
            [3] = ScriptConnectionType.SqlServer,
            [11] = ScriptConnectionType.Oracle,
            [13] = ScriptConnectionType.Hive
        };

    private readonly HttpClient httpClient;
    private readonly PixBiConnectionRegistryOptions options;
    private string? token;

    public PixBiConnectionRegistry(HttpClient httpClient, PixBiConnectionRegistryOptions options)
    {
        this.httpClient = httpClient;
        this.options = options;
    }

    public async ValueTask<ScriptConnection?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var datasource = await FindDatasourceAsync(name, cancellationToken).ConfigureAwait(false);
        if (datasource is null)
        {
            return null;
        }

        var detail = await GetDatasourceAsync(datasource.Id, cancellationToken).ConfigureAwait(false);
        if (!SupportedTypes.TryGetValue(detail.DataSourceType, out var type))
        {
            return null;
        }

        return new ScriptConnection
        {
            Name = detail.Name,
            Provider = type,
            ConnectionString = BuildConnectionString(type, detail.Params)
        };
    }

    public async ValueTask<IReadOnlyList<string>> FindNamesAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetDatasourcesAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        return response.GetItems()
            .Where(static datasource => datasource.TypeId is { } typeId && SupportedTypes.ContainsKey(typeId))
            .Select(static datasource => datasource.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<PixBiDatasourceListItem?> FindDatasourceAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var response = await GetDatasourcesAsync(name, cancellationToken).ConfigureAwait(false);
        return response.GetItems().FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) &&
            item.TypeId is { } typeId &&
            SupportedTypes.ContainsKey(typeId));
    }

    private async Task<PixBiDatasourceListResponse> GetDatasourcesAsync(
        string searchText,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(cancellationToken).ConfigureAwait(false);
        using var response = await httpClient.PostAsJsonAsync(
                ResolveUri("/api/v0/datasources"),
                new PixBiDatasourceSearchRequest
                {
                    PageNumber = 1,
                    PageSize = options.PageSize,
                    SearchText = searchText,
                    Filters = new PixBiDatasourceFilters
                    {
                        DataSourceType = new PixBiContainsInFilter
                        {
                            ContainsIn = SupportedTypes.Keys.ToArray()
                        }
                    },
                    Sort = []
                },
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PixBiDatasourceListResponse>(
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return payload ?? new PixBiDatasourceListResponse();
    }

    private async Task<PixBiDatasourceDetail> GetDatasourceAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(cancellationToken).ConfigureAwait(false);
        var detail = await httpClient.GetFromJsonAsync<PixBiDatasourceDetail>(
                ResolveUri($"/api/v0/datasource/{id.ToString(CultureInfo.InvariantCulture)}"),
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return detail ?? throw new InvalidOperationException($"PIX BI datasource '{id}' returned empty response.");
    }

    private async Task EnsureAuthorizedAsync(CancellationToken cancellationToken)
    {
        if (token is not null)
        {
            return;
        }

        using var response = await httpClient.PostAsJsonAsync(
                ResolveUri("/api/v0/token"),
                new PixBiTokenRequest
                {
                    Username = options.Username,
                    Password = options.Password
                },
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        token = await ReadTokenAsync(response, cancellationToken).ConfigureAwait(false);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var trimmed = text.Trim().Trim('"');
        if (!trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        using var document = JsonDocument.Parse(trimmed);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String &&
                property.Name.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.GetString()!;
            }
        }

        throw new InvalidOperationException("PIX BI token response does not contain token.");
    }

    private Uri ResolveUri(string path)
    {
        return new Uri(options.BaseUri, path);
    }

    private static string BuildConnectionString(
        ScriptConnectionType type,
        IReadOnlyDictionary<string, string?> parameters)
    {
        return type switch
        {
            ScriptConnectionType.Postgres => BuildPostgresConnectionString(parameters),
            ScriptConnectionType.ClickHouse => BuildClickHouseConnectionString(parameters),
            ScriptConnectionType.SqlServer => BuildSqlServerConnectionString(parameters),
            ScriptConnectionType.Oracle => BuildOracleConnectionString(parameters),
            ScriptConnectionType.Hive => BuildHiveConnectionString(parameters),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static string BuildPostgresConnectionString(IReadOnlyDictionary<string, string?> parameters)
    {
        return string.Join(
            ';',
            RequiredPart("Host", "Host", parameters),
            OptionalPart("Port", "Port", parameters),
            RequiredPart("Database", "Database", parameters),
            RequiredPart("Username", "UserId", parameters),
            OptionalPart("Password", "Password", parameters),
            OptionalRaw(parameters));
    }

    private static string BuildClickHouseConnectionString(IReadOnlyDictionary<string, string?> parameters)
    {
        return string.Join(
            ';',
            RequiredPart("Host", "Host", parameters),
            OptionalPart("Port", "Port", parameters),
            RequiredPart("Database", "Database", parameters),
            RequiredPart("Username", "UserId", parameters),
            OptionalPart("Password", "Password", parameters),
            "Protocol=http",
            OptionalRaw(parameters));
    }

    private static string BuildSqlServerConnectionString(IReadOnlyDictionary<string, string?> parameters)
    {
        var host = Required("Host", parameters);
        var port = Optional("Port", parameters);
        var server = string.IsNullOrWhiteSpace(port) ? host : $"{host},{port}";
        return string.Join(
            ';',
            $"Server={server}",
            RequiredPart("Database", "Database", parameters),
            RequiredPart("User Id", "UserId", parameters),
            OptionalPart("Password", "Password", parameters),
            "TrustServerCertificate=True",
            "Encrypt=False",
            OptionalRaw(parameters));
    }

    private static string BuildOracleConnectionString(IReadOnlyDictionary<string, string?> parameters)
    {
        var host = Required("Host", parameters);
        var port = Optional("Port", parameters) ?? "1521";
        var database = Required("Database", parameters);
        return string.Join(
            ';',
            $"Data Source={host}:{port}/{database}",
            RequiredPart("User Id", "UserId", parameters),
            OptionalPart("Password", "Password", parameters),
            OptionalRaw(parameters));
    }

    private static string BuildHiveConnectionString(IReadOnlyDictionary<string, string?> parameters)
    {
        return string.Join(
            ';',
            "Driver={Hive}",
            RequiredPart("Host", "Host", parameters),
            OptionalPart("Port", "Port", parameters),
            OptionalPart("Schema", "Database", parameters),
            OptionalPart("UID", "UserId", parameters),
            OptionalPart("PWD", "Password", parameters),
            OptionalRaw(parameters));
    }

    private static string RequiredPart(
        string connectionStringName,
        string parameterName,
        IReadOnlyDictionary<string, string?> parameters)
    {
        return $"{connectionStringName}={Required(parameterName, parameters)}";
    }

    private static string? OptionalPart(
        string connectionStringName,
        string parameterName,
        IReadOnlyDictionary<string, string?> parameters)
    {
        var value = Optional(parameterName, parameters);
        return string.IsNullOrWhiteSpace(value) ? null : $"{connectionStringName}={value}";
    }

    private static string Required(string name, IReadOnlyDictionary<string, string?> parameters)
    {
        var value = Optional(name, parameters);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"PIX BI datasource parameter '{name}' is required.")
            : value;
    }

    private static string? Optional(string name, IReadOnlyDictionary<string, string?> parameters)
    {
        return parameters.TryGetValue(name, out var value) ? value : null;
    }

    private static string? OptionalRaw(IReadOnlyDictionary<string, string?> parameters)
    {
        return Optional("OtherParameters", parameters);
    }

    private sealed record PixBiTokenRequest
    {
        public required string Username { get; init; }

        public required string Password { get; init; }
    }

    private sealed record PixBiDatasourceSearchRequest
    {
        public required int PageNumber { get; init; }

        public required int PageSize { get; init; }

        public required string SearchText { get; init; }

        public required PixBiDatasourceFilters Filters { get; init; }

        public required object[] Sort { get; init; }
    }

    private sealed record PixBiDatasourceFilters
    {
        public required PixBiContainsInFilter DataSourceType { get; init; }
    }

    private sealed record PixBiContainsInFilter
    {
        public required int[] ContainsIn { get; init; }
    }

    private sealed record PixBiDatasourceListResponse
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<PixBiDatasourceListItem> Items { get; init; } = [];

        [JsonPropertyName("data")]
        public IReadOnlyList<PixBiDatasourceListItem>? Data { get; init; }

        public IReadOnlyList<PixBiDatasourceListItem> GetItems()
        {
            return Items.Count > 0 ? Items : Data ?? [];
        }
    }

    private sealed record PixBiDatasourceListItem
    {
        public required int Id { get; init; }

        public required string Name { get; init; }

        public int? DataSourceTypeId { get; init; }

        public JsonElement? DataSourceType { get; init; }

        public int? TypeId =>
            DataSourceTypeId ??
            (DataSourceType is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var typeId)
                ? typeId
                : null);
    }

    private sealed record PixBiDatasourceDetail
    {
        public required int Id { get; init; }

        public required string Name { get; init; }

        public required int DataSourceType { get; init; }

        public IReadOnlyDictionary<string, string?> Params { get; init; } =
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }
}
