using ContactMesh.Core.Logging;

namespace ContactMesh.Core.Models;

public sealed record SyncResult
{
    public required string TargetUserId { get; init; }
    public bool DryRun { get; init; }
    public IReadOnlyList<SyncOperation> Operations { get; init; } = Array.Empty<SyncOperation>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SyncLogEntry> LogEntries { get; init; } = Array.Empty<SyncLogEntry>();

    public int CreateCount => Operations.Count(o => o.OperationType == SyncOperationType.Create);
    public int UpdateCount => Operations.Count(o => o.OperationType == SyncOperationType.Update);
    public int DeleteCount => Operations.Count(o => o.OperationType == SyncOperationType.Delete);
    public int NoChangeCount => Operations.Count(o => o.OperationType == SyncOperationType.NoChange);
    public int WriteCount => CreateCount + UpdateCount + DeleteCount;
    public int WarningCount => Warnings.Count;
    public int ErrorCount => Errors.Count;
    public bool HasWarnings => WarningCount > 0;
    public bool HasErrors => ErrorCount > 0;
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
