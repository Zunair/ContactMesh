// File: ContactSyncRunResultTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Logging;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class ContactSyncRunResultTests
    {
        [Fact]
        public void Summary_Aggregates_Target_Counts_Issues_And_Log_Entries()
        {
            var logEntry = new SyncLogEntry(
                DateTimeOffset.UtcNow,
                SyncLogLevel.Information,
                "Planned target.",
                TargetUserId: "target-1");
            var result = new ContactSyncRunResult
            {
                DryRun = true,
                Results = new[]
                {
                    new SyncResult
                    {
                        TargetUserId = "target-1",
                        DryRun = true,
                        Operations = new[]
                        {
                            Operation(SyncOperationType.Create, "create-1"),
                            Operation(SyncOperationType.NoChange, "same-1")
                        },
                        Warnings = new[] { "warning-1" },
                        Errors = new[] { "error-1" },
                        LogEntries = new[] { logEntry }
                    },
                    new SyncResult
                    {
                        TargetUserId = "target-2",
                        DryRun = true,
                        Operations = new[]
                        {
                            Operation(SyncOperationType.Update, "update-1"),
                            Operation(SyncOperationType.Delete, "delete-1")
                        },
                        Warnings = new[] { "warning-2" }
                    }
                }
            };

            var summary = result.Summary;

            Assert.True(summary.DryRun);
            Assert.Equal(2, summary.TargetCount);
            Assert.Equal(1, summary.CreateCount);
            Assert.Equal(1, summary.UpdateCount);
            Assert.Equal(1, summary.DeleteCount);
            Assert.Equal(1, summary.NoChangeCount);
            Assert.Equal(3, summary.WriteCount);
            Assert.Equal(2, summary.WarningCount);
            Assert.Equal(1, summary.ErrorCount);
            Assert.True(result.HasWarnings);
            Assert.True(result.HasErrors);
            Assert.Equal(new[] { "warning-1", "warning-2" }, result.Warnings);
            Assert.Equal(new[] { "error-1" }, result.Errors);
            Assert.Equal(logEntry, Assert.Single(result.LogEntries));
        }

        private static SyncOperation Operation(SyncOperationType operationType, string sourceId)
        {
            return new SyncOperation
            {
                OperationType = operationType,
                DesiredContact = new MeshContact { SourceId = sourceId }
            };
        }
    }
}
