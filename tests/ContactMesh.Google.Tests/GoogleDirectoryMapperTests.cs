// File: GoogleDirectoryMapperTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Google.Directory;
using Xunit;

namespace ContactMesh.Google.Tests
{
    public sealed class GoogleDirectoryMapperTests
    {
        [Fact]
        public void ToMeshUser_Maps_Identity_Email_And_DisplayName()
        {
            var user = GoogleDirectoryMapper.ToMeshUser("google-user-1", "jane@example.org", "Jane Doe", "Jane", "Doe", "/Staff");

            Assert.Equal("google-user-1", user.Id);
            Assert.Equal("jane@example.org", user.Email);
            Assert.Equal("Jane Doe", user.DisplayName);
            Assert.Equal("Jane", user.GivenName);
            Assert.Equal("Doe", user.FamilyName);
            Assert.Equal("/Staff", user.OrganizationUnit);
        }
    }
}
