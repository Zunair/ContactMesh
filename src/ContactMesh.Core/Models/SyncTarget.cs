namespace ContactMesh.Core.Models;

public sealed record SyncTarget
{
    public required string UserId { get; init; }
    public required string UserEmail { get; init; }
    public IReadOnlySet<string> LabelNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
