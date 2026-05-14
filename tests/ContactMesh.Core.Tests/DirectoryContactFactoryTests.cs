using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class DirectoryContactFactoryTests
{
    [Fact]
    public void CreateUserContact_Maps_User_To_Managed_Work_Contact()
    {
        var user = new MeshUser
        {
            Id = "user-1",
            Email = "jane@example.org",
            DisplayName = "Jane Doe",
            GivenName = "Jane",
            FamilyName = "Doe",
            CompanyName = "Example",
            Department = "Engineering",
            JobTitle = "Director",
            OrganizationUnit = "/Staff",
            Phones = new[]
            {
                new ContactPhone("215-555-0100", "work", true),
                new ContactPhone("267-555-2222", "workMobile")
            },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["manager"] = "Pat Example"
            }
        };

        var contact = new DirectoryContactFactory().CreateUserContact(user, new[] { "Directory", "Engineering" });

        Assert.Equal("user-1", contact.SourceId);
        Assert.Equal("Jane Doe", contact.DisplayName);
        Assert.Equal("Jane", contact.GivenName);
        Assert.Equal("Doe", contact.FamilyName);
        Assert.Equal("Example", contact.CompanyName);
        Assert.Equal("Engineering", contact.Department);
        Assert.Equal("Director", contact.JobTitle);
        Assert.Equal(2, contact.Phones.Count);
        Assert.Equal("jane@example.org", Assert.Single(contact.Emails).Address);
        Assert.Contains("Directory", contact.Labels);
        Assert.Contains("Engineering", contact.Labels);
        Assert.Equal("user-1", contact.Metadata["userId"]);
        Assert.Equal("/Staff", contact.Metadata["organizationUnit"]);
        Assert.Equal("Pat Example", contact.Metadata["manager"]);
    }

    [Fact]
    public void CreateUserContact_Uses_EmailOverride_For_SendAs_Address()
    {
        var user = new MeshUser
        {
            Id = "user-1",
            Email = "jane@example.org"
        };

        var contact = new DirectoryContactFactory().CreateUserContact(user, emailOverride: "j.doe@example.org");

        var email = Assert.Single(contact.Emails);
        Assert.Equal("j.doe@example.org", email.Address);
        Assert.Equal("work", email.Type);
        Assert.True(email.IsPrimary);
    }

    [Fact]
    public void CreateUserContact_Falls_Back_To_Name_Then_Email_For_DisplayName()
    {
        var factory = new DirectoryContactFactory();

        var namedContact = factory.CreateUserContact(new MeshUser
        {
            Id = "user-1",
            Email = "jane@example.org",
            GivenName = "Jane",
            FamilyName = "Doe"
        });

        var emailContact = factory.CreateUserContact(new MeshUser
        {
            Id = "user-2",
            Email = "unknown@example.org"
        });

        Assert.Equal("Jane Doe", namedContact.DisplayName);
        Assert.Equal("unknown@example.org", emailContact.DisplayName);
    }

    [Fact]
    public void CreateUserContact_Deduplicates_Labels_Case_Insensitively()
    {
        var user = new MeshUser
        {
            Id = "user-1",
            Email = "jane@example.org"
        };

        var contact = new DirectoryContactFactory().CreateUserContact(user, new[] { "Directory", "directory", "" });

        Assert.Single(contact.Labels);
        Assert.Contains("Directory", contact.Labels);
    }
}
