using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;

namespace ContactMesh.Core.Logging;

public static class SyncRunReportFormatter
{
    public static IReadOnlyList<string> Format(ContactSyncRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var summary = result.Summary;
        var lines = new List<string>
        {
            $"Targets: {summary.TargetCount}",
            $"Plan: {summary.CreateCount} create, {summary.UpdateCount} update, {summary.DeleteCount} delete, {summary.NoChangeCount} unchanged.",
            $"Writes: {summary.WriteCount}",
            $"Dry run: {FormatDryRun(summary)}",
            $"Warnings: {summary.WarningCount}",
            $"Errors: {summary.ErrorCount}"
        };

        foreach (var syncResult in result.Results)
        {
            AddTargetLines(lines, syncResult, result.DryRun);
        }

        return lines;
    }

    private static void AddTargetLines(List<string> lines, SyncResult result, bool dryRun)
    {
        lines.Add(
            $"Target {result.TargetUserId}: {result.CreateCount} create, {result.UpdateCount} update, {result.DeleteCount} delete, {result.NoChangeCount} unchanged.");

        foreach (var warning in result.Warnings)
        {
            lines.Add($"  Warning: {warning}");
        }

        foreach (var error in result.Errors)
        {
            lines.Add($"  Error: {error}");
        }

        if (!dryRun)
        {
            return;
        }

        foreach (var operation in result.Operations.Where(operation => operation.OperationType is not SyncOperationType.NoChange))
        {
            lines.Add($"  Dry-run {FormatOperationType(operation.OperationType)}: {DescribeContact(operation)}{FormatReason(operation)}");
        }
    }

    private static string FormatDryRun(SyncRunSummary summary)
    {
        return summary.DryRun
            ? $"true (provider writes skipped: {summary.WriteCount})"
            : "false";
    }

    private static string FormatOperationType(SyncOperationType operationType)
    {
        return operationType.ToString().ToLowerInvariant();
    }

    private static string DescribeContact(SyncOperation operation)
    {
        var identity = GetIdentity(operation.DesiredContact)
            ?? (operation.ExistingContact is null ? null : GetIdentity(operation.ExistingContact))
            ?? "(unknown contact)";

        var sourceId = operation.DesiredContact.SourceId ?? operation.ExistingContact?.SourceId;

        return string.IsNullOrWhiteSpace(sourceId)
            || string.Equals(identity, sourceId, StringComparison.OrdinalIgnoreCase)
            ? identity
            : $"{identity} [{sourceId}]";
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

    private static string FormatReason(SyncOperation operation)
    {
        return string.IsNullOrWhiteSpace(operation.Reason)
            ? string.Empty
            : $" - {operation.Reason}";
    }
}
