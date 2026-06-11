// File: MicrosoftContactProviderTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Microsoft365.Contacts;
using Xunit;

namespace ContactMesh.Microsoft365.Tests
{
    public sealed class MicrosoftContactProviderTests
    {
        [Fact]
        public async Task GetContactsAsync_Returns_Empty_When_Client_Is_Not_Configured()
        {
            var provider = new MicrosoftContactProvider();

            var contacts = await provider.GetContactsAsync("user@example.org", CancellationToken.None);

            Assert.Empty(contacts);
        }

        [Fact]
        public async Task GetContactsAsync_Reads_From_Client_And_Maps_To_MeshContacts()
        {
            var provider = new MicrosoftContactProvider(new FakeGraphContactClient());

            var contacts = await provider.GetContactsAsync("user@example.org", CancellationToken.None);

            var contact = Assert.Single(contacts);
            Assert.Equal("directory-user-1", contact.SourceId);
            Assert.Equal("Jane Doe", contact.DisplayName);
            Assert.Equal("contact-1", contact.Metadata[MicrosoftContactMapper.ContactIdMetadataKey]);
            Assert.Contains("Directory", contact.Labels);
        }

        private sealed class FakeGraphContactClient : IMicrosoftGraphContactClient
        {
            public Task<IReadOnlyList<MicrosoftGraphContact>> ListAsync(
                string userId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<MicrosoftGraphContact>>(new[]
                {
                    new MicrosoftGraphContact
                    {
                        Id = "contact-1",
                        SourceId = "directory-user-1",
                        DisplayName = "Jane Doe",
                        Categories = new[] { "Directory" }
                    }
                });
            }

            public Task CreateAsync(
                string userId,
                MicrosoftGraphContact contact,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(
                string userId,
                MicrosoftGraphContact contact,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(
                string userId,
                string contactId,
                string? contactFolderId,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
