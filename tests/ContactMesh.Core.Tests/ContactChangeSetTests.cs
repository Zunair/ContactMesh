using ContactMesh.Core.Models;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class ContactChangeSetTests
{
    [Fact]
    public void FromOperations_Splits_Create_Update_And_Delete_Operations()
    {
        var create = Contact("create");
        var update = Contact("update");
        var delete = Contact("delete");

        var changeSet = ContactChangeSet.FromOperations(new[]
        {
            Operation(SyncOperationType.Create, create),
            Operation(SyncOperationType.Update, update),
            Operation(SyncOperationType.Delete, delete),
            Operation(SyncOperationType.NoChange, Contact("same"))
        });

        Assert.Equal(create, Assert.Single(changeSet.Creates));
        Assert.Equal(update, Assert.Single(changeSet.Updates));
        Assert.Equal(delete, Assert.Single(changeSet.Deletes));
        Assert.False(changeSet.DeleteWritesDisabled);
    }

    [Fact]
    public void FromOperations_DisableDeletes_Removes_Delete_Writes_And_Preserves_Flag()
    {
        var delete = Contact("delete");

        var changeSet = ContactChangeSet.FromOperations(
            new[] { Operation(SyncOperationType.Delete, delete) },
            deleteWritesDisabled: true);

        Assert.Empty(changeSet.Deletes);
        Assert.True(changeSet.DeleteWritesDisabled);
    }

    private static SyncOperation Operation(SyncOperationType operationType, MeshContact contact)
    {
        return new SyncOperation
        {
            OperationType = operationType,
            DesiredContact = contact
        };
    }

    private static MeshContact Contact(string sourceId)
    {
        return new MeshContact { SourceId = sourceId, DisplayName = sourceId };
    }
}
