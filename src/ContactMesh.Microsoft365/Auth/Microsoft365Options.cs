namespace ContactMesh.Microsoft365.Auth;

public sealed record Microsoft365Options
{
    public const string SectionName = "Microsoft365";
    public const string DefaultGraphScope = "https://graph.microsoft.com/.default";

    public static readonly IReadOnlyList<string> DefaultGraphScopes = new[]
    {
        DefaultGraphScope
    };

    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
}
