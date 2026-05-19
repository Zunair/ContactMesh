namespace ContactMesh.Core.Merge;

public sealed record ContactMergeOptions
{
    public IReadOnlySet<string> ManagedLabels { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool ForceResetLabels { get; init; }
    public bool ForceDeduplicatePhones { get; init; }
}
