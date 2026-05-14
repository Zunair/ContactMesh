using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Logging;
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
            Operations = operations,
            LogEntries = CreateLogEntries(target, operations, dryRun)
        };
    }

    private static IReadOnlyList<SyncLogEntry> CreateLogEntries(
        SyncTarget target,
        IReadOnlyList<SyncOperation> operations,
        bool dryRun)
    {
        var now = DateTimeOffset.UtcNow;
        var changeCount = operations.Count(o => o.OperationType is not SyncOperationType.NoChange);
        var entries = new List<SyncLogEntry>
        {
            new(now, "Information", $"Planned {operations.Count} operation(s) for {target.UserId}; {changeCount} write(s).")
        };

        if (dryRun)
        {
            entries.Add(new SyncLogEntry(now, "Information", "Dry run enabled; provider writes were skipped."));
        }

        return entries;
    }
}
