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
            lines.Add($"  Dry-run {FormatOperationType(operation.OperationType)}: {DescribeContact(operation.DesiredContact)}{FormatReason(operation)}");
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

    private static string DescribeContact(MeshContact contact)
    {
        var identity = new[]
            {
                contact.DisplayName,
                contact.SourceId,
                contact.Emails.FirstOrDefault(email => email.IsPrimary)?.Address,
                contact.Emails.FirstOrDefault()?.Address
            }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "(unknown contact)";

        return string.IsNullOrWhiteSpace(contact.SourceId)
            || string.Equals(identity, contact.SourceId, StringComparison.OrdinalIgnoreCase)
            ? identity
            : $"{identity} [{contact.SourceId}]";
    }

    private static string FormatReason(SyncOperation operation)
    {
        return string.IsNullOrWhiteSpace(operation.Reason)
            ? string.Empty
            : $" - {operation.Reason}";
    }
}
