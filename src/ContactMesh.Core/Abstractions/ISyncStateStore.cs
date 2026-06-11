// File: ISyncStateStore.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Abstractions
{
    public interface ISyncStateStore
    {
        Task<SyncCheckpoint?> GetCheckpointAsync(string scope, CancellationToken cancellationToken);
        Task SaveCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken);
        Task<SyncContactState?> GetContactStateAsync(string targetUserId, string sourceId, CancellationToken cancellationToken);
        Task SaveContactStateAsync(SyncContactState state, CancellationToken cancellationToken);
        Task RemoveContactStateAsync(string targetUserId, string sourceId, CancellationToken cancellationToken);
    }

    public sealed record SyncCheckpoint
    {
        public required string Scope { get; init; }
        public string? Cursor { get; init; }
        public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
        public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record SyncContactState
    {
        public required string TargetUserId { get; init; }
        public required string SourceId { get; init; }
        public string? ProviderContactId { get; init; }
        public string? ETag { get; init; }
        public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
        public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
