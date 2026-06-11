// File: GooglePeopleContactProviderTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Google.Contacts;
using Xunit;

namespace ContactMesh.Google.Tests
{
    public sealed class GooglePeopleContactProviderTests
    {
        [Fact]
        public async Task GetContactsAsync_Reads_From_Client_And_Maps_To_MeshContacts()
        {
            var provider = new GooglePeopleContactProvider(
                new FakePeopleContactClient(),
                new FakeContactGroupLabelClient());

            var contacts = await provider.GetContactsAsync("user@example.org", CancellationToken.None);

            var contact = Assert.Single(contacts);
            Assert.Equal("directory-user-1", contact.SourceId);
            Assert.Equal("Jane Doe", contact.DisplayName);
            Assert.Equal("people/c123", contact.Metadata[GoogleContactMapper.ResourceNameMetadataKey]);
            Assert.Contains("Directory", contact.Labels);
        }

        private sealed class FakePeopleContactClient : IGooglePeopleContactClient
        {
            public Task<IReadOnlyList<GooglePersonContact>> ListAsync(string userId, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<GooglePersonContact>>(new[]
                {
                    new GooglePersonContact
                    {
                        ResourceName = "people/c123",
                        SourceId = "directory-user-1",
                        DisplayName = "Jane Doe",
                        ContactGroupResourceNames = new[] { "contactGroups/directory" }
                    }
                });
            }

            public Task CreateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(string userId, GooglePersonContact contact, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeContactGroupLabelClient : IGoogleContactGroupLabelClient
        {
            public Task<IReadOnlyList<GoogleContactGroupLabel>> ListAsync(string userId, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<GoogleContactGroupLabel>>(new[]
                {
                    new GoogleContactGroupLabel(
                        "contactGroups/directory",
                        "Directory",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            [GoogleContactGroupLabelReconciler.LabelNameClientDataKey] = "Directory"
                        })
                });
            }

            public Task CreateAsync(
                string userId,
                string labelName,
                IReadOnlyDictionary<string, string> clientData,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(
                string userId,
                string resourceName,
                string labelName,
                IReadOnlyDictionary<string, string> clientData,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(string userId, string resourceName, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
