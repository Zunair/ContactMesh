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
    public void CreatePlan_Updates_Unmanaged_Contact_With_Matching_Email()
    {
        var desired = Contact("user-1", "Jane Doe") with
        {
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) },
            JobTitle = "Director"
        };
        var existing = new MeshContact
        {
            DisplayName = "Jane",
            Emails = new[]
            {
                new ContactEmail("JANE@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            },
            Notes = "Met at conference."
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Update, operation.OperationType);
        Assert.Equal(existing, operation.ExistingContact);
        Assert.Equal("user-1", operation.DesiredContact.SourceId);
        Assert.Equal("Director", operation.DesiredContact.JobTitle);
        Assert.Contains(operation.DesiredContact.Emails, email => email.Address == "jane.personal@example.net");
        Assert.Equal("Met at conference.", operation.DesiredContact.Notes);
        Assert.Equal("Existing contact matched by email.", operation.Reason);
    }

    [Fact]
    public void CreatePlan_Creates_When_Email_Match_Is_Ambiguous()
    {
        var desired = Contact("user-1", "Jane Doe") with
        {
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) }
        };
        var firstExisting = new MeshContact
        {
            DisplayName = "Jane",
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) }
        };
        var secondExisting = new MeshContact
        {
            DisplayName = "Jane Alt",
            Emails = new[] { new ContactEmail("JANE@example.org", "work", true) }
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { firstExisting, secondExisting });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Create, operation.OperationType);
        Assert.Null(operation.ExistingContact);
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
        var stale = Contact("user-1", "Jane Doe") with
        {
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["userId"] = "user-1" }
        };

        var operations = Planner().CreatePlan(Array.Empty<MeshContact>(), new[] { stale });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Delete, operation.OperationType);
        Assert.Equal(stale, operation.DesiredContact);
    }

    [Fact]
    public void CreatePlan_Preserves_Stale_Contacts_With_UserOwned_Data()
    {
        var stale = Contact("user-1", "Jane Doe") with
        {
            Notes = "Personal note",
            Emails = new[]
            {
                new ContactEmail("jane@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
        };

        var operations = Planner().CreatePlan(Array.Empty<MeshContact>(), new[] { stale });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Update, operation.OperationType);
        Assert.Null(operation.DesiredContact.SourceId);
        Assert.Equal("Personal note", operation.DesiredContact.Notes);
        Assert.Equal("jane.personal@example.net", Assert.Single(operation.DesiredContact.Emails).Address);
    }

    [Fact]
    public void CreatePlan_Deletes_Unmanaged_Stale_Contacts_With_Managed_Email()
    {
        var stale = new MeshContact
        {
            DisplayName = "Jane Doe",
            CompanyName = "Example",
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) }
        };

        var operations = Planner().CreatePlan(Array.Empty<MeshContact>(), new[] { stale });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Delete, operation.OperationType);
        Assert.Equal(stale, operation.DesiredContact);
        Assert.Equal("Managed contact is stale and has no user-owned details.", operation.Reason);
    }

    [Fact]
    public void CreatePlan_Preserves_Unmanaged_Stale_Contacts_With_Notes()
    {
        var stale = new MeshContact
        {
            DisplayName = "Jane Doe",
            CompanyName = "Example",
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) },
            Notes = "Keep this context."
        };

        var operations = Planner().CreatePlan(Array.Empty<MeshContact>(), new[] { stale });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Update, operation.OperationType);
        Assert.Null(operation.DesiredContact.DisplayName);
        Assert.Null(operation.DesiredContact.CompanyName);
        Assert.Empty(operation.DesiredContact.Emails);
        Assert.Equal("Keep this context.", operation.DesiredContact.Notes);
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

    private static SyncPlanner Planner()
    {
        return new SyncPlanner(
            staleContactCleanupEngine: new StaleContactCleanupEngine(new StaleContactCleanupOptions
            {
                ManagedEmailDomains = new[] { "example.org" },
                ManagedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
            }));
    }
}
