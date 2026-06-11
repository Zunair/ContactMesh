// File: RunAuditWriterTests.cs
// Author: Zunair
// Producer: Copilot

using System.Text;
using ContactMesh.Core.Audit;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class RunAuditWriterTests : IDisposable
    {
        private readonly string root;

        public RunAuditWriterTests()
        {
            this.root = Path.Combine(Path.GetTempPath(), "contactmesh-audit-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(this.root))
            {
                Directory.Delete(this.root, recursive: true);
            }
        }

        [Fact]
        public async Task Writer_Produces_Detail_And_Summary_Csv_For_Successful_Run()
        {
            var options = new AuditLogOptions { Directory = this.root, IncludeNoChange = false };
            var writer = new RunAuditWriter(options);

            var result = new ContactSyncRunResult
            {
                DryRun = false,
                Results = new[]
                {
                    new SyncResult
                    {
                        TargetUserId = "target-1",
                        TargetUserEmail = "target-1@example.com",
                        DryRun = false,
                        Operations = new[]
                        {
                            new SyncOperation
                            {
                                OperationType = SyncOperationType.Create,
                                DesiredContact = new MeshContact
                                {
                                    SourceId = "user:alice",
                                    DisplayName = "Alice",
                                    Emails = new[] { new ContactEmail("alice@example.com", "work", true) },
                                    Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Directory" },
                                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        ["sourceRule"] = "Directory"
                                    }
                                },
                                Reason = "Adopting new contact"
                            },
                            new SyncOperation
                            {
                                OperationType = SyncOperationType.NoChange,
                                DesiredContact = new MeshContact { SourceId = "user:bob" }
                            }
                        },
                        Warnings = new[] { "minor-warning" }
                    }
                }
            };

            var context = new RunAuditContext
            {
                Provider = "Microsoft365",
                RunId = "run-123",
                StartedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                CompletedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 7, TimeSpan.Zero),
                HostKind = "Cli",
                ConfigPath = @"C:\config.json"
            };

            var artifacts = await writer.WriteAsync(result, context, TestContext.Current.CancellationToken);

            Assert.NotNull(artifacts);
            Assert.True(File.Exists(artifacts!.DetailCsvPath));
            Assert.True(File.Exists(artifacts.SummaryCsvPath));

            var detail = await File.ReadAllTextAsync(artifacts.DetailCsvPath, TestContext.Current.CancellationToken);
            Assert.Contains("Operation,Status", detail);
            Assert.Contains("TargetUserId,TargetUserEmail,Operation", detail);
            Assert.Contains("target-1,target-1@example.com,Create", detail);
            Assert.Contains("Create,Written", detail);
            Assert.Contains("user:alice", detail);
            Assert.Contains("alice@example.com", detail);
            Assert.Contains("Adopting new contact", detail);
            Assert.DoesNotContain("user:bob", detail);
            Assert.Contains("minor-warning", detail);
            Assert.Contains("target-1,target-1@example.com,Warning,Warning", detail);

            var summary = await File.ReadAllTextAsync(artifacts.SummaryCsvPath, TestContext.Current.CancellationToken);
            Assert.Contains("Microsoft365", summary);
            Assert.Contains("TargetUserId,TargetUserEmail,Outcome", summary);
            // Run row
            Assert.Contains("Run,Microsoft365", summary);
            Assert.Contains(",Success,", summary);
            // Target row for target-1
            Assert.Contains("Target,Microsoft365", summary);
            Assert.Contains(",target-1,target-1@example.com,", summary);
            Assert.Contains(",1,1,0,0,1,1,", summary, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Writer_Includes_Run_Warnings_In_Detail_And_Summary()
        {
            var writer = new RunAuditWriter(new AuditLogOptions { Directory = this.root });
            var result = new ContactSyncRunResult
            {
                DryRun = false,
                RunWarnings = new[] { "alias mismatch" }
            };
            var context = new RunAuditContext
            {
                Provider = "Microsoft365",
                RunId = "run-warning",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow
            };

            var artifacts = await writer.WriteAsync(result, context, TestContext.Current.CancellationToken);

            var detail = await File.ReadAllTextAsync(artifacts!.DetailCsvPath, TestContext.Current.CancellationToken);
            Assert.Contains(",,Warning,Warning", detail);
            Assert.Contains("alias mismatch", detail);

            var summary = await File.ReadAllTextAsync(artifacts.SummaryCsvPath, TestContext.Current.CancellationToken);
            Assert.Contains("alias mismatch", summary);
            Assert.Contains(",1,0,", summary, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Writer_Reports_Failure_When_Context_Has_Exception()
        {
            var options = new AuditLogOptions { Directory = this.root };
            var writer = new RunAuditWriter(options);

            var context = new RunAuditContext
            {
                Provider = "Google",
                RunId = "run-fail",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
                Failure = new InvalidOperationException("boom")
            };

            var artifacts = await writer.WriteAsync(result: null, context, TestContext.Current.CancellationToken);

            Assert.NotNull(artifacts);
            var summary = await File.ReadAllTextAsync(artifacts!.SummaryCsvPath, TestContext.Current.CancellationToken);
            Assert.Contains(",Failure,", summary);
            Assert.Contains("boom", summary);
        }

        [Fact]
        public async Task Writer_Uses_Context_Dry_Run_When_Result_Is_Missing()
        {
            var options = new AuditLogOptions { Directory = this.root };
            var writer = new RunAuditWriter(options);

            var context = new RunAuditContext
            {
                Provider = "Google",
                RunId = "run-dry-fail",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
                DryRun = true,
                Failure = new InvalidOperationException("boom")
            };

            var artifacts = await writer.WriteAsync(result: null, context, TestContext.Current.CancellationToken);

            Assert.NotNull(artifacts);
            var summary = await File.ReadAllTextAsync(artifacts!.SummaryCsvPath, TestContext.Current.CancellationToken);
            Assert.Contains(",true,,,Failure,", summary);
        }

        [Fact]
        public async Task Writer_Returns_Null_When_Disabled()
        {
            var writer = new RunAuditWriter(new AuditLogOptions { Enabled = false });
            var context = new RunAuditContext
            {
                Provider = "Google",
                RunId = "run-disabled",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow
            };

            var artifacts = await writer.WriteAsync(new ContactSyncRunResult(), context, TestContext.Current.CancellationToken);

            Assert.Null(artifacts);
        }

        [Fact]
        public async Task Writer_Skips_Detail_File_When_No_Rows_To_Write()
        {
            var options = new AuditLogOptions { Directory = this.root, IncludeNoChange = false };
            var writer = new RunAuditWriter(options);

            var result = new ContactSyncRunResult
            {
                DryRun = false,
                Results = new[]
                {
                    new SyncResult
                    {
                        TargetUserId = "target-1",
                        TargetUserEmail = "target-1@example.com",
                        DryRun = false,
                        Operations = new[]
                        {
                            new SyncOperation
                            {
                                OperationType = SyncOperationType.NoChange,
                                DesiredContact = new MeshContact { SourceId = "user:bob" }
                            }
                        }
                    }
                }
            };

            var context = new RunAuditContext
            {
                Provider = "Microsoft365",
                RunId = "run-empty",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow
            };

            var artifacts = await writer.WriteAsync(result, context, TestContext.Current.CancellationToken);

            Assert.NotNull(artifacts);
            Assert.Equal(string.Empty, artifacts!.DetailCsvPath);
            Assert.Equal(0, artifacts.DetailCsvBytes);
            Assert.True(File.Exists(artifacts.SummaryCsvPath));
            Assert.Empty(Directory.GetFiles(this.root, "*-detail.csv"));
        }

        [Fact]
        public async Task Writer_Escapes_Csv_Special_Characters()
        {
            var options = new AuditLogOptions { Directory = this.root };
            var writer = new RunAuditWriter(options);

            var result = new ContactSyncRunResult
            {
                DryRun = true,
                Results = new[]
                {
                    new SyncResult
                    {
                        TargetUserId = "target,with,commas",
                        TargetUserEmail = "target@example.com",
                        DryRun = true,
                        Operations = new[]
                        {
                            new SyncOperation
                            {
                                OperationType = SyncOperationType.Update,
                                DesiredContact = new MeshContact { DisplayName = "Name \"quoted\"" },
                                ExistingContact = new MeshContact { DisplayName = "Old" },
                                Reason = "line1\nline2"
                            }
                        }
                    }
                }
            };

            var context = new RunAuditContext
            {
                Provider = "Microsoft365",
                RunId = "run-csv",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow
            };

            var artifacts = await writer.WriteAsync(result, context, TestContext.Current.CancellationToken);
            var detail = await File.ReadAllTextAsync(
                artifacts!.DetailCsvPath,
                Encoding.UTF8,
                TestContext.Current.CancellationToken);
            Assert.Contains("\"target,with,commas\",target@example.com", detail);
            Assert.Contains("\"Name \"\"quoted\"\"\"", detail);
            Assert.Contains("\"line1\nline2\"", detail);
        }
    }
}
