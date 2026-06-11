// File: SyncExecutorTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Logging;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class SyncExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_DryRun_Logs_Planned_Writes_And_Skips_Provider()
        {
            var provider = new FakeContactProvider();
            var executor = new SyncExecutor(provider);

            var result = await executor.ExecuteAsync(
                new SyncTarget { UserId = "user@example.org", UserEmail = "user@example.org" },
                new[]
                {
                    new SyncOperation
                    {
                        OperationType = SyncOperationType.Create,
                        DesiredContact = new MeshContact { SourceId = "directory-user-1", DisplayName = "Directory User" },
                        Reason = "New managed contact."
                    }
                },
                dryRun: true,
                CancellationToken.None);

            Assert.Equal(0, provider.ApplyCount);
            Assert.Equal("user@example.org", result.TargetUserEmail);
            Assert.Contains(result.LogEntries, entry => entry.Message.Contains("Planned 1 operation(s)", StringComparison.Ordinal));
            Assert.Contains(result.LogEntries, entry => entry.Message.Contains("Dry run enabled", StringComparison.Ordinal));
            var operationEntry = result.LogEntries.Single(entry => entry.OperationType == SyncOperationType.Create);
            Assert.Equal("user@example.org", operationEntry.TargetUserId);
            Assert.Equal("directory-user-1", operationEntry.SourceId);
            Assert.Equal("New managed contact.", operationEntry.Reason);
            Assert.Contains("Dry-run create Directory User.", operationEntry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ExecuteAsync_DisableDeletes_Skips_Delete_Writes_But_Reports_Plan()
        {
            var provider = new FakeContactProvider();
            var executor = new SyncExecutor(provider);
            var staleContact = new MeshContact { SourceId = "stale-1", DisplayName = "Stale Contact" };

            var result = await executor.ExecuteAsync(
                new SyncTarget { UserId = "user@example.org", UserEmail = "user@example.org" },
                new[]
                {
                    new SyncOperation
                    {
                        OperationType = SyncOperationType.Delete,
                        DesiredContact = staleContact,
                        Reason = "Stale managed contact."
                    }
                },
                dryRun: false,
                CancellationToken.None,
                disableDeletes: true);

            Assert.Equal(1, provider.ApplyCount);
            Assert.NotNull(provider.LastChanges);
            Assert.Empty(provider.LastChanges.Deletes);
            Assert.True(provider.LastChanges.DeleteWritesDisabled);
            Assert.Equal(1, result.DeleteCount);
            Assert.Contains(result.LogEntries, entry => entry.Level == SyncLogLevel.Warning && entry.Message.Contains("Delete writes disabled", StringComparison.Ordinal));
            Assert.Contains(result.LogEntries, entry => entry.OperationType == SyncOperationType.Delete && entry.Message.Contains("Skipped delete Stale Contact.", StringComparison.Ordinal));
        }

        private sealed class FakeContactProvider : IContactProvider
        {
            public int ApplyCount { get; private set; }
            public ContactChangeSet? LastChanges { get; private set; }

            public Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
            }

            public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
            {
                this.ApplyCount++;
                this.LastChanges = changes;

                return Task.CompletedTask;
            }
        }
    }
}
