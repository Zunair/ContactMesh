namespace ContactMesh.Google.Auth;

public sealed record GoogleWorkspaceOptions
{
    public const string SectionName = "GoogleWorkspace";

    public string ServiceAccountFile { get; init; } = "client_secret_svc.json";
    public string? AdminUserEmail { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
}
