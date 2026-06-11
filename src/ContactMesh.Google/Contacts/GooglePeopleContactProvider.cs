// File: GooglePeopleContactProvider.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Google.Contacts
{
    public sealed class GooglePeopleContactProvider : IContactProvider
    {
        private readonly IGooglePeopleContactClient? client;
        private readonly IGoogleContactGroupLabelClient? labelClient;
        private readonly GoogleContactBatchWriter writer;

        public GooglePeopleContactProvider(
            IGooglePeopleContactClient? client = null,
            IGoogleContactGroupLabelClient? labelClient = null,
            GoogleContactBatchWriter? writer = null)
        {
            this.client = client;
            this.labelClient = labelClient;
            this.writer = writer ?? new GoogleContactBatchWriter();
        }

        public async Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            if (this.client is null)
            {
                return Array.Empty<MeshContact>();
            }

            var contacts = await this.client.ListAsync(userId, cancellationToken).ConfigureAwait(false);
            var labelNamesByResourceName = await this.GetLabelNamesByResourceNameAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            return contacts
                .Select(contact => GoogleContactMapper.ToMeshContact(contact, labelNamesByResourceName))
                .ToList();
        }

        public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
        {
            return this.writer.ApplyAsync(userId, changes, cancellationToken);
        }

        private async Task<IReadOnlyDictionary<string, string>> GetLabelNamesByResourceNameAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            if (this.labelClient is null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var labels = await this.labelClient.ListAsync(userId, cancellationToken).ConfigureAwait(false);

            return labels
                .Where(label => !string.IsNullOrWhiteSpace(label.ResourceName))
                .ToDictionary(
                    label => label.ResourceName!,
                    label => label.ManagedLabelName,
                    StringComparer.OrdinalIgnoreCase);
        }
    }
}
