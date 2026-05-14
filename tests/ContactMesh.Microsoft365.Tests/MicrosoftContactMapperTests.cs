using ContactMesh.Microsoft365.Contacts;
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
}
