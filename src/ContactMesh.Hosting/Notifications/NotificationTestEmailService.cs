// File: NotificationTestEmailService.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Core.Notifications;
using ContactMesh.Microsoft365.Auth;

namespace ContactMesh.Hosting.Notifications
{
    public sealed class NotificationTestEmailService
    {
        private readonly HttpClient httpClient;

        public NotificationTestEmailService(HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            this.httpClient = httpClient;
        }

        public async Task<RunNotificationResult> SendAsync(
            ContactMeshOptions contactMesh,
            Microsoft365Options microsoft365,
            string? configPath,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contactMesh);
            ArgumentNullException.ThrowIfNull(microsoft365);

            var options = contactMesh.Notifications;
            if (!options.Enabled)
            {
                return new RunNotificationResult(RunNotificationOutcome.Skipped, "Notifications disabled.");
            }

            var sender = ContactMeshHostFactory.CreateNotificationSender(contactMesh, microsoft365, this.httpClient);
            if (sender is null)
            {
                return new RunNotificationResult(RunNotificationOutcome.Skipped, "No notification sender configured for the selected provider.");
            }

            if (string.IsNullOrWhiteSpace(options.From))
            {
                return new RunNotificationResult(RunNotificationOutcome.Skipped, "Notifications:From is not configured.");
            }

            var recipients = options.SuccessTo
                .Concat(options.FailureTo)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (recipients.Length == 0)
            {
                return new RunNotificationResult(RunNotificationOutcome.Skipped, "Notifications has no success or failure recipients.");
            }

            var prefix = string.IsNullOrWhiteSpace(options.SubjectPrefix)
                ? string.Empty
                : options.SubjectPrefix.Trim() + " ";
            var message = new NotificationMessage(
                options.From.Trim(),
                recipients,
                $"{prefix}Test notification",
                BuildBody(contactMesh.Provider, configPath),
                Array.Empty<NotificationAttachment>());

            await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return new RunNotificationResult(RunNotificationOutcome.Sent, $"Test notification sent to {recipients.Length} recipient(s).");
        }

        private static string BuildBody(string provider, string? configPath)
        {
            return string.Join(
                Environment.NewLine,
                "This is a ContactMesh notification test from the settings dashboard.",
                $"Provider: {provider}",
                $"Config: {(string.IsNullOrWhiteSpace(configPath) ? "Not set" : configPath)}",
                $"Sent: {DateTimeOffset.UtcNow:o}");
        }
    }
}
