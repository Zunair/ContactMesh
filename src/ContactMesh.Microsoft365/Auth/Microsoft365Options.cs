namespace ContactMesh.Microsoft365.Auth;

public sealed record Microsoft365Options
{
    public const string SectionName = "Microsoft365";

    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
}
