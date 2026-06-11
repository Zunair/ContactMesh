// File: MicrosoftUserProviderTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Microsoft365.Directory;
using Xunit;

namespace ContactMesh.Microsoft365.Tests
{
    public sealed class MicrosoftUserProviderTests
    {
        [Fact]
        public async Task GetUsersAsync_Returns_Empty_When_Client_Is_Not_Configured()
        {
            var provider = new MicrosoftUserProvider();

            var users = await provider.GetUsersAsync(CancellationToken.None);

            Assert.Empty(users);
        }

        [Fact]
        public async Task GetUsersAsync_Maps_Valid_Graph_Users_And_Skips_Incomplete_Users()
        {
            var provider = new MicrosoftUserProvider(new FakeDirectoryClient(
                new MicrosoftGraphUser
                {
                    Id = "graph-user-1",
                    Mail = "jane@example.org",
                    DisplayName = "Jane Doe"
                },
                new MicrosoftGraphUser
                {
                    Id = "missing-email"
                },
                new MicrosoftGraphUser
                {
                    Mail = "missing-id@example.org"
                }));

            var users = await provider.GetUsersAsync(CancellationToken.None);

            var user = Assert.Single(users);
            Assert.Equal("graph-user-1", user.Id);
            Assert.Equal("jane@example.org", user.Email);
            Assert.Equal("Jane Doe", user.DisplayName);
        }

        private sealed class FakeDirectoryClient : IMicrosoftGraphDirectoryClient
        {
            private readonly IReadOnlyList<MicrosoftGraphUser> users;

            public FakeDirectoryClient(params MicrosoftGraphUser[] users)
            {
                this.users = users;
            }

            public Task<IReadOnlyList<MicrosoftGraphUser>> ListUsersAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(this.users);
            }
        }
    }
}
