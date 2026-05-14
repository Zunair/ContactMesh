using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class ContactEmailPolicyEngineTests
{
    [Fact]
    public void Apply_Removes_Duplicate_Email_Addresses_Case_Insensitively()
    {
        var contact = new MeshContact
        {
            Emails = new[]
            {
                new ContactEmail("jane@example.org", "work"),
                new ContactEmail("JANE@example.org", "home", true)
            }
        };

        var updated = Engine().Apply(contact);

        var email = Assert.Single(updated.Emails);
        Assert.Equal("JANE@example.org", email.Address);
        Assert.True(email.IsPrimary);
    }

    [Fact]
    public void Apply_Makes_Resolved_SendAs_Email_Primary_Work_Email_When_Present()
    {
        var contact = new MeshContact
        {
            Emails = new[]
            {
                new ContactEmail("jane@example.org", "other", true),
                new ContactEmail("j.doe@example.org", "home")
            }
        };

        var updated = Engine().Apply(contact, "j.doe@example.org");

        Assert.False(updated.Emails[0].IsPrimary);
        Assert.True(updated.Emails[1].IsPrimary);
        Assert.Equal("work", updated.Emails[1].Type);
    }

    [Fact]
    public void Apply_Makes_First_Managed_Email_Primary_When_SendAs_Is_Not_Present()
    {
        var contact = new MeshContact
        {
            Emails = new[]
            {
                new ContactEmail("jane.personal@example.net", "home", true),
                new ContactEmail("jane@example.org", "other")
            }
        };

        var updated = Engine().Apply(contact, "missing@example.org");

        Assert.False(updated.Emails[0].IsPrimary);
        Assert.True(updated.Emails[1].IsPrimary);
        Assert.Equal("work", updated.Emails[1].Type);
    }

    [Fact]
    public void Apply_Preserves_Primary_When_No_Managed_Or_SendAs_Email_Exists()
    {
        var contact = new MeshContact
        {
            Emails = new[]
            {
                new ContactEmail("jane.personal@example.net", "home", true),
                new ContactEmail("jane.alt@example.net", "other")
            }
        };

        var updated = Engine().Apply(contact);

        Assert.True(updated.Emails[0].IsPrimary);
        Assert.False(updated.Emails[1].IsPrimary);
    }

    private static ContactEmailPolicyEngine Engine()
    {
        return new ContactEmailPolicyEngine(new ContactEmailPolicyOptions
        {
            ManagedEmailDomains = new[] { "example.org" }
        });
    }
}
