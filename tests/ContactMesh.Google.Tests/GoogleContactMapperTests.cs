using ContactMesh.Google.Contacts;
using Xunit;

namespace ContactMesh.Google.Tests;

public sealed class GoogleContactMapperTests
{
    [Fact]
    public void ToMeshContact_Maps_Primary_Work_Email()
    {
        var contact = GoogleContactMapper.ToMeshContact("google-user-1", "Jane Doe", "jane@example.org");

        Assert.Equal("google-user-1", contact.SourceId);
        Assert.Equal("Jane Doe", contact.DisplayName);

        var email = Assert.Single(contact.Emails);
        Assert.Equal("jane@example.org", email.Address);
        Assert.Equal("work", email.Type);
        Assert.True(email.IsPrimary);
    }

    [Fact]
    public void ToMeshContact_Maps_Google_Person_Metadata_And_Fields()
    {
        var contact = GoogleContactMapper.ToMeshContact(
            new GooglePersonContact
            {
                ResourceName = "people/c123",
                ETag = "etag-1",
                SourceId = "directory-user-1",
                DisplayName = "Jane Doe",
                GivenName = "Jane",
                FamilyName = "Doe",
                CompanyName = "Example",
                Department = "Engineering",
                JobTitle = "Director",
                Emails = new[] { new GooglePersonEmail("jane@example.org", "work", true) },
                Phones = new[] { new GooglePersonPhone("+12155550100", "work", true) },
                ContactGroupResourceNames = new[] { "contactGroups/directory" }
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["contactGroups/directory"] = "Directory"
            });

        Assert.Equal("directory-user-1", contact.SourceId);
        Assert.Equal("people/c123", contact.Metadata[GoogleContactMapper.ResourceNameMetadataKey]);
        Assert.Equal("etag-1", contact.Metadata[GoogleContactMapper.ETagMetadataKey]);
        Assert.Equal("Jane", contact.GivenName);
        Assert.Equal("Doe", contact.FamilyName);
        Assert.Equal("Example", contact.CompanyName);
        Assert.Equal("Engineering", contact.Department);
        Assert.Equal("Director", contact.JobTitle);
        Assert.Equal("jane@example.org", Assert.Single(contact.Emails).Address);
        Assert.Equal("+12155550100", Assert.Single(contact.Phones).Number);
        Assert.Contains("Directory", contact.Labels);
    }

    [Fact]
    public void ToGooglePersonContact_Maps_MeshContact_For_Writes()
    {
        var contact = GoogleContactMapper.ToGooglePersonContact(
            new ContactMesh.Core.Models.MeshContact
            {
                SourceId = "directory-user-1",
                DisplayName = "Jane Doe",
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [GoogleContactMapper.ResourceNameMetadataKey] = "people/c123",
                    [GoogleContactMapper.ETagMetadataKey] = "etag-1"
                },
                Emails = new[] { new ContactMesh.Core.Models.ContactEmail("jane@example.org", "work", true) },
                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Directory"] = "contactGroups/directory"
            });

        Assert.Equal("people/c123", contact.ResourceName);
        Assert.Equal("etag-1", contact.ETag);
        Assert.Equal("directory-user-1", contact.SourceId);
        Assert.Equal("Jane Doe", contact.DisplayName);
        Assert.Equal("jane@example.org", Assert.Single(contact.Emails).Address);
        Assert.Equal("contactGroups/directory", Assert.Single(contact.ContactGroupResourceNames));
    }
}
