using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

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
        Assert.Contains(result.LogEntries, entry => entry.Message.Contains("Planned 1 operation(s)", StringComparison.Ordinal));
        Assert.Contains(result.LogEntries, entry => entry.Message.Contains("Dry run enabled", StringComparison.Ordinal));
        var operationEntry = result.LogEntries.Single(entry => entry.OperationType == SyncOperationType.Create);
        Assert.Equal("user@example.org", operationEntry.TargetUserId);
        Assert.Equal("directory-user-1", operationEntry.SourceId);
        Assert.Equal("New managed contact.", operationEntry.Reason);
        Assert.Contains("Dry-run create Directory User.", operationEntry.Message, StringComparison.Ordinal);
    }

    private sealed class FakeContactProvider : IContactProvider
    {
        public int ApplyCount { get; private set; }

        public Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
        }

        public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
        {
            this.ApplyCount++;

            return Task.CompletedTask;
        }
    }
}
