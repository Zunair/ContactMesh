// File: RunNotificationDispatcherTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Audit;
using ContactMesh.Core.Models;
using ContactMesh.Core.Notifications;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class RunNotificationDispatcherTests
    {
        [Fact]
        public async Task Dispatcher_Skips_When_Dry_Run()
        {
            var sender = new RecordingSender();
            var dispatcher = new RunNotificationDispatcher(
                new NotificationOptions
                {
                    Enabled = true,
                    From = "ops@example.com",
                    SuccessTo = new[] { "team@example.com" }
                },
                sender);

            var result = await dispatcher.DispatchAsync(
                new ContactSyncRunResult { DryRun = true },
                NewContext(),
                artifacts: null,
                TestContext.Current.CancellationToken);

            Assert.Equal(RunNotificationOutcome.Skipped, result.Outcome);
            Assert.Contains("Dry-run", result.Reason);
            Assert.Empty(sender.Messages);
        }

        [Fact]
        public async Task Dispatcher_Skips_When_Dry_Run_Fails_Before_Result()
        {
            var sender = new RecordingSender();
            var dispatcher = new RunNotificationDispatcher(
                new NotificationOptions
                {
                    Enabled = true,
                    From = "ops@example.com",
                    FailureTo = new[] { "oncall@example.com" }
                },
                sender);

            var result = await dispatcher.DispatchAsync(
                result: null,
                NewContext(dryRun: true),
                artifacts: null,
                TestContext.Current.CancellationToken);

            Assert.Equal(RunNotificationOutcome.Skipped, result.Outcome);
            Assert.Contains("Dry-run", result.Reason);
            Assert.Empty(sender.Messages);
        }

        [Fact]
        public async Task Dispatcher_Sends_Success_Email_When_No_Errors()
        {
            var sender = new RecordingSender();
            var dispatcher = new RunNotificationDispatcher(
                new NotificationOptions
                {
                    Enabled = true,
                    From = "ops@example.com",
                    SuccessTo = new[] { "team@example.com" },
                    FailureTo = new[] { "oncall@example.com" }
                },
                sender);

            var result = await dispatcher.DispatchAsync(
                new ContactSyncRunResult { DryRun = false },
                NewContext(),
                artifacts: null,
                TestContext.Current.CancellationToken);

            Assert.Equal(RunNotificationOutcome.Sent, result.Outcome);
            var message = Assert.Single(sender.Messages);
            Assert.Equal("ops@example.com", message.From);
            Assert.Equal(new[] { "team@example.com" }, message.To);
            Assert.Contains("Success", message.Subject);
            Assert.Empty(message.Attachments);
        }

        [Fact]
        public async Task Dispatcher_Sends_Failure_Email_When_Errors_Present_And_Attaches_Csv()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "contactmesh-dispatcher-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var detail = Path.Combine(tempDir, "detail.csv");
                var summary = Path.Combine(tempDir, "summary.csv");
                await File.WriteAllTextAsync(detail, "header\nrow", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(summary, "header\nrow", TestContext.Current.CancellationToken);

                var sender = new RecordingSender();
                var dispatcher = new RunNotificationDispatcher(
                    new NotificationOptions
                    {
                        Enabled = true,
                        From = "ops@example.com",
                        SuccessTo = new[] { "team@example.com" },
                        FailureTo = new[] { "oncall@example.com" },
                        AttachCsvOnFailure = true
                    },
                    sender);

                var result = new ContactSyncRunResult
                {
                    DryRun = false,
                    Results = new[]
                    {
                        new SyncResult
                        {
                            TargetUserId = "target-1",
                            DryRun = false,
                            Errors = new[] { "graph failed" }
                        }
                    }
                };

                var dispatched = await dispatcher.DispatchAsync(
                    result,
                    NewContext(),
                    new RunAuditArtifacts(detail, summary, 10, 10),
                    TestContext.Current.CancellationToken);

                Assert.Equal(RunNotificationOutcome.Sent, dispatched.Outcome);
                var message = Assert.Single(sender.Messages);
                Assert.Equal(new[] { "oncall@example.com" }, message.To);
                Assert.Contains("FAILED", message.Subject);
                Assert.Equal(2, message.Attachments.Count);
                Assert.Contains(message.Attachments, attachment => attachment.FileName == "detail.csv");
                Assert.Contains(message.Attachments, attachment => attachment.FileName == "summary.csv");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task Dispatcher_Skips_When_No_Sender()
        {
            var dispatcher = new RunNotificationDispatcher(
                new NotificationOptions { Enabled = true, From = "ops@example.com", SuccessTo = new[] { "team@example.com" } },
                sender: null);

            var result = await dispatcher.DispatchAsync(
                new ContactSyncRunResult { DryRun = false },
                NewContext(),
                artifacts: null,
                TestContext.Current.CancellationToken);

            Assert.Equal(RunNotificationOutcome.Skipped, result.Outcome);
            Assert.Contains("sender", result.Reason);
        }

        [Fact]
        public async Task Dispatcher_Skips_When_Recipients_Empty()
        {
            var sender = new RecordingSender();
            var dispatcher = new RunNotificationDispatcher(
                new NotificationOptions { Enabled = true, From = "ops@example.com" },
                sender);

            var result = await dispatcher.DispatchAsync(
                new ContactSyncRunResult { DryRun = false },
                NewContext(),
                artifacts: null,
                TestContext.Current.CancellationToken);

            Assert.Equal(RunNotificationOutcome.Skipped, result.Outcome);
            Assert.Empty(sender.Messages);
        }

        private static RunAuditContext NewContext(bool dryRun = false)
        {
            return new RunAuditContext
            {
                Provider = "Microsoft365",
                RunId = "run-test",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow.AddSeconds(2),
                DryRun = dryRun
            };
        }

        [Fact]
        public async Task Dispatcher_Sends_Warning_Email_To_Failure_Recipients()
        {
            var sender = new RecordingSender();
            var dispatcher = new RunNotificationDispatcher(
                new NotificationOptions
                {
                    Enabled = true,
                    From = "ops@example.com",
                    SuccessTo = new[] { "team@example.com" },
                    FailureTo = new[] { "oncall@example.com" },
                    AttachCsvOnFailure = true
                },
                sender);

            var dispatched = await dispatcher.DispatchAsync(
                new ContactSyncRunResult
                {
                    DryRun = false,
                    RunWarnings = new[] { "alias mismatch" }
                },
                NewContext(),
                artifacts: null,
                TestContext.Current.CancellationToken);

            Assert.Equal(RunNotificationOutcome.Sent, dispatched.Outcome);
            var message = Assert.Single(sender.Messages);
            Assert.Equal(new[] { "oncall@example.com" }, message.To);
            Assert.Contains("Warning", message.Subject);
            Assert.Contains("Outcome: Warning", message.Body);
            Assert.Contains("alias mismatch", message.Body);
            Assert.Empty(message.Attachments);
        }

        private sealed class RecordingSender : IRunNotificationSender
        {
            public List<NotificationMessage> Messages { get; } = new();

            public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
            {
                this.Messages.Add(message);
                return Task.CompletedTask;
            }
        }
    }
}
