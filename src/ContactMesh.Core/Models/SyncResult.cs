namespace ContactMesh.Core.Models;

public sealed record SyncResult
{
    public required string TargetUserId { get; init; }
    public bool DryRun { get; init; }
    public IReadOnlyList<SyncOperation> Operations { get; init; } = Array.Empty<SyncOperation>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public int CreateCount => Operations.Count(o => o.OperationType == SyncOperationType.Create);
    public int UpdateCount => Operations.Count(o => o.OperationType == SyncOperationType.Update);
    public int DeleteCount => Operations.Count(o => o.OperationType == SyncOperationType.Delete);
    public int NoChangeCount => Operations.Count(o => o.OperationType == SyncOperationType.NoChange);
}

public sealed record SyncOperation
{
    public required SyncOperationType OperationType { get; init; }
    public required MeshContact DesiredContact { get; init; }
    public MeshContact? ExistingContact { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public enum SyncOperationType
{
    NoChange,
    Create,
    Update,
    Delete
}
