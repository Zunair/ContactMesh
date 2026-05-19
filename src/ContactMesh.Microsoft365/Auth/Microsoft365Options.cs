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
    public Microsoft365ContactDiagnosticOptions ContactDiagnostic { get; init; } = new();

    /// <summary>
    /// Group types to load from Microsoft Graph. Accepted values: Microsoft365, MailEnabledSecurity, Distribution.
    /// When empty (the default), all mail-enabled group types are loaded.
    /// </summary>
    public IReadOnlyList<string> GroupTypes { get; init; } = Array.Empty<string>();
}

public sealed record Microsoft365ContactDiagnosticOptions
{
    public string? User { get; init; }
    public IReadOnlyList<string> Contacts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ContactIds { get; init; } = Array.Empty<string>();
    public string? WorkEmail { get; init; }
    public bool Apply { get; init; }
}
