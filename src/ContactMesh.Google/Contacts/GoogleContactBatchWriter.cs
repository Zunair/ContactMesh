// File: GoogleContactBatchWriter.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Google.Contacts
{
    public sealed class GoogleContactBatchWriter
    {
        public const string DefaultAppId = "contact-mesh";

        private readonly string appId;
        private readonly IGooglePeopleContactClient? contactClient;
        private readonly IGoogleContactGroupLabelClient? labelClient;
        private readonly GoogleContactGroupLabelReconciler labelReconciler;

        public GoogleContactBatchWriter(
            string appId = DefaultAppId,
            IGooglePeopleContactClient? contactClient = null,
            IGoogleContactGroupLabelClient? labelClient = null,
            GoogleContactGroupLabelReconciler? labelReconciler = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appId);

            this.appId = appId;
            this.contactClient = contactClient;
            this.labelClient = labelClient;
            this.labelReconciler = labelReconciler ?? new GoogleContactGroupLabelReconciler();
        }

        public async Task ApplyAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentNullException.ThrowIfNull(changes);

            if (this.labelClient is not null)
            {
                await this.ReconcileContactGroupLabelsAsync(userId, changes, cancellationToken).ConfigureAwait(false);
            }

            if (this.contactClient is not null)
            {
                var labelResourceNamesByName = await this.GetLabelResourceNamesByNameAsync(userId, cancellationToken)
                    .ConfigureAwait(false);

                await this.ApplyPeopleContactChangesAsync(
                    userId,
                    changes,
                    labelResourceNamesByName,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ApplyPeopleContactChangesAsync(
            string userId,
            ContactChangeSet changes,
            IReadOnlyDictionary<string, string> labelResourceNamesByName,
            CancellationToken cancellationToken)
        {
            foreach (var contact in changes.Creates)
            {
                await this.contactClient!.CreateAsync(
                    userId,
                    GoogleContactMapper.ToGooglePersonContact(contact, labelResourceNamesByName),
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var contact in changes.Updates)
            {
                var googleContact = GoogleContactMapper.ToGooglePersonContact(contact, labelResourceNamesByName);
                if (!string.IsNullOrWhiteSpace(googleContact.ResourceName))
                {
                    await this.contactClient!.UpdateAsync(userId, googleContact, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var contact in changes.DeleteWritesDisabled ? Array.Empty<MeshContact>() : changes.Deletes)
            {
                if (contact.Metadata.TryGetValue(GoogleContactMapper.ResourceNameMetadataKey, out var resourceName)
                    && !string.IsNullOrWhiteSpace(resourceName))
                {
                    await this.contactClient!.DeleteAsync(userId, resourceName, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task<IReadOnlyDictionary<string, string>> GetLabelResourceNamesByNameAsync(
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
                .GroupBy(label => label.ManagedLabelName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().ResourceName!,
                    StringComparer.OrdinalIgnoreCase);
        }

        private async Task ReconcileContactGroupLabelsAsync(
            string userId,
            ContactChangeSet changes,
            CancellationToken cancellationToken)
        {
            var desiredLabels = changes.Creates
                .Concat(changes.Updates)
                .SelectMany(contact => contact.Labels)
                .ToList();

            var existingLabels = await this.labelClient!.ListAsync(userId, cancellationToken).ConfigureAwait(false);
            var plan = this.labelReconciler.CreatePlan(this.appId, desiredLabels, existingLabels);

            foreach (var staleLabel in changes.DeleteWritesDisabled ? Array.Empty<GoogleContactGroupLabel>() : plan.LabelsToDelete)
            {
                if (!string.IsNullOrWhiteSpace(staleLabel.ResourceName))
                {
                    await this.labelClient.DeleteAsync(userId, staleLabel.ResourceName, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var update in plan.LabelsToUpdate)
            {
                if (!string.IsNullOrWhiteSpace(update.ExistingLabel.ResourceName))
                {
                    await this.labelClient.UpdateAsync(
                        userId,
                        update.ExistingLabel.ResourceName,
                        update.DesiredName,
                        GoogleContactGroupLabelReconciler.CreateClientData(this.appId, update.DesiredName),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var labelName in plan.LabelsToCreate)
            {
                await this.labelClient.CreateAsync(
                    userId,
                    labelName,
                    GoogleContactGroupLabelReconciler.CreateClientData(this.appId, labelName),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
