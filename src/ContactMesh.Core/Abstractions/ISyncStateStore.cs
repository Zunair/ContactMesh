namespace ContactMesh.Core.Abstractions;

public interface ISyncStateStore
{
    Task<string?> GetCursorAsync(string targetUserId, CancellationToken cancellationToken);
    Task SaveCursorAsync(string targetUserId, string cursor, CancellationToken cancellationToken);
}
