using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class ContactPruneEngineTests
{
    [Fact]
    public void CreatePlan_Deletes_Blank_Contacts()
    {
        var blank = new MeshContact();

        var operations = Engine().CreatePlan(new[] { blank });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Delete, operation.OperationType);
        Assert.Equal("Contact is blank.", operation.Reason);
    }

    [Fact]
    public void CreatePlan_Deletes_Contacts_With_Only_Managed_Email()
    {
        var contact = new MeshContact
        {
            Emails = new[] { new ContactEmail("jane@example.org") }
        };

        var operations = Engine().CreatePlan(new[] { contact });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Delete, operation.OperationType);
        Assert.Equal("Contact only contains a managed-domain email.", operation.Reason);
    }

    [Fact]
    public void CreatePlan_Does_Not_Delete_Managed_Email_Contact_With_UserOwned_Data()
    {
        var contact = new MeshContact
        {
            Emails = new[] { new ContactEmail("jane@example.org") },
            Notes = "Do not delete"
        };

        var operations = Engine().CreatePlan(new[] { contact });

        Assert.Empty(operations);
    }

    [Fact]
    public void CreatePlan_Does_Not_Delete_Contact_With_Personal_Email()
    {
        var contact = new MeshContact
        {
            Emails = new[] { new ContactEmail("jane@example.net") }
        };

        var operations = Engine().CreatePlan(new[] { contact });

        Assert.Empty(operations);
    }

    [Fact]
    public void CreatePlan_Does_Not_Delete_Contact_With_Organization_Data()
    {
        var contact = new MeshContact
        {
            CompanyName = "Example"
        };

        var operations = Engine().CreatePlan(new[] { contact });

        Assert.Empty(operations);
    }

    private static ContactPruneEngine Engine()
    {
        return new ContactPruneEngine(new ContactPruneOptions
        {
            ManagedEmailDomains = new[] { "example.org" }
        });
    }
}
