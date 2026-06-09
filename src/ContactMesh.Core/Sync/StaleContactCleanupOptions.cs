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

    /// <summary>
    /// Metadata keys that identify a contact as app-managed for stale cleanup. These markers are
    /// separate from <see cref="ManagedMetadataKeys"/> so providers can preserve write-routing
    /// metadata while still using it to recognize legacy managed contacts.
    /// </summary>
    public IReadOnlySet<string> ManagedMarkerMetadataKeys { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When true, all labels are treated as managed and removed from stale contacts, regardless
    /// of whether they appear in <see cref="ManagedLabels"/>. Use this to clean up labels that
    /// were applied by a previous sync configuration that is no longer active.
    /// </summary>
    public bool ForceResetLabels { get; init; }

    /// <summary>
    /// Metadata keys that are required by the provider to perform write operations (e.g. a contact
    /// identifier needed to issue a PATCH request). These keys are excluded from
    /// <see cref="HasUserOwnedData"/> so they do not prevent a stale contact from being deleted,
    /// but they are preserved in the cleaned contact so that write operations can succeed.
    /// </summary>
    public IReadOnlySet<string> OperationalMetadataKeys { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
