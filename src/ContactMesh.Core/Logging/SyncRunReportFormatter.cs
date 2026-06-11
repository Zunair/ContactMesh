// File: SyncRunReportFormatter.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;

namespace ContactMesh.Core.Logging
{
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

            foreach (var warning in result.RunWarnings)
            {
                lines.Add($"Warning: {warning}");
            }

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
                AddOperationDetailLines(lines, operation);
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

        private static void AddOperationDetailLines(List<string> lines, SyncOperation operation)
        {
            if (operation.OperationType == SyncOperationType.Update && operation.ExistingContact is not null)
            {
                var changedFields = GetChangedFields(operation.DesiredContact, operation.ExistingContact);
                if (changedFields.Count > 0)
                {
                    lines.Add($"    Changed: {string.Join(", ", changedFields)}");
                }
            }

            var labels = operation.DesiredContact.Labels
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (labels.Count > 0)
            {
                lines.Add($"    Labels: {string.Join(", ", labels)}");
            }

            if (operation.ExistingContact is not null)
            {
                var removedLabels = operation.ExistingContact.Labels
                    .Where(label => !string.IsNullOrWhiteSpace(label)
                        && !operation.DesiredContact.Labels.Contains(label))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (removedLabels.Count > 0)
                {
                    lines.Add($"    Labels removed: {string.Join(", ", removedLabels)}");
                }
            }

            var source = FormatSource(operation.DesiredContact);
            if (!string.IsNullOrWhiteSpace(source))
            {
                lines.Add($"    Source: {source}");
            }
        }

        private static string? FormatSource(MeshContact contact)
        {
            if (!contact.Metadata.TryGetValue("sourceRule", out var sourceRule)
                || string.IsNullOrWhiteSpace(sourceRule))
            {
                return null;
            }

            return sourceRule switch
            {
                "Directory" => "Directory user contact.",
                "VisibleGroupContact" => $"Visible group contact from {FormatSourceGroup(contact.Metadata)}.",
                "VisibleGroupMember" => $"Visible group member contact from {FormatSourceGroup(contact.Metadata)}.",
                "GroupsToSyncByGroup" => $"GroupsToSyncByGroup contact from {FormatSourceGroup(contact.Metadata)}.",
                _ => sourceRule
            };
        }

        private static string FormatSourceGroup(IDictionary<string, string> metadata)
        {
            metadata.TryGetValue("sourceGroupDisplayName", out var displayName);
            metadata.TryGetValue("sourceGroupEmail", out var email);
            metadata.TryGetValue("sourceGroupId", out var id);

            var identity = new[]
                {
                    displayName,
                    email,
                    id
                }
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? "(unknown group)";

            var details = new[]
                {
                    string.IsNullOrWhiteSpace(email) || string.Equals(identity, email, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : $"email={email}",
                    string.IsNullOrWhiteSpace(id) || string.Equals(identity, id, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : $"id={id}"
                }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            return details.Count == 0
                ? identity
                : $"{identity} ({string.Join(", ", details)})";
        }

        private static IReadOnlyList<string> GetChangedFields(MeshContact desired, MeshContact existing)
        {
            var fields = new List<string>();

            if (!string.Equals(desired.DisplayName, existing.DisplayName, StringComparison.Ordinal))
                fields.Add("DisplayName");
            if (!string.Equals(desired.GivenName, existing.GivenName, StringComparison.Ordinal))
                fields.Add("GivenName");
            if (!string.Equals(desired.FamilyName, existing.FamilyName, StringComparison.Ordinal))
                fields.Add("FamilyName");
            if (!string.Equals(desired.CompanyName, existing.CompanyName, StringComparison.Ordinal))
                fields.Add("CompanyName");
            if (!string.Equals(desired.Department, existing.Department, StringComparison.Ordinal))
                fields.Add("Department");
            if (!string.Equals(desired.JobTitle, existing.JobTitle, StringComparison.Ordinal))
                fields.Add("JobTitle");
            if (!EmailsEqual(desired.Emails, existing.Emails))
                fields.Add("Emails");
            if (!PhonesEqual(desired.Phones, existing.Phones))
                fields.Add("Phones");
            if (!desired.Labels.SetEquals(existing.Labels))
                fields.Add("Labels");
            if (!MetadataEqual(desired.Metadata, existing.Metadata))
                fields.Add("Metadata");

            return fields;
        }

        private static bool EmailsEqual(IReadOnlyList<ContactEmail> left, IReadOnlyList<ContactEmail> right)
        {
            return left.Count == right.Count
                && left.Zip(right).All(pair =>
                    string.Equals(pair.First.Address, pair.Second.Address, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pair.First.Type, pair.Second.Type, StringComparison.Ordinal)
                    && pair.First.IsPrimary == pair.Second.IsPrimary);
        }

        private static bool PhonesEqual(IReadOnlyList<ContactPhone> left, IReadOnlyList<ContactPhone> right)
        {
            if (left.Count != right.Count) return false;
            var sortedLeft = left.OrderBy(p => p.Type, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Number, StringComparer.OrdinalIgnoreCase);
            var sortedRight = right.OrderBy(p => p.Type, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Number, StringComparer.OrdinalIgnoreCase);
            return sortedLeft.Zip(sortedRight).All(pair =>
                string.Equals(pair.First.Number, pair.Second.Number, StringComparison.Ordinal)
                && string.Equals(pair.First.Type, pair.Second.Type, StringComparison.Ordinal)
                && pair.First.IsPrimary == pair.Second.IsPrimary);
        }

        private static bool MetadataEqual(IDictionary<string, string> left, IDictionary<string, string> right)
        {
            // Mirror the SyncPlanner semantics: only keys present in `right` (existing) are compared.
            return right.All(item => left.TryGetValue(item.Key, out var leftValue)
                && string.Equals(item.Value, leftValue, StringComparison.Ordinal));
        }
    }
}
