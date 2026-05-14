using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class SyncExecutor
{
    private readonly IContactProvider contactProvider;

    public SyncExecutor(IContactProvider contactProvider)
    {
        this.contactProvider = contactProvider;
    }

    public async Task<SyncResult> ExecuteAsync(SyncTarget target, IReadOnlyList<SyncOperation> operations, bool dryRun, CancellationToken cancellationToken)
    {
        if (!dryRun)
        {
            await this.contactProvider.ApplyChangesAsync(target.UserId, ContactChangeSet.FromOperations(operations), cancellationToken).ConfigureAwait(false);
        }

        return new SyncResult
        {
            TargetUserId = target.UserId,
            DryRun = dryRun,
            Operations = operations
        };
    }
}
