// File: MicrosoftContactBatchWriter.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Contacts
{
    public sealed class MicrosoftContactBatchWriter
    {
        private readonly IMicrosoftGraphContactClient? client;

        public MicrosoftContactBatchWriter(IMicrosoftGraphContactClient? client = null)
        {
            this.client = client;
        }

        public async Task ApplyAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentNullException.ThrowIfNull(changes);

            if (this.client is null)
            {
                return;
            }

            foreach (var contact in changes.Creates)
            {
                await this.client.CreateAsync(
                    userId,
                    MicrosoftContactMapper.ToMicrosoftGraphContact(contact),
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var contact in changes.Updates)
            {
                var graphContact = MicrosoftContactMapper.ToMicrosoftGraphContact(contact);
                if (!string.IsNullOrWhiteSpace(graphContact.Id))
                {
                    await this.client.UpdateAsync(userId, graphContact, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var contact in changes.DeleteWritesDisabled ? Array.Empty<MeshContact>() : changes.Deletes)
            {
                var graphContact = MicrosoftContactMapper.ToMicrosoftGraphContact(contact);
                if (!string.IsNullOrWhiteSpace(graphContact.Id)
                    && contact.Metadata.TryGetValue(MicrosoftContactMapper.ContactIdMetadataKey, out var contactId)
                    && !string.IsNullOrWhiteSpace(contactId))
                {
                    await this.client.DeleteAsync(
                        userId,
                        contactId,
                        graphContact.ContactFolderId,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
