using ContactMesh.Microsoft365.Directory;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftDirectoryMapperTests
{
    [Fact]
    public void ToMeshUser_Maps_Identity_Email_And_DisplayName()
    {
        var user = MicrosoftDirectoryMapper.ToMeshUser("graph-user-1", "jane@example.org", "Jane Doe");

        Assert.Equal("graph-user-1", user.Id);
        Assert.Equal("jane@example.org", user.Email);
        Assert.Equal("Jane Doe", user.DisplayName);
    }
}
