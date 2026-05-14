using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed record ContactSyncRunResult
{
    public IReadOnlyList<SyncResult> Results { get; init; } = Array.Empty<SyncResult>();

    public int TargetCount => this.Results.Count;
    public int CreateCount => this.Results.Sum(result => result.CreateCount);
    public int UpdateCount => this.Results.Sum(result => result.UpdateCount);
    public int DeleteCount => this.Results.Sum(result => result.DeleteCount);
    public int NoChangeCount => this.Results.Sum(result => result.NoChangeCount);
}
