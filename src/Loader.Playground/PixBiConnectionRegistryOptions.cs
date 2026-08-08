internal sealed record PixBiConnectionRegistryOptions
{
    public required Uri BaseUri { get; init; }

    public required string Username { get; init; }

    public required string Password { get; init; }

    public int PageSize { get; init; } = 50;
}
