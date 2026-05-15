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
        var createCount = operations.Count(o => o.OperationType == SyncOperationType.Create);
        var updateCount = operations.Count(o => o.OperationType == SyncOperationType.Update);
        var deleteCount = operations.Count(o => o.OperationType == SyncOperationType.Delete);
        var changeCount = createCount + updateCount + deleteCount;
        var entries = new List<SyncLogEntry>
        {
            new(
                now,
                SyncLogLevel.Information,
                $"Planned {operations.Count} operation(s) for {target.UserId}: {createCount} create, {updateCount} update, {deleteCount} delete, {changeCount} write(s).",
                target.UserId)
        };

        if (dryRun)
        {
            entries.Add(new SyncLogEntry(
                now,
                SyncLogLevel.Information,
                "Dry run enabled; provider writes were skipped.",
                target.UserId));
        }

        foreach (var operation in operations.Where(operation => operation.OperationType is not SyncOperationType.NoChange))
        {
            entries.Add(new SyncLogEntry(
                now,
                SyncLogLevel.Information,
                $"{(dryRun ? "Dry-run" : "Applied")} {FormatOperationType(operation.OperationType)} {DescribeContact(operation)}.",
                target.UserId,
                operation.OperationType,
                operation.DesiredContact.SourceId ?? operation.ExistingContact?.SourceId,
                operation.Reason));
        }

        return entries;
    }

    private static string FormatOperationType(SyncOperationType operationType)
    {
        return operationType.ToString().ToLowerInvariant();
    }

    private static string DescribeContact(SyncOperation operation)
    {
        return GetIdentity(operation.DesiredContact)
            ?? (operation.ExistingContact is null ? null : GetIdentity(operation.ExistingContact))
            ?? "(unknown contact)";
    }

    private static string? GetIdentity(MeshContact contact)
    {
        return new[]
            {
                contact.DisplayName,
                contact.SourceId,
                contact.Emails.FirstOrDefault(email => email.IsPrimary)?.Address,
                contact.Emails.FirstOrDefault()?.Address
            }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
