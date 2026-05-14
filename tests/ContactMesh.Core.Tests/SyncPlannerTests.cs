using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class SyncPlannerTests
{
    [Fact]
    public void CreatePlan_Creates_Missing_Managed_Contacts()
    {
        var desired = Contact("user-1", "Jane Doe");

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, Array.Empty<MeshContact>());

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Create, operation.OperationType);
        Assert.Equal(desired, operation.DesiredContact);
    }

    [Fact]
    public void CreatePlan_Updates_Changed_Managed_Contacts()
    {
        var desired = Contact("user-1", "Jane Doe") with
        {
            GivenName = "Jane",
            FamilyName = "Doe",
            CompanyName = "Example",
            Department = "Engineering",
            JobTitle = "Director",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["etag"] = "new" }
        };

        var existing = desired with
        {
            Department = "Operations",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["etag"] = "old" }
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Update, operation.OperationType);
        Assert.Equal("Engineering", operation.DesiredContact.Department);
        Assert.Equal("new", operation.DesiredContact.Metadata["etag"]);
    }

    [Fact]
    public void CreatePlan_Does_Not_Update_When_Only_Existing_Notes_Differ()
    {
        var desired = Contact("user-1", "Jane Doe") with
        {
            Notes = "Directory note"
        };

        var existing = desired with
        {
            Notes = "User-owned note"
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.NoChange, operation.OperationType);
        Assert.Equal("User-owned note", operation.DesiredContact.Notes);
    }

    [Fact]
    public void CreatePlan_Does_Not_Update_Equivalent_Contacts()
    {
        var desired = Contact("user-1", "Jane Doe") with
        {
            Emails = new[] { new ContactEmail("Jane@example.org", "work", true) }
        };

        var existing = desired with
        {
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) }
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.NoChange, operation.OperationType);
    }

    [Fact]
    public void CreatePlan_Deletes_Stale_Managed_Contacts()
    {
        var stale = Contact("user-1", "Jane Doe");

        var operations = new SyncPlanner().CreatePlan(Array.Empty<MeshContact>(), new[] { stale });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Delete, operation.OperationType);
        Assert.Equal(stale, operation.DesiredContact);
    }

    [Fact]
    public void CreatePlan_Ignores_Existing_Contacts_Without_SourceId()
    {
        var personalContact = new MeshContact
        {
            DisplayName = "Personal Contact",
            Emails = new[] { new ContactEmail("friend@example.net") }
        };

        var operations = new SyncPlanner().CreatePlan(Array.Empty<MeshContact>(), new[] { personalContact });

        Assert.Empty(operations);
    }

    private static MeshContact Contact(string sourceId, string displayName)
    {
        return new MeshContact
        {
            SourceId = sourceId,
            DisplayName = displayName,
            Emails = new[] { new ContactEmail($"{sourceId}@example.org", "work", true) },
            Phones = new[] { new ContactPhone("215-555-0100", "work", true) },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
        };
    }
}
