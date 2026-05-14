namespace ContactMesh.Core.Sync;

public sealed record StaleContactCleanupOptions
{
    public IReadOnlyList<string> ManagedEmailDomains { get; init; } = Array.Empty<string>();
    public IReadOnlySet<string> ManagedPhoneTypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "work",
        "workMobile",
        "workFax"
    };
    public IReadOnlySet<string> ManagedMetadataKeys { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sourceId",
        "userId",
        "manager"
    };
    public IReadOnlySet<string> ManagedLabels { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
