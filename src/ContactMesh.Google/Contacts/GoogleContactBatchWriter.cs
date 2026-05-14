using ContactMesh.Core.Models;

namespace ContactMesh.Google.Contacts;

public sealed class GoogleContactBatchWriter
{
    public const string DefaultAppId = "contact-mesh";

    private readonly string appId;
    private readonly IGoogleContactGroupLabelClient? labelClient;
    private readonly GoogleContactGroupLabelReconciler labelReconciler;

    public GoogleContactBatchWriter(
        string appId = DefaultAppId,
        IGoogleContactGroupLabelClient? labelClient = null,
        GoogleContactGroupLabelReconciler? labelReconciler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        this.appId = appId;
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

        foreach (var staleLabel in plan.LabelsToDelete)
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
