// File: MicrosoftContactProvider.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Contacts
{
    public sealed class MicrosoftContactProvider : IContactProvider
    {
        private readonly IMicrosoftGraphContactClient? client;
        private readonly MicrosoftContactBatchWriter writer;

        public MicrosoftContactProvider(
            IMicrosoftGraphContactClient? client = null,
            MicrosoftContactBatchWriter? writer = null)
        {
            this.client = client;
            this.writer = writer ?? new MicrosoftContactBatchWriter();
        }

        public async Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            if (this.client is null)
            {
                return Array.Empty<MeshContact>();
            }

            var contacts = await this.client.ListAsync(userId, cancellationToken).ConfigureAwait(false);

            return contacts
                .Select(MicrosoftContactMapper.ToMeshContact)
                .ToList();
        }

        public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
        {
            return this.writer.ApplyAsync(userId, changes, cancellationToken);
        }
    }
}
