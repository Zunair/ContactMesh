// File: JsonFileSyncStateStoreTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.State;
using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class JsonFileSyncStateStoreTests
    {
        [Fact]
        public async Task SaveCheckpointAsync_Persists_Cursor_And_Metadata()
        {
            var path = CreateStatePath();
            var store = new JsonFileSyncStateStore(path);

            await store.SaveCheckpointAsync(
                new SyncCheckpoint
                {
                    Scope = " directory ",
                    Cursor = "cursor-1",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["etag"] = "etag-1"
                    }
                },
                CancellationToken.None);

            var reloaded = new JsonFileSyncStateStore(path);
            var checkpoint = await reloaded.GetCheckpointAsync("DIRECTORY", CancellationToken.None);

            Assert.NotNull(checkpoint);
            Assert.Equal("directory", checkpoint.Scope);
            Assert.Equal("cursor-1", checkpoint.Cursor);
            Assert.Equal("etag-1", checkpoint.Metadata["ETAG"]);
        }

        [Fact]
        public async Task SaveContactStateAsync_Persists_Provider_Id_Etag_And_Metadata_By_Target()
        {
            var path = CreateStatePath();
            var store = new JsonFileSyncStateStore(path);

            await store.SaveContactStateAsync(
                new SyncContactState
                {
                    TargetUserId = " user@example.org ",
                    SourceId = " directory-user-1 ",
                    ProviderContactId = "people/c123",
                    ETag = "etag-1",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["resource"] = "people/c123"
                    }
                },
                CancellationToken.None);

            var reloaded = new JsonFileSyncStateStore(path);
            var state = await reloaded.GetContactStateAsync(
                "USER@example.org",
                "DIRECTORY-user-1",
                CancellationToken.None);

            Assert.NotNull(state);
            Assert.Equal("user@example.org", state.TargetUserId);
            Assert.Equal("directory-user-1", state.SourceId);
            Assert.Equal("people/c123", state.ProviderContactId);
            Assert.Equal("etag-1", state.ETag);
            Assert.Equal("people/c123", state.Metadata["RESOURCE"]);
        }

        [Fact]
        public async Task RemoveContactStateAsync_Removes_Mapping_Without_Removing_Other_Targets()
        {
            var path = CreateStatePath();
            var store = new JsonFileSyncStateStore(path);

            await store.SaveContactStateAsync(
                State("first@example.org", "source-1", "people/first"),
                CancellationToken.None);
            await store.SaveContactStateAsync(
                State("second@example.org", "source-1", "people/second"),
                CancellationToken.None);

            await store.RemoveContactStateAsync("FIRST@example.org", "SOURCE-1", CancellationToken.None);

            Assert.Null(await store.GetContactStateAsync("first@example.org", "source-1", CancellationToken.None));
            Assert.NotNull(await store.GetContactStateAsync("second@example.org", "source-1", CancellationToken.None));
        }

        [Fact]
        public async Task GetAsync_Returns_Null_For_Missing_State()
        {
            var store = new JsonFileSyncStateStore(CreateStatePath());

            Assert.Null(await store.GetCheckpointAsync("missing", CancellationToken.None));
            Assert.Null(await store.GetContactStateAsync("user@example.org", "source-1", CancellationToken.None));
        }

        private static SyncContactState State(string targetUserId, string sourceId, string providerContactId)
        {
            return new SyncContactState
            {
                TargetUserId = targetUserId,
                SourceId = sourceId,
                ProviderContactId = providerContactId,
                ETag = $"etag:{providerContactId}"
            };
        }

        private static string CreateStatePath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ContactMesh.Tests", Guid.NewGuid().ToString("N"));
            return Path.Combine(directory, "sync-state.json");
        }
    }
}
