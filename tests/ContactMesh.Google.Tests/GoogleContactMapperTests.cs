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
}
