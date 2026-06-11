// File: RunAuditWriter.cs
// Author: Zunair
// Producer: Copilot

using System.Globalization;
using System.Text;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;

namespace ContactMesh.Core.Audit
{
    public sealed class RunAuditWriter
    {
        private static readonly string[] DetailHeaders = new[]
        {
            "Timestamp",
            "Provider",
            "RunId",
            "DryRun",
            "TargetUserId",
            "TargetUserEmail",
            "Operation",
            "Status",
            "SourceId",
            "DisplayName",
            "PrimaryEmail",
            "Labels",
            "LabelsRemoved",
            "ChangedFields",
            "SourceRule",
            "Reason"
        };

        private static readonly string[] SummaryHeaders = new[]
        {
            "RowType",
            "Provider",
            "RunId",
            "HostKind",
            "ConfigPath",
            "StartedAt",
            "CompletedAt",
            "DurationSeconds",
            "DryRun",
            "TargetUserId",
            "TargetUserEmail",
            "Outcome",
            "TargetCount",
            "CreateCount",
            "UpdateCount",
            "DeleteCount",
            "NoChangeCount",
            "WriteCount",
            "WarningCount",
            "ErrorCount",
            "FailureMessage",
            "Warnings",
            "Errors",
            "DetailCsvPath"
        };

        private readonly AuditLogOptions options;

        public RunAuditWriter(AuditLogOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            this.options = options;
        }

        public async Task<RunAuditArtifacts?> WriteAsync(
            ContactSyncRunResult? result,
            RunAuditContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!this.options.Enabled)
            {
                return null;
            }

            var directory = ResolveDirectory(this.options.Directory);
            Directory.CreateDirectory(directory);

            var baseName = BuildBaseName(context);
            var detailPath = Path.Combine(directory, baseName + "-detail.csv");
            var summaryPath = Path.Combine(directory, baseName + "-summary.csv");

            var hasDetailRows = HasDetailRows(result);
            var effectiveDetailPath = hasDetailRows ? detailPath : string.Empty;

            if (hasDetailRows)
            {
                await WriteDetailAsync(result, context, detailPath, cancellationToken).ConfigureAwait(false);
            }

            await WriteSummaryAsync(result, context, summaryPath, effectiveDetailPath, cancellationToken).ConfigureAwait(false);

            var detailInfo = hasDetailRows ? new FileInfo(detailPath) : null;
            var summaryInfo = new FileInfo(summaryPath);
            return new RunAuditArtifacts(
                effectiveDetailPath,
                summaryPath,
                detailInfo is { Exists: true } ? detailInfo.Length : 0,
                summaryInfo.Exists ? summaryInfo.Length : 0);
        }

        private bool HasDetailRows(ContactSyncRunResult? result)
        {
            if (result is null)
            {
                return false;
            }

            if (result.RunWarnings.Count > 0)
            {
                return true;
            }

            foreach (var target in result.Results)
            {
                if (target.Errors.Count > 0 || target.Warnings.Count > 0)
                {
                    return true;
                }

                foreach (var operation in target.Operations)
                {
                    if (!this.options.IncludeNoChange && operation.OperationType == SyncOperationType.NoChange)
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private static string ResolveDirectory(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = "logs/audit";
            }

            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(configured, Directory.GetCurrentDirectory());
        }

        private static string BuildBaseName(RunAuditContext context)
        {
            var provider = SanitizeForFileName(string.IsNullOrWhiteSpace(context.Provider) ? "unknown" : context.Provider);
            var stamp = context.StartedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var runId = SanitizeForFileName(context.RunId);
            return $"{provider}-{stamp}-{runId}";
        }

        private static string SanitizeForFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }

            return builder.ToString();
        }

        private async Task WriteDetailAsync(
            ContactSyncRunResult? result,
            RunAuditContext context,
            string path,
            CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            await writer.WriteLineAsync(JoinCsv(DetailHeaders)).ConfigureAwait(false);

            if (result is null)
            {
                return;
            }

            var dryRun = result.DryRun ? "true" : "false";
            foreach (var warning in result.RunWarnings)
            {
                await writer.WriteLineAsync(JoinCsv(BuildRunIssueRow(context, dryRun, "Warning", warning))).ConfigureAwait(false);
            }

            foreach (var target in result.Results)
            {
                foreach (var operation in target.Operations)
                {
                    if (!this.options.IncludeNoChange && operation.OperationType == SyncOperationType.NoChange)
                    {
                        continue;
                    }

                    var row = BuildDetailRow(context, dryRun, target, operation, this.options.IncludeDryRunPlannedAsWrites);
                    await writer.WriteLineAsync(JoinCsv(row)).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                foreach (var error in target.Errors)
                {
                    await writer.WriteLineAsync(JoinCsv(BuildTargetIssueRow(context, dryRun, target, "Error", error))).ConfigureAwait(false);
                }

                foreach (var warning in target.Warnings)
                {
                    await writer.WriteLineAsync(JoinCsv(BuildTargetIssueRow(context, dryRun, target, "Warning", warning))).ConfigureAwait(false);
                }
            }
        }

        private async Task WriteSummaryAsync(
            ContactSyncRunResult? result,
            RunAuditContext context,
            string path,
            string detailPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            await writer.WriteLineAsync(JoinCsv(SummaryHeaders)).ConfigureAwait(false);

            var outcome = DetermineOutcome(result, context);
            var duration = (context.CompletedAt - context.StartedAt).TotalSeconds;
            var summary = result?.Summary;
            var runWarnings = result is null ? Array.Empty<string>() : result.Warnings.ToArray();
            var runErrors = result is null ? Array.Empty<string>() : result.Errors.ToArray();
            var dryRun = (result?.DryRun ?? context.DryRun) ? "true" : "false";

            // Run-level aggregate row
            var runRow = new[]
            {
                "Run",
                context.Provider,
                context.RunId,
                context.HostKind ?? string.Empty,
                context.ConfigPath ?? string.Empty,
                context.StartedAt.ToString("o", CultureInfo.InvariantCulture),
                context.CompletedAt.ToString("o", CultureInfo.InvariantCulture),
                duration.ToString("F3", CultureInfo.InvariantCulture),
                dryRun,
                string.Empty,
                string.Empty,
                outcome,
                (summary?.TargetCount ?? 0).ToString(CultureInfo.InvariantCulture),
                (summary?.CreateCount ?? 0).ToString(CultureInfo.InvariantCulture),
                (summary?.UpdateCount ?? 0).ToString(CultureInfo.InvariantCulture),
                (summary?.DeleteCount ?? 0).ToString(CultureInfo.InvariantCulture),
                (summary?.NoChangeCount ?? 0).ToString(CultureInfo.InvariantCulture),
                (summary?.WriteCount ?? 0).ToString(CultureInfo.InvariantCulture),
                (summary?.WarningCount ?? 0).ToString(CultureInfo.InvariantCulture),
                (summary?.ErrorCount ?? 0).ToString(CultureInfo.InvariantCulture),
                context.Failure?.Message ?? string.Empty,
                string.Join(" | ", runWarnings),
                string.Join(" | ", runErrors),
                detailPath
            };

            await writer.WriteLineAsync(JoinCsv(runRow)).ConfigureAwait(false);

            // Per-target rows
            if (result is not null)
            {
                foreach (var target in result.Results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var targetOutcome = target.HasErrors ? "Failure" : "Success";
                    var targetRow = new[]
                    {
                        "Target",
                        context.Provider,
                        context.RunId,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        dryRun,
                        target.TargetUserId,
                        target.TargetUserEmail,
                        targetOutcome,
                        string.Empty,
                        target.CreateCount.ToString(CultureInfo.InvariantCulture),
                        target.UpdateCount.ToString(CultureInfo.InvariantCulture),
                        target.DeleteCount.ToString(CultureInfo.InvariantCulture),
                        target.NoChangeCount.ToString(CultureInfo.InvariantCulture),
                        target.WriteCount.ToString(CultureInfo.InvariantCulture),
                        target.WarningCount.ToString(CultureInfo.InvariantCulture),
                        target.ErrorCount.ToString(CultureInfo.InvariantCulture),
                        string.Empty,
                        string.Join(" | ", target.Warnings),
                        string.Join(" | ", target.Errors),
                        string.Empty
                    };

                    await writer.WriteLineAsync(JoinCsv(targetRow)).ConfigureAwait(false);
                }
            }
        }

        internal static string DetermineOutcome(ContactSyncRunResult? result, RunAuditContext context)
        {
            if (context.Failure is not null)
            {
                return "Failure";
            }

            if (result is null)
            {
                return "Failure";
            }

            return result.HasErrors ? "Failure" : "Success";
        }

        private static string[] BuildDetailRow(
            RunAuditContext context,
            string dryRun,
            SyncResult target,
            SyncOperation operation,
            bool includeDryRunPlannedAsWrites)
        {
            var status = ResolveStatus(target.DryRun, operation.OperationType, includeDryRunPlannedAsWrites);
            var sourceId = operation.DesiredContact.SourceId ?? operation.ExistingContact?.SourceId ?? string.Empty;
            var displayName = operation.DesiredContact.DisplayName ?? operation.ExistingContact?.DisplayName ?? string.Empty;
            var primaryEmail = ResolvePrimaryEmail(operation.DesiredContact)
                ?? ResolvePrimaryEmail(operation.ExistingContact)
                ?? string.Empty;
            var labels = string.Join(",", operation.DesiredContact.Labels.OrderBy(label => label, StringComparer.OrdinalIgnoreCase));
            var labelsRemoved = operation.ExistingContact is null
                ? string.Empty
                : string.Join(",", operation.ExistingContact.Labels
                    .Where(label => !operation.DesiredContact.Labels.Contains(label))
                    .OrderBy(label => label, StringComparer.OrdinalIgnoreCase));
            var changedFields = operation.ExistingContact is null
                ? string.Empty
                : string.Join(",", ComputeChangedFields(operation.DesiredContact, operation.ExistingContact));
            operation.DesiredContact.Metadata.TryGetValue("sourceRule", out var sourceRule);

            return new[]
            {
                DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                context.Provider,
                context.RunId,
                dryRun,
                target.TargetUserId,
                target.TargetUserEmail,
                operation.OperationType.ToString(),
                status,
                sourceId,
                displayName,
                primaryEmail,
                labels,
                labelsRemoved,
                changedFields,
                sourceRule ?? string.Empty,
                operation.Reason ?? string.Empty
            };
        }

        private static string[] BuildTargetIssueRow(
            RunAuditContext context,
            string dryRun,
            SyncResult target,
            string level,
            string message)
        {
            return new[]
            {
                DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                context.Provider,
                context.RunId,
                dryRun,
                target.TargetUserId,
                target.TargetUserEmail,
                level,
                level,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                message
            };
        }

        private static string[] BuildRunIssueRow(
            RunAuditContext context,
            string dryRun,
            string level,
            string message)
        {
            return new[]
            {
                DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                context.Provider,
                context.RunId,
                dryRun,
                string.Empty,
                string.Empty,
                level,
                level,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                message
            };
        }

        private static string ResolveStatus(bool dryRun, SyncOperationType operationType, bool includeDryRunPlannedAsWrites)
        {
            if (operationType == SyncOperationType.NoChange)
            {
                return "NoChange";
            }

            if (!dryRun || includeDryRunPlannedAsWrites)
            {
                return "Written";
            }

            return "Planned";
        }

        private static string? ResolvePrimaryEmail(MeshContact? contact)
        {
            if (contact is null)
            {
                return null;
            }

            return contact.Emails.FirstOrDefault(email => email.IsPrimary)?.Address
                ?? contact.Emails.FirstOrDefault()?.Address;
        }

        private static IEnumerable<string> ComputeChangedFields(MeshContact desired, MeshContact existing)
        {
            if (!string.Equals(desired.DisplayName, existing.DisplayName, StringComparison.Ordinal))
                yield return "DisplayName";
            if (!string.Equals(desired.GivenName, existing.GivenName, StringComparison.Ordinal))
                yield return "GivenName";
            if (!string.Equals(desired.FamilyName, existing.FamilyName, StringComparison.Ordinal))
                yield return "FamilyName";
            if (!string.Equals(desired.CompanyName, existing.CompanyName, StringComparison.Ordinal))
                yield return "CompanyName";
            if (!string.Equals(desired.Department, existing.Department, StringComparison.Ordinal))
                yield return "Department";
            if (!string.Equals(desired.JobTitle, existing.JobTitle, StringComparison.Ordinal))
                yield return "JobTitle";
            if (!string.Equals(desired.Notes, existing.Notes, StringComparison.Ordinal))
                yield return "Notes";
            if (!EmailsEqual(desired.Emails, existing.Emails))
                yield return "Emails";
            if (!PhonesEqual(desired.Phones, existing.Phones))
                yield return "Phones";
        }

        private static bool EmailsEqual(IReadOnlyList<ContactEmail> a, IReadOnlyList<ContactEmail> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i].Address, b[i].Address, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.Equals(a[i].Type, b[i].Type, StringComparison.OrdinalIgnoreCase)) return false;
                if (a[i].IsPrimary != b[i].IsPrimary) return false;
            }

            return true;
        }

        private static bool PhonesEqual(IReadOnlyList<ContactPhone> a, IReadOnlyList<ContactPhone> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i].Number, b[i].Number, StringComparison.Ordinal)) return false;
                if (!string.Equals(a[i].Type, b[i].Type, StringComparison.OrdinalIgnoreCase)) return false;
                if (a[i].IsPrimary != b[i].IsPrimary) return false;
            }

            return true;
        }

        private static string JoinCsv(IReadOnlyList<string> fields)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(EscapeCsv(fields[i]));
            }

            return builder.ToString();
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            var sanitized = value.Replace("\"", "\"\"", StringComparison.Ordinal);
            return needsQuotes ? $"\"{sanitized}\"" : sanitized;
        }
    }
}
