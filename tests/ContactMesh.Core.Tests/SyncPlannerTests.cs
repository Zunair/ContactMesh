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
    public void CreatePlan_Updates_Unmanaged_Contact_With_Matching_Alias_And_Warns()
    {
        var desired = Contact("user-1", "Jane Doe") with
        {
            Emails = new[] { new ContactEmail("primary@example.org", "work", true) },
            MatchEmails = new[] { "primary@example.org", "alias@example.org" }
        };
        var existing = new MeshContact
        {
            DisplayName = "Jane",
            Emails = new[] { new ContactEmail("alias@example.org", "work", true) }
        };

        var operation = Assert.Single(new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing }));

        Assert.Equal(SyncOperationType.Update, operation.OperationType);
        Assert.Equal(existing, operation.ExistingContact);
        Assert.Contains("matched by alternate email alias@example.org", Assert.Single(operation.Warnings));
        Assert.Equal("primary@example.org", Assert.Single(operation.DesiredContact.Emails).Address);
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
        Assert.Equal("Jane Doe", operation.DesiredContact.DisplayName);
        Assert.Equal("Example", operation.DesiredContact.CompanyName);
        Assert.Empty(operation.DesiredContact.Emails);
        Assert.Equal("Keep this context.", operation.DesiredContact.Notes);
        Assert.Contains("notes", operation.Reason, StringComparison.Ordinal);
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

    private static SyncPlanner PlannerWithEmailPolicy()
    {
        return new SyncPlanner(
            emailPolicyEngine: new ContactEmailPolicyEngine(new ContactEmailPolicyOptions
            {
                ManagedEmailDomains = new[] { "example.org" }
            }));
    }

    [Fact]
    public void CreatePlan_Upgrades_Other_Typed_Managed_Email_To_Work_On_Create()
    {
        var desired = new MeshContact
        {
            SourceId = "user-1",
            Emails = new[] { new ContactEmail("jane@example.org", "other") }
        };

        var operations = PlannerWithEmailPolicy().CreatePlan(new[] { desired }, Array.Empty<MeshContact>());

        var op = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Create, op.OperationType);
        var email = Assert.Single(op.DesiredContact.Emails);
        Assert.Equal("work", email.Type);
        Assert.True(email.IsPrimary);
    }

    [Fact]
    public void CreatePlan_Upgrades_Other_Typed_Managed_Email_To_Work_On_Update()
    {
        var desired = new MeshContact
        {
            SourceId = "user-1",
            Emails = new[] { new ContactEmail("jane@example.org", "other") }
        };

        var existing = new MeshContact
        {
            SourceId = "user-1",
            Emails = new[] { new ContactEmail("jane@example.org", "other") }
        };

        var operations = PlannerWithEmailPolicy().CreatePlan(new[] { desired }, new[] { existing });

        // Merged contact has same address but email policy upgrades type to "work";
        // existing still has "other", so the operation is an Update.
        var op = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Update, op.OperationType);
        var email = Assert.Single(op.DesiredContact.Emails);
        Assert.Equal("work", email.Type);
    }

    [Fact]
    public void CreatePlan_Does_Not_Upgrade_Non_Managed_Domain_Email_Type()
    {
        var desired = new MeshContact
        {
            SourceId = "user-1",
            Emails = new[] { new ContactEmail("jane@personal.net", "other") }
        };

        var operations = PlannerWithEmailPolicy().CreatePlan(new[] { desired }, Array.Empty<MeshContact>());

        var op = Assert.Single(operations);
        var email = Assert.Single(op.DesiredContact.Emails);
        Assert.Equal("other", email.Type);
    }

    [Fact]
    public void CreatePlan_NoChange_When_Merged_Has_Extra_Ephemeral_Metadata_Keys()
    {
        // Simulates the Microsoft 365 round-trip: the provider only stores and returns its own
        // bookkeeping keys (e.g. ContactId, ChangeKey), while the source adds transient keys
        // (sourceRule, userId, etc.) that are never persisted. After MergeMetadata the merged
        // contact has more keys than the existing one, but the comparison must not trigger a
        // spurious Update operation.
        var desired = Contact("user-1", "Jane Doe") with
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceRule"] = "Directory",
                ["userId"] = "user-1"
            }
        };

        var existing = desired with
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Provider only returned its own keys; ephemeral source keys are absent.
                ["providerContactId"] = "abc-123",
                ["providerChangeKey"] = "AAABBB=="
            }
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.NoChange, operation.OperationType);
    }

    [Fact]
    public void CreatePlan_Updates_When_Persisted_Metadata_Value_Changes()
    {
        // Even with the relaxed comparison, a change in a value that IS present in the existing
        // contact (e.g. a provider contact ID that got reassigned) must still trigger an Update.
        var desired = Contact("user-1", "Jane Doe") with
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceRule"] = "Directory",
                ["providerContactId"] = "xyz-999"   // source overwrites the old value
            }
        };

        var existing = desired with
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["providerContactId"] = "abc-123",  // old value from provider
                ["providerChangeKey"] = "AAABBB=="
            }
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.Update, operation.OperationType);
    }

    [Fact]
    public void CreatePlan_NoChange_When_Phones_Same_But_Different_Order()
    {
        // Microsoft Graph may return phones in a different order than what was written.
        // The comparison must be order-insensitive to avoid a spurious Update every run.
        var phone1 = new ContactPhone("267-507-3813", "work");
        var phone2 = new ContactPhone("267-804-6444", "mobile");
        var phone3 = new ContactPhone("+12675073780", "work");

        var desired = Contact("user-1", "Jane Doe") with
        {
            Phones = new[] { phone1, phone2, phone3 }
        };

        var existing = desired with
        {
            Phones = new[] { phone1, phone3, phone2 }  // same phones, different order
        };

        var operations = new SyncPlanner().CreatePlan(new[] { desired }, new[] { existing });

        var operation = Assert.Single(operations);
        Assert.Equal(SyncOperationType.NoChange, operation.OperationType);
    }
}
