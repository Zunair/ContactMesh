using ContactMesh.Microsoft365.Contacts;
using ContactMesh.Core.Models;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftContactMapperTests
{
    [Fact]
    public void ToMeshContact_Maps_Primary_Work_Email()
    {
        var contact = MicrosoftContactMapper.ToMeshContact("graph-user-1", "Jane Doe", "jane@example.org");

        Assert.Equal("graph-user-1", contact.SourceId);
        Assert.Equal("Jane Doe", contact.DisplayName);

        var email = Assert.Single(contact.Emails);
        Assert.Equal("jane@example.org", email.Address);
        Assert.Equal("work", email.Type);
        Assert.True(email.IsPrimary);
    }

    [Fact]
    public void ToMeshContact_Maps_Graph_Contact_Metadata_And_Categories()
    {
        var contact = MicrosoftContactMapper.ToMeshContact(new MicrosoftGraphContact
        {
            Id = "contact-1",
            ChangeKey = "change-1",
            SourceId = "directory-user-1",
            DisplayName = "Jane Doe",
            GivenName = "Jane",
            Surname = "Doe",
            CompanyName = "Example",
            Department = "Engineering",
            JobTitle = "Director",
            EmailAddresses = new[]
            {
                new MicrosoftGraphEmailAddress("jane@example.org", "Jane Doe"),
                new MicrosoftGraphEmailAddress("j.doe@example.org", "Jane Doe")
            },
            BusinessPhones = new[] { "+1 215 555 0100" },
            MobilePhone = "+1 215 555 0101",
            Categories = new[] { "Directory" },
            PersonalNotes = "managed"
        });

        Assert.Equal("directory-user-1", contact.SourceId);
        Assert.Equal("Jane", contact.GivenName);
        Assert.Equal("Doe", contact.FamilyName);
        Assert.Equal("contact-1", contact.Metadata[MicrosoftContactMapper.ContactIdMetadataKey]);
        Assert.Equal("change-1", contact.Metadata[MicrosoftContactMapper.ChangeKeyMetadataKey]);
        Assert.Equal("managed", contact.Notes);
        Assert.Equal("jane@example.org", contact.Emails[0].Address);
        Assert.True(contact.Emails[0].IsPrimary);
        Assert.False(contact.Emails[1].IsPrimary);
        Assert.Contains(new ContactPhone("+1 215 555 0100", "work"), contact.Phones);
        Assert.Contains(new ContactPhone("+1 215 555 0101", "mobile"), contact.Phones);
        Assert.Contains("Directory", contact.Labels);
    }

    [Fact]
    public void ToMicrosoftGraphContact_Maps_MeshContact_Metadata_And_Labels()
    {
        var contact = MicrosoftContactMapper.ToMicrosoftGraphContact(new MeshContact
        {
            SourceId = "directory-user-1",
            DisplayName = "Jane Doe",
            GivenName = "Jane",
            FamilyName = "Doe",
            CompanyName = "Example",
            Department = "Engineering",
            JobTitle = "Director",
            Notes = "managed",
            Emails = new[] { new ContactEmail("jane@example.org", "work", true) },
            Phones = new[]
            {
                new ContactPhone("+1 215 555 0100", "work"),
                new ContactPhone("+1 215 555 0101", "mobile")
            },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [MicrosoftContactMapper.ContactIdMetadataKey] = "contact-1",
                [MicrosoftContactMapper.ChangeKeyMetadataKey] = "change-1"
            }
        });

        Assert.Equal("contact-1", contact.Id);
        Assert.Equal("change-1", contact.ChangeKey);
        Assert.Equal("directory-user-1", contact.SourceId);
        Assert.Equal("Doe", contact.Surname);
        Assert.Equal("jane@example.org", Assert.Single(contact.EmailAddresses).Address);
        Assert.Equal("+1 215 555 0100", Assert.Single(contact.BusinessPhones));
        Assert.Equal("+1 215 555 0101", contact.MobilePhone);
        Assert.Equal("Directory", Assert.Single(contact.Categories));
    }
}
