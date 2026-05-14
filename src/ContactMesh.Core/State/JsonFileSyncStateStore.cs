using System.Text.Json;
using ContactMesh.Core.Abstractions;

namespace ContactMesh.Core.State;

public sealed class JsonFileSyncStateStore : ISyncStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonFileSyncStateStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        this.path = path;
    }

    public async Task<SyncCheckpoint?> GetCheckpointAsync(string scope, CancellationToken cancellationToken)
    {
        var normalizedScope = Normalize(scope, nameof(scope));

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Checkpoints.TryGetValue(normalizedScope, out var checkpoint)
                ? checkpoint
                : null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task SaveCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var normalizedScope = Normalize(checkpoint.Scope, nameof(checkpoint.Scope));

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            document.Checkpoints[normalizedScope] = checkpoint with
            {
                Scope = normalizedScope,
                Metadata = CreateMetadata(checkpoint.Metadata)
            };

            await this.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task<SyncContactState?> GetContactStateAsync(string targetUserId, string sourceId, CancellationToken cancellationToken)
    {
        var normalizedTargetUserId = Normalize(targetUserId, nameof(targetUserId));
        var normalizedSourceId = Normalize(sourceId, nameof(sourceId));

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.ContactsByTargetUserId.TryGetValue(normalizedTargetUserId, out var contacts)
                && contacts.TryGetValue(normalizedSourceId, out var state)
                    ? state
                    : null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task SaveContactStateAsync(SyncContactState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var normalizedTargetUserId = Normalize(state.TargetUserId, nameof(state.TargetUserId));
        var normalizedSourceId = Normalize(state.SourceId, nameof(state.SourceId));

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!document.ContactsByTargetUserId.TryGetValue(normalizedTargetUserId, out var contacts))
            {
                contacts = new Dictionary<string, SyncContactState>(StringComparer.OrdinalIgnoreCase);
                document.ContactsByTargetUserId[normalizedTargetUserId] = contacts;
            }

            contacts[normalizedSourceId] = state with
            {
                TargetUserId = normalizedTargetUserId,
                SourceId = normalizedSourceId,
                Metadata = CreateMetadata(state.Metadata)
            };

            await this.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task RemoveContactStateAsync(string targetUserId, string sourceId, CancellationToken cancellationToken)
    {
        var normalizedTargetUserId = Normalize(targetUserId, nameof(targetUserId));
        var normalizedSourceId = Normalize(sourceId, nameof(sourceId));

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await this.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (document.ContactsByTargetUserId.TryGetValue(normalizedTargetUserId, out var contacts))
            {
                contacts.Remove(normalizedSourceId);
                if (contacts.Count == 0)
                {
                    document.ContactsByTargetUserId.Remove(normalizedTargetUserId);
                }
            }

            await this.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async Task<StoreDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(this.path))
        {
            return StoreDocument.Empty();
        }

        await using var stream = File.OpenRead(this.path);
        var document = await JsonSerializer.DeserializeAsync<StoreDocument>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);

        return document?.Normalize() ?? StoreDocument.Empty();
    }

    private async Task SaveAsync(StoreDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(this.path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{this.path}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                document.Normalize(),
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, this.path, overwrite: true);
    }

    private static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        return value.Trim();
    }

    private static IDictionary<string, string> CreateMetadata(IDictionary<string, string> metadata)
    {
        return new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record StoreDocument
    {
        public Dictionary<string, SyncCheckpoint> Checkpoints { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, SyncContactState>> ContactsByTargetUserId { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public static StoreDocument Empty()
        {
            return new StoreDocument();
        }

        public StoreDocument Normalize()
        {
            return this with
            {
                Checkpoints = this.Checkpoints.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value with { Metadata = CreateMetadata(pair.Value.Metadata) },
                    StringComparer.OrdinalIgnoreCase),
                ContactsByTargetUserId = this.ContactsByTargetUserId.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToDictionary(
                        contactPair => contactPair.Key,
                        contactPair => contactPair.Value with { Metadata = CreateMetadata(contactPair.Value.Metadata) },
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
