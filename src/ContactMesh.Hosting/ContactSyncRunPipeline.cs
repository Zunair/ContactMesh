using ContactMesh.Core.Audit;
using ContactMesh.Core.Logging;
using ContactMesh.Core.Models;
using ContactMesh.Core.Notifications;
using ContactMesh.Core.Sync;

namespace ContactMesh.Hosting;

public sealed class ContactSyncRunPipeline
{
    private readonly ContactSyncOrchestrator orchestrator;
    private readonly RunAuditWriter? auditWriter;
    private readonly RunNotificationDispatcher? notificationDispatcher;

    public ContactSyncRunPipeline(
        ContactSyncOrchestrator orchestrator,
        RunAuditWriter? auditWriter,
        RunNotificationDispatcher? notificationDispatcher)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        this.orchestrator = orchestrator;
        this.auditWriter = auditWriter;
        this.notificationDispatcher = notificationDispatcher;
    }

    public async Task<ContactSyncRunResult?> RunAsync(
        ContactMeshOptions options,
        TextWriter output,
        string hostKind,
        string? configPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        var startedAt = DateTimeOffset.UtcNow;
        ContactSyncRunResult? result = null;
        Exception? failure = null;

        try
        {
            result = await this.orchestrator.RunAsync(
                options,
                cancellationToken,
                async (progress, _) =>
                {
                    await output.WriteLineAsync(SyncProgressFormatter.Format(progress)).ConfigureAwait(false);
                }).ConfigureAwait(false);

            foreach (var line in SyncRunReportFormatter.Format(result))
            {
                await output.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failure = ex;
            await output.WriteLineAsync($"Run failed: {ex.Message}").ConfigureAwait(false);
        }

        var context = new RunAuditContext
        {
            Provider = options.Provider,
            RunId = RunAuditContext.NewRunId(startedAt),
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            DryRun = options.DryRun,
            ConfigPath = configPath,
            HostKind = hostKind,
            Failure = failure
        };

        RunAuditArtifacts? artifacts = null;
        if (this.auditWriter is not null)
        {
            try
            {
                artifacts = await this.auditWriter.WriteAsync(result, context, cancellationToken).ConfigureAwait(false);
                if (artifacts is not null)
                {
                    await output.WriteLineAsync($"Audit detail: {artifacts.DetailCsvPath}").ConfigureAwait(false);
                    await output.WriteLineAsync($"Audit summary: {artifacts.SummaryCsvPath}").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await output.WriteLineAsync($"Audit log write failed: {ex.Message}").ConfigureAwait(false);
            }
        }

        if (this.notificationDispatcher is not null)
        {
            try
            {
                var notification = await this.notificationDispatcher
                    .DispatchAsync(result, context, artifacts, cancellationToken)
                    .ConfigureAwait(false);
                if (notification.Outcome == RunNotificationOutcome.Sent)
                {
                    await output.WriteLineAsync("Notification email sent.").ConfigureAwait(false);
                }
                else if (!string.IsNullOrWhiteSpace(notification.Reason))
                {
                    await output.WriteLineAsync($"Notification skipped: {notification.Reason}").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await output.WriteLineAsync($"Notification dispatch failed: {ex.Message}").ConfigureAwait(false);
            }
        }

        if (failure is not null)
        {
            throw failure;
        }

        return result;
    }
}
