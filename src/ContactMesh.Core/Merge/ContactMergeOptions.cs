namespace ContactMesh.Core.Merge;

public sealed record ContactMergeOptions
{
    public IReadOnlySet<string> ManagedLabels { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
