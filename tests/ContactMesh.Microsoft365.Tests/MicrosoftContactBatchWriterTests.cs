// File: MicrosoftContactBatchWriterTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Microsoft365.Contacts;
using Xunit;

namespace ContactMesh.Microsoft365.Tests
{
    public sealed class MicrosoftContactBatchWriterTests
    {
        [Fact]
        public async Task ApplyAsync_Writes_Create_Update_Delete_Contacts_When_Client_Is_Configured()
        {
            var client = new FakeGraphContactClient();
            var writer = new MicrosoftContactBatchWriter(client);

            await writer.ApplyAsync(
                "user@example.org",
                new ContactChangeSet
                {
                    Creates = new[] { Contact("directory-user-1") },
                    Updates = new[]
                    {
                        Contact("directory-user-2") with
                        {
                            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [MicrosoftContactMapper.ContactIdMetadataKey] = "contact-2"
                            }
                        }
                    },
                    Deletes = new[]
                    {
                        Contact("directory-user-3") with
                        {
                            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [MicrosoftContactMapper.ContactIdMetadataKey] = "contact-3",
                                [MicrosoftContactMapper.ContactFolderIdMetadataKey] = "folder-3"
                            }
                        }
                    }
                },
                CancellationToken.None);

            Assert.Equal(
                new[]
                {
                    "create:user@example.org:directory-user-1::Directory",
                    "update:user@example.org:directory-user-2:contact-2:Directory",
                    "delete:user@example.org:folder-3:contact-3"
                },
                client.Calls);
        }

        [Fact]
        public async Task ApplyAsync_Skips_Writes_When_Client_Is_Not_Configured()
        {
            var writer = new MicrosoftContactBatchWriter();

            await writer.ApplyAsync(
                "user@example.org",
                new ContactChangeSet
                {
                    Creates = new[] { Contact("directory-user-1") }
                },
                CancellationToken.None);
        }

        [Fact]
        public async Task ApplyAsync_DisableDeletes_Skips_Delete_Contacts()
        {
            var client = new FakeGraphContactClient();
            var writer = new MicrosoftContactBatchWriter(client);

            await writer.ApplyAsync(
                "user@example.org",
                new ContactChangeSet
                {
                    Deletes = new[]
                    {
                        Contact("directory-user-3") with
                        {
                            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [MicrosoftContactMapper.ContactIdMetadataKey] = "contact-3"
                            }
                        }
                    },
                    DeleteWritesDisabled = true
                },
                CancellationToken.None);

            Assert.Empty(client.Calls);
        }

        private static MeshContact Contact(string sourceId)
        {
            return new MeshContact
            {
                SourceId = sourceId,
                DisplayName = "Jane Doe",
                Emails = new[] { new ContactEmail("jane@example.org", "work", true) },
                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
            };
        }

        private sealed class FakeGraphContactClient : IMicrosoftGraphContactClient
        {
            public List<string> Calls { get; } = new();

            public Task<IReadOnlyList<MicrosoftGraphContact>> ListAsync(
                string userId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<MicrosoftGraphContact>>(Array.Empty<MicrosoftGraphContact>());
            }

            public Task CreateAsync(
                string userId,
                MicrosoftGraphContact contact,
                CancellationToken cancellationToken)
            {
                this.Calls.Add($"create:{userId}:{contact.SourceId}:{contact.Id}:{FormatCategories(contact)}");

                return Task.CompletedTask;
            }

            public Task UpdateAsync(
                string userId,
                MicrosoftGraphContact contact,
                CancellationToken cancellationToken)
            {
                this.Calls.Add($"update:{userId}:{contact.SourceId}:{contact.Id}:{FormatCategories(contact)}");

                return Task.CompletedTask;
            }

            public Task DeleteAsync(
                string userId,
                string contactId,
                string? contactFolderId,
                CancellationToken cancellationToken)
            {
                this.Calls.Add($"delete:{userId}:{contactFolderId}:{contactId}");

                return Task.CompletedTask;
            }

            private static string FormatCategories(MicrosoftGraphContact contact)
            {
                return string.Join(",", contact.Categories.Order(StringComparer.Ordinal));
            }
        }
    }
}
