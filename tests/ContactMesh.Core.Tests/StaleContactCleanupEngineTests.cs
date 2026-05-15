using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class StaleContactCleanupEngineTests
{
    [Fact]
    public void Clean_Deletes_Stale_Contact_When_No_UserOwned_Data_Remains()
    {
        var contact = ManagedContact();
        var engine = Engine();

        var result = engine.Clean(contact);

        Assert.True(result.ShouldDelete);
        Assert.Equal(contact, result.Contact);
    }

    [Fact]
    public void Clean_Preserves_Contact_With_UserOwned_Notes()
    {
        var contact = ManagedContact() with { Notes = "Call after 5 PM." };
        var engine = Engine();

        var result = engine.Clean(contact);

        Assert.False(result.ShouldDelete);
        Assert.Null(result.Contact.SourceId);
        Assert.Equal("Call after 5 PM.", result.Contact.Notes);
        Assert.Empty(result.Contact.Emails);
        Assert.Empty(result.Contact.Phones);
        Assert.Empty(result.Contact.Labels);
        Assert.Contains("notes=Call after 5 PM.", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Clean_Deletes_Stale_Contact_When_Notes_Only_Contain_Managed_Phone_Migration_Data()
    {
        var contact = ManagedContact() with
        {
            Notes = """
                1
                workMobile: +12156179107
                """
        };

        var result = Engine().Clean(contact);

        Assert.True(result.ShouldDelete);
        Assert.Equal("Managed contact is stale and has no user-owned details.", result.Reason);
    }

    [Fact]
    public void Clean_Strips_Managed_Phone_Migration_Notes_And_Preserves_Other_UserOwned_Data()
    {
        var contact = ManagedContact() with
        {
            Notes = """
                2
                work: +12155550100
                workMobile: +12156179107
                """,
            Emails = new[]
            {
                new ContactEmail("jane@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            }
        };

        var result = Engine().Clean(contact);

        Assert.False(result.ShouldDelete);
        Assert.Null(result.Contact.Notes);
        Assert.Equal("jane.personal@example.net", Assert.Single(result.Contact.Emails).Address);
        Assert.DoesNotContain("notes=", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Clean_Preserves_Notes_When_Migrated_Phone_Block_Contains_UserOwned_Type()
    {
        var contact = ManagedContact() with
        {
            Notes = """
                1
                mobile: +12156179107
                """
        };

        var result = Engine().Clean(contact);

        Assert.False(result.ShouldDelete);
        Assert.Equal(contact.Notes, result.Contact.Notes);
        Assert.Contains("notes=", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Clean_Strips_Managed_Data_And_Preserves_UserOwned_Email_And_Phone()
    {
        var contact = ManagedContact() with
        {
            Emails = new[]
            {
                new ContactEmail("jane@example.org", "work", true),
                new ContactEmail("jane.personal@example.net", "home")
            },
            Phones = new[]
            {
                new ContactPhone("215-555-0100", "work", true),
                new ContactPhone("267-555-2222", "mobile")
            },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory", "Personal" },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userId"] = "user-1",
                ["nickname"] = "JD"
            }
        };

        var result = Engine().Clean(contact);

        Assert.False(result.ShouldDelete);
        Assert.Null(result.Contact.SourceId);
        Assert.Null(result.Contact.DisplayName);
        Assert.DoesNotContain(result.Contact.Emails, email => email.Address.EndsWith("@example.org", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("jane.personal@example.net", Assert.Single(result.Contact.Emails).Address);
        Assert.Equal("267-555-2222", Assert.Single(result.Contact.Phones).Number);
        Assert.DoesNotContain("Directory", result.Contact.Labels);
        Assert.Contains("Personal", result.Contact.Labels);
        Assert.False(result.Contact.Metadata.ContainsKey("userId"));
        Assert.Equal("JD", result.Contact.Metadata["nickname"]);
        Assert.Contains("emails=[jane.personal@example.net]", result.Reason, StringComparison.Ordinal);
        Assert.Contains("phones=[267-555-2222]", result.Reason, StringComparison.Ordinal);
        Assert.Contains("labels=[Personal]", result.Reason, StringComparison.Ordinal);
        Assert.Contains("metadata=[nickname=JD]", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Clean_Includes_Metadata_Values_In_Preserved_Details()
    {
        var contact = ManagedContact() with
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["customA"] = "Alpha",
                ["customB"] = "Bravo"
            }
        };

        var result = Engine().Clean(contact);

        Assert.False(result.ShouldDelete);
        Assert.Contains("metadata=[customA=Alpha; customB=Bravo]", result.Reason, StringComparison.Ordinal);
    }

    private static StaleContactCleanupEngine Engine()
    {
        return new StaleContactCleanupEngine(new StaleContactCleanupOptions
        {
            ManagedEmailDomains = new[] { "example.org" },
            ManagedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
        });
    }

    private static MeshContact ManagedContact()
    {
        return new MeshContact
        {
            SourceId = "user-1",
            DisplayName = "Jane Doe",
            GivenName = "Jane",
            FamilyName = "Doe",
            CompanyName = "Example",
            Department = "Engineering",
            JobTitle = "Director",
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) },
            Phones = new[] { new ContactPhone("215-555-0100", "work", true) },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["userId"] = "user-1" }
        };
    }
}
