using System.Globalization;
using System.Text;
using ContactMesh.Core.Audit;
using ContactMesh.Core.Logging;
using ContactMesh.Core.Sync;

namespace ContactMesh.Core.Notifications;

public enum RunNotificationOutcome
{
    Skipped,
    Sent
}

public sealed record RunNotificationResult(RunNotificationOutcome Outcome, string? Reason = null);

public sealed class RunNotificationDispatcher
{
    private readonly NotificationOptions options;
    private readonly IRunNotificationSender? sender;

    public RunNotificationDispatcher(NotificationOptions options, IRunNotificationSender? sender)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
        this.sender = sender;
    }

    public async Task<RunNotificationResult> DispatchAsync(
        ContactSyncRunResult? result,
        RunAuditContext context,
        RunAuditArtifacts? artifacts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!this.options.Enabled)
        {
            return new RunNotificationResult(RunNotificationOutcome.Skipped, "Notifications disabled.");
        }

        if (result?.DryRun == true || (result is null && context.DryRun))
        {
            return new RunNotificationResult(RunNotificationOutcome.Skipped, "Dry-run; email not sent.");
        }

        if (this.sender is null)
        {
            return new RunNotificationResult(RunNotificationOutcome.Skipped, "No notification sender configured.");
        }

        if (string.IsNullOrWhiteSpace(this.options.From))
        {
            return new RunNotificationResult(RunNotificationOutcome.Skipped, "Notifications:From is not configured.");
        }

        var notificationStatus = ResolveNotificationStatus(result, context);
        var useFailureRecipients = notificationStatus is "Failure" or "Warning";
        var recipients = (useFailureRecipients ? this.options.FailureTo : this.options.SuccessTo)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();

        if (recipients.Count == 0)
        {
            return new RunNotificationResult(
                RunNotificationOutcome.Skipped,
                useFailureRecipients ? "Notifications:FailureTo has no recipients." : "Notifications:SuccessTo has no recipients.");
        }

        var subject = BuildSubject(result, context, notificationStatus);
        var body = BuildBody(result, context, artifacts, notificationStatus);
        var attachments = BuildAttachments(artifacts, notificationStatus == "Failure");

        var message = new NotificationMessage(this.options.From, recipients, subject, body, attachments);
        await this.sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        return new RunNotificationResult(RunNotificationOutcome.Sent);
    }

    private static string ResolveNotificationStatus(ContactSyncRunResult? result, RunAuditContext context)
    {
        if (RunAuditWriter.DetermineOutcome(result, context) == "Failure")
        {
            return "Failure";
        }

        return result?.HasWarnings == true ? "Warning" : "Success";
    }

    private string BuildSubject(ContactSyncRunResult? result, RunAuditContext context, string status)
    {
        var prefix = string.IsNullOrWhiteSpace(this.options.SubjectPrefix)
            ? string.Empty
            : this.options.SubjectPrefix.Trim() + " ";
        var subjectStatus = status switch
        {
            "Failure" => "FAILED",
            "Warning" => "Warning",
            _ => "Success"
        };
        var summary = result?.Summary;
        var counts = summary is null
            ? string.Empty
            : $" — {summary.CreateCount}C/{summary.UpdateCount}U/{summary.DeleteCount}D over {summary.TargetCount} targets";

        return $"{prefix}{context.Provider} sync {subjectStatus}{counts}";
    }

    private static string BuildBody(
        ContactSyncRunResult? result,
        RunAuditContext context,
        RunAuditArtifacts? artifacts,
        string status)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Provider: {context.Provider}");
        builder.AppendLine($"Run id: {context.RunId}");
        if (!string.IsNullOrWhiteSpace(context.HostKind))
        {
            builder.AppendLine($"Host: {context.HostKind}");
        }
        if (!string.IsNullOrWhiteSpace(context.ConfigPath))
        {
            builder.AppendLine($"Config: {context.ConfigPath}");
        }
        builder.AppendLine($"Started: {context.StartedAt.ToString("o", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Completed: {context.CompletedAt.ToString("o", CultureInfo.InvariantCulture)}");
        var duration = (context.CompletedAt - context.StartedAt).TotalSeconds;
        builder.AppendLine($"Duration: {duration.ToString("F3", CultureInfo.InvariantCulture)} seconds");
        builder.AppendLine($"Outcome: {status}");

        if (context.Failure is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Run-level exception:");
            builder.AppendLine(context.Failure.ToString());
        }

        if (result is not null)
        {
            builder.AppendLine();
            foreach (var line in SyncRunReportFormatter.Format(result))
            {
                builder.AppendLine(line);
            }
        }

        if (artifacts is not null)
        {
            builder.AppendLine();
            if (!string.IsNullOrEmpty(artifacts.DetailCsvPath))
            {
                builder.AppendLine($"Audit CSV (detail): {artifacts.DetailCsvPath} ({artifacts.DetailCsvBytes} bytes)");
            }
            builder.AppendLine($"Audit CSV (summary): {artifacts.SummaryCsvPath} ({artifacts.SummaryCsvBytes} bytes)");
        }

        return builder.ToString();
    }

    private IReadOnlyList<NotificationAttachment> BuildAttachments(RunAuditArtifacts? artifacts, bool isFailure)
    {
        if (artifacts is null || !isFailure || !this.options.AttachCsvOnFailure)
        {
            return Array.Empty<NotificationAttachment>();
        }

        var attachments = new List<NotificationAttachment>(2);
        TryAttach(attachments, artifacts.SummaryCsvPath);
        TryAttach(attachments, artifacts.DetailCsvPath);
        return attachments;
    }

    private void TryAttach(ICollection<NotificationAttachment> attachments, string path)
    {
        if (!File.Exists(path)) return;

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > this.options.MaxAttachmentBytes && this.options.MaxAttachmentBytes > 0)
        {
            // Truncate; add a footer note so recipient sees it was capped.
            var notice = Encoding.UTF8.GetBytes($"{Environment.NewLine}# truncated to {this.options.MaxAttachmentBytes} bytes{Environment.NewLine}");
            var capped = new byte[this.options.MaxAttachmentBytes + notice.Length];
            Buffer.BlockCopy(bytes, 0, capped, 0, this.options.MaxAttachmentBytes);
            Buffer.BlockCopy(notice, 0, capped, this.options.MaxAttachmentBytes, notice.Length);
            bytes = capped;
        }

        attachments.Add(new NotificationAttachment(Path.GetFileName(path), "text/csv", bytes));
    }
}
