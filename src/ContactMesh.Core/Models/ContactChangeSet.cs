namespace ContactMesh.Core.Models;

public sealed record ContactChangeSet
{
    public IReadOnlyList<MeshContact> Creates { get; init; } = Array.Empty<MeshContact>();
    public IReadOnlyList<MeshContact> Updates { get; init; } = Array.Empty<MeshContact>();
    public IReadOnlyList<MeshContact> Deletes { get; init; } = Array.Empty<MeshContact>();
    public bool DeleteWritesDisabled { get; init; }

    public static ContactChangeSet FromOperations(IEnumerable<SyncOperation> operations, bool deleteWritesDisabled = false)
    {
        var operationList = operations.ToList();

        return new ContactChangeSet
        {
            Creates = operationList.Where(o => o.OperationType == SyncOperationType.Create).Select(o => o.DesiredContact).ToList(),
            Updates = operationList.Where(o => o.OperationType == SyncOperationType.Update).Select(o => o.DesiredContact).ToList(),
            Deletes = deleteWritesDisabled
                ? Array.Empty<MeshContact>()
                : operationList.Where(o => o.OperationType == SyncOperationType.Delete).Select(o => o.DesiredContact).ToList(),
            DeleteWritesDisabled = deleteWritesDisabled
        };
    }
}
