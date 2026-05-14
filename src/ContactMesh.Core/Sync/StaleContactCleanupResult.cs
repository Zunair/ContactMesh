using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed record StaleContactCleanupResult
{
    public required bool ShouldDelete { get; init; }
    public required MeshContact Contact { get; init; }
    public string Reason { get; init; } = string.Empty;
}
