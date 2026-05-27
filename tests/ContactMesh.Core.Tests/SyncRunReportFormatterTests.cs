using ContactMesh.Core.Logging;
using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class SyncRunReportFormatterTests
{
    [Fact]
    public void ProgressFormatter_Includes_Target_Position_Status_And_Counts()
    {
        var started = new SyncProgress(
            SyncProgressKind.TargetStarted,
            "user-1",
            "first@example.org",
            1,
            2);
        var completed = new SyncProgress(
            SyncProgressKind.TargetCompleted,
            "user-1",
            "first@example.org",
            1,
            2,
            CreateCount: 3,
            NoChangeCount: 4);
        var failed = new SyncProgress(
            SyncProgressKind.TargetFailed,
            "user-2",
            "second@example.org",
            2,
            2,
            ErrorMessage: "contact store unavailable");

        Assert.Equal("Target 1/2 user-1 <first@example.org>: started.", SyncProgressFormatter.Format(started));
        Assert.Equal(
            "Target 1/2 user-1 <first@example.org>: completed with 3 create, 0 update, 0 delete, 4 unchanged.",
            SyncProgressFormatter.Format(completed));
        Assert.Equal(
            "Target 2/2 user-2 <second@example.org>: failed - contact store unavailable",
            SyncProgressFormatter.Format(failed));
    }

    [Fact]
    public void ProgressFormatter_RunStarted_Shows_Scope_And_Target_Count()
    {
        var runStarted = new SyncProgress(
            SyncProgressKind.RunStarted,
            TargetUserId: string.Empty,
            TargetUserEmail: null,
            TargetIndex: 0,
            TargetCount: 6,
            Message: "GlobalUserGroups [it@example.org]");

        Assert.Equal(
            "Scope: GlobalUserGroups [it@example.org] (6 targets)",
            SyncProgressFormatter.Format(runStarted));
    }

    [Fact]
    public async Task RunAsync_Emits_RunStarted_Progress_Before_Targets()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            new MeshUser { Id = "u1", Email = "u1@example.org" },
            new MeshUser { Id = "u2", Email = "u2@example.org" }
        });
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(),
            new NoOpContactProvider());
        var progressUpdates = new List<SyncProgress>();

        await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = true,
                Rules = new SyncRuleOptions { TargetUsers = new[] { "u1@example.org" } }
            },
            CancellationToken.None,
            (p, _) => { progressUpdates.Add(p); return Task.CompletedTask; });

        var runStarted = progressUpdates.First(p => p.Kind == SyncProgressKind.RunStarted);
        Assert.Equal(0, runStarted.TargetIndex);
        Assert.Equal(1, runStarted.TargetCount);
        Assert.Contains("TargetUsers", runStarted.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SyncProgressKind.TargetStarted, progressUpdates.First(p => p.Kind == SyncProgressKind.TargetStarted).Kind);
    }

    [Fact]
    public async Task RunAsync_RunStarted_Scope_Shows_GlobalUserGroups()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            new MeshUser { Id = "u1", Email = "u1@example.org" },
            new MeshUser { Id = "u2", Email = "u2@example.org" }
        });
        var group = new MeshGroup
        {
            Id = "dept",
            Email = "dept@example.org",
            GroupVisibility = MeshGroupVisibility.Domain,
            MemberVisibility = MeshGroupVisibility.Domain,
            Members = new[] { new MeshGroupMember { Id = "u1", Email = "u1@example.org", Type = MeshGroupMemberType.User } }
        };
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(group),
            new NoOpContactProvider());
        var progressUpdates = new List<SyncProgress>();

        await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = true,
                Rules = new SyncRuleOptions { GlobalUserGroups = new[] { "dept@example.org" } }
            },
            CancellationToken.None,
            (p, _) => { progressUpdates.Add(p); return Task.CompletedTask; });

        var runStarted = progressUpdates.First(p => p.Kind == SyncProgressKind.RunStarted);
        Assert.Equal(1, runStarted.TargetCount);
        Assert.Contains("GlobalUserGroups", runStarted.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dept@example.org", runStarted.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_RunStarted_Scope_Shows_All_Users_When_No_Scope_Configured()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            new MeshUser { Id = "u1", Email = "u1@example.org" },
            new MeshUser { Id = "u2", Email = "u2@example.org" }
        });
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(),
            new NoOpContactProvider());
        var progressUpdates = new List<SyncProgress>();

        await orchestrator.RunAsync(
            new ContactMeshOptions { DryRun = true },
            CancellationToken.None,
            (p, _) => { progressUpdates.Add(p); return Task.CompletedTask; });

        var runStarted = progressUpdates.First(p => p.Kind == SyncProgressKind.RunStarted);
        Assert.Equal(2, runStarted.TargetCount);
        Assert.Contains("all", runStarted.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_RunStarted_Warns_When_GlobalUserGroups_Group_Not_Found()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            new MeshUser { Id = "u1", Email = "u1@example.org" }
        });
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(),
            new NoOpContactProvider());
        var progressUpdates = new List<SyncProgress>();

        await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = true,
                Rules = new SyncRuleOptions { GlobalUserGroups = new[] { "missing-group@example.org" } }
            },
            CancellationToken.None,
            (p, _) => { progressUpdates.Add(p); return Task.CompletedTask; });

        var runStarted = progressUpdates.First(p => p.Kind == SyncProgressKind.RunStarted);
        Assert.Contains("no members found", runStarted.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeDirectoryProvider : IDirectoryProvider
    {
        private readonly IReadOnlyList<MeshUser> users;
        public FakeDirectoryProvider(IReadOnlyList<MeshUser> users) => this.users = users;
        public Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken _) => Task.FromResult(this.users);
    }

    private sealed class FakeGroupProvider : IGroupProvider
    {
        private readonly IReadOnlyList<MeshGroup> groups;
        public FakeGroupProvider(params MeshGroup[] groups) => this.groups = groups;
        public Task<IReadOnlyList<MeshGroup>> GetGroupsAsync(CancellationToken _) => Task.FromResult(this.groups);
        public Task<IReadOnlyList<MeshContact>> GetGroupContactsAsync(string groupId, CancellationToken _) =>
            Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
    }

    private sealed class NoOpContactProvider : IContactProvider
    {
        public Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken _) =>
            Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
        public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken _) => Task.CompletedTask;
    }

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
                                DisplayName = "Directory User",
                                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    "Directory",
                                    "team-id"
                                },
                                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["sourceRule"] = "Directory"
                                }
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
        Assert.Contains("    Labels: Directory, team-id", lines);
        Assert.Contains("    Source: Directory user contact.", lines);
        Assert.DoesNotContain(lines, line => line.Contains("No managed fields changed.", StringComparison.Ordinal));
    }

    [Fact]
    public void Format_DryRun_Includes_Group_Source_Rule_Details()
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
                                SourceId = "group:opaque-id",
                                DisplayName = "+Front Desk",
                                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    "front-desk@example.org",
                                    "opaque-id"
                                },
                                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["sourceRule"] = "VisibleGroupContact",
                                    ["sourceGroupDisplayName"] = "Front Desk",
                                    ["sourceGroupEmail"] = "front-desk@example.org",
                                    ["sourceGroupId"] = "opaque-id"
                                }
                            },
                            Reason = "New managed contact."
                        }
                    }
                }
            }
        };

        var lines = SyncRunReportFormatter.Format(result);

        Assert.Contains("    Labels: front-desk@example.org, opaque-id", lines);
        Assert.Contains("    Source: Visible group contact from Front Desk (email=front-desk@example.org, id=opaque-id).", lines);
    }

    [Fact]
    public void Format_DryRun_Uses_Existing_Contact_Identity_For_Cleanup_Update()
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
                            OperationType = SyncOperationType.Update,
                            DesiredContact = new MeshContact
                            {
                                Notes = "Keep this."
                            },
                            ExistingContact = new MeshContact
                            {
                                DisplayName = "Former Employee",
                                Emails = new[] { new ContactEmail("former@example.org", "work", true) }
                            },
                            Reason = "Managed contact is stale; preserving user-owned details (notes=Keep this.) and removing managed fields."
                        }
                    }
                }
            }
        };

        var lines = SyncRunReportFormatter.Format(result);

        Assert.Contains(
            "  Dry-run update: Former Employee - Managed contact is stale; preserving user-owned details (notes=Keep this.) and removing managed fields.",
            lines);
        Assert.DoesNotContain(lines, line => line.Contains("(unknown contact)", StringComparison.Ordinal));
    }

    [Fact]
    public void Format_DryRun_Shows_Labels_Removed_Line_For_Update_With_Label_Drops()
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
                            OperationType = SyncOperationType.Update,
                            DesiredContact = new MeshContact
                            {
                                DisplayName = "+IT-Department",
                                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    "IT Department"
                                }
                            },
                            ExistingContact = new MeshContact
                            {
                                DisplayName = "+IT-Department",
                                Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    "IT Department",
                                    "dept-id",
                                    "dept@example.org"
                                }
                            },
                            Reason = "Managed fields changed."
                        }
                    }
                }
            }
        };

        var lines = SyncRunReportFormatter.Format(result);

        Assert.Contains("    Labels: IT Department", lines);
        Assert.Contains("    Labels removed: dept-id, dept@example.org", lines);
    }

    [Fact]
    public void Format_DryRun_Shows_Changed_Fields_Line_For_Update()
    {
        var existing = new MeshContact
        {
            DisplayName = "Jane Doe",
            GivenName = "Jane",
            FamilyName = "Doe",
            Department = "Operations",
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" }
        };

        var desired = existing with
        {
            Department = "Engineering",
            JobTitle = "Director"
        };

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
                            OperationType = SyncOperationType.Update,
                            DesiredContact = desired,
                            ExistingContact = existing,
                            Reason = "Managed fields changed."
                        }
                    }
                }
            }
        };

        var lines = SyncRunReportFormatter.Format(result);

        Assert.Contains("    Changed: Department, JobTitle", lines);
    }

    [Fact]
    public void Format_DryRun_Does_Not_Show_Changed_Line_For_Stale_Update()
    {
        // Stale-cleanup updates use a custom reason; the Changed: line should still appear
        // when fields genuinely differ, but a stale cleanup that only strips managed fields
        // produces a desired contact that is a subset of existing — Changed: should be absent
        // when the only difference is removal (labels/emails/phones stripped by cleanup).
        var existing = new MeshContact
        {
            DisplayName = "Former Employee",
            GivenName = "Former",
            FamilyName = "Employee",
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" },
            Notes = "Keep this."
        };

        var desired = existing with
        {
            DisplayName = null,
            GivenName = null,
            FamilyName = null,
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

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
                            OperationType = SyncOperationType.Update,
                            DesiredContact = desired,
                            ExistingContact = existing,
                            Reason = "Managed contact is stale; preserving user-owned details (notes=Keep this.) and removing managed fields."
                        }
                    }
                }
            }
        };

        var lines = SyncRunReportFormatter.Format(result);

        // DisplayName, GivenName, FamilyName, Labels all changed — Changed: line should be present
        Assert.Contains(lines, line => line.StartsWith("    Changed:", StringComparison.Ordinal));
    }
}
