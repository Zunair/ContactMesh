using ContactMesh.Core.Logging;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class SyncRunReportFormatterTests
{
    [Fact]
    public void Format_DryRun_Includes_Summary_Issues_And_Planned_Writes()
    {
        var result = new ContactSyncRunResult
        {
            DryRun = true,
            Results = new[]
            {
                new SyncResult
                {
                    TargetUserId = "target@example.org",
                    DryRun = true,
                    Operations = new[]
                    {
                        new SyncOperation
                        {
                            OperationType = SyncOperationType.Create,
                            DesiredContact = new MeshContact
                            {
                                SourceId = "directory-user-1",
                                DisplayName = "Directory User"
                            },
                            Reason = "New managed contact."
                        },
                        new SyncOperation
                        {
                            OperationType = SyncOperationType.NoChange,
                            DesiredContact = new MeshContact { SourceId = "same-1" },
                            Reason = "No managed fields changed."
                        }
                    },
                    Warnings = new[] { "Skipped incomplete phone." },
                    Errors = new[] { "Provider write failed." }
                }
            }
        };

        var lines = SyncRunReportFormatter.Format(result);

        Assert.Contains("Targets: 1", lines);
        Assert.Contains("Plan: 1 create, 0 update, 0 delete, 1 unchanged.", lines);
        Assert.Contains("Writes: 1", lines);
        Assert.Contains("Dry run: true (provider writes skipped: 1)", lines);
        Assert.Contains("Warnings: 1", lines);
        Assert.Contains("Errors: 1", lines);
        Assert.Contains("Target target@example.org: 1 create, 0 update, 0 delete, 1 unchanged.", lines);
        Assert.Contains("  Warning: Skipped incomplete phone.", lines);
        Assert.Contains("  Error: Provider write failed.", lines);
        Assert.Contains("  Dry-run create: Directory User [directory-user-1] - New managed contact.", lines);
        Assert.DoesNotContain(lines, line => line.Contains("No managed fields changed.", StringComparison.Ordinal));
    }
}
