using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class DuplicateContactConsolidationEngineTests
{
    [Fact]
    public void CreatePlan_Merges_Duplicate_Managed_Contacts_By_Primary_Email()
    {
        var keeper = Contact("contact-1", "Jane", "jane@example.org") with
        {
            Phones = new[] { new ContactPhone("215-555-0100", "work", true) },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" },
            Notes = "Keeper note"
        };

        var duplicate = Contact("contact-2", "Jane Duplicate", "JANE@example.org") with
        {
            Emails = new[]
            {
                new ContactEmail("JANE@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            },
            Phones = new[] { new ContactPhone("267-555-2222", "mobile") },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sales" },
            Notes = "Duplicate note"
        };

        var operations = new DuplicateContactConsolidationEngine().CreatePlan(new[] { keeper, duplicate });

        Assert.Equal(2, operations.Count);

        var update = operations.Single(operation => operation.OperationType == SyncOperationType.Update);
        Assert.Equal(keeper, update.ExistingContact);
        Assert.Contains(update.DesiredContact.Emails, email => email.Address == "jane@example.org");
        Assert.Contains(update.DesiredContact.Emails, email => email.Address == "jane.personal@example.net");
        Assert.Contains(update.DesiredContact.Phones, phone => phone.Number == "215-555-0100");
        Assert.Contains(update.DesiredContact.Phones, phone => phone.Number == "267-555-2222");
        Assert.Contains("Directory", update.DesiredContact.Labels);
        Assert.Contains("Sales", update.DesiredContact.Labels);
        Assert.Contains("Keeper note", update.DesiredContact.Notes);
        Assert.Contains("Duplicate note", update.DesiredContact.Notes);

        var delete = operations.Single(operation => operation.OperationType == SyncOperationType.Delete);
        Assert.Equal(duplicate, delete.DesiredContact);
    }

    [Fact]
    public void CreatePlan_Ignores_Unmanaged_Duplicates()
    {
        var first = new MeshContact { Emails = new[] { new ContactEmail("jane@example.org", "work", true) } };
        var second = new MeshContact { Emails = new[] { new ContactEmail("JANE@example.org", "work", true) } };

        var operations = new DuplicateContactConsolidationEngine().CreatePlan(new[] { first, second });

        Assert.Empty(operations);
    }

    [Fact]
    public void CreatePlan_Does_Not_Create_Update_When_Keeper_Already_Has_All_Details()
    {
        var keeper = Contact("contact-1", "Jane", "jane@example.org") with
        {
            Emails = new[]
            {
                new ContactEmail("jane@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            }
        };

        var duplicate = Contact("contact-2", "Jane Duplicate", "JANE@example.org") with
        {
            Emails = new[]
            {
                new ContactEmail("JANE@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            }
        };

        var operations = new DuplicateContactConsolidationEngine().CreatePlan(new[] { keeper, duplicate });

        Assert.DoesNotContain(operations, operation => operation.OperationType == SyncOperationType.Update);
        Assert.Single(operations, operation => operation.OperationType == SyncOperationType.Delete);
    }

    [Fact]
    public void CreatePlan_Merges_Phone_Variants_With_Hyphenated_Display_Format()
    {
        var keeper = Contact("contact-1", "Jane", "jane@example.org") with
        {
            Phones = new[]
            {
                new ContactPhone("+12675073489", "work", true),
                new ContactPhone("12675073489", "work")
            }
        };

        var duplicate = Contact("contact-2", "Jane Duplicate", "JANE@example.org") with
        {
            Phones = new[]
            {
                new ContactPhone("2675073489", "work"),
                new ContactPhone("267-507-3489", "work")
            }
        };

        var operations = new DuplicateContactConsolidationEngine().CreatePlan(new[] { keeper, duplicate });

        var update = operations.Single(operation => operation.OperationType == SyncOperationType.Update);
        var phone = Assert.Single(update.DesiredContact.Phones);
        Assert.Equal("267-507-3489", phone.Number);
        Assert.Equal("work", phone.Type);
    }

    private static MeshContact Contact(string sourceId, string displayName, string email)
    {
        return new MeshContact
        {
            SourceId = sourceId,
            DisplayName = displayName,
            Emails = new[] { new ContactEmail(email, "work", true) }
        };
    }
}
