using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Xunit;

namespace ContactMesh.Core.Tests;

public sealed class ContactSyncOrchestratorTests
{
    [Fact]
    public async Task RunAsync_Builds_Targets_Desired_Contacts_And_Executes_Plans()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("user-1", "first@example.org"),
            User("user-2", "second@example.org"),
            User("blocked", "blocked@example.org"),
            User("suspended", "suspended@example.org", isSuspended: true)
        });
        var visibleGroup = Group(
            "team",
            "team@example.org",
            MeshGroupVisibility.Domain,
            MeshGroupVisibility.Domain,
            "user-1",
            "user-2");
        var exclusionGroup = Group(
            "blocked-group",
            "blocked-group@example.org",
            MeshGroupVisibility.Hidden,
            MeshGroupVisibility.Hidden,
            "blocked");
        var externalContact = new MeshContact
        {
            SourceId = "external-1",
            DisplayName = "External Person"
        };
        var groupProvider = new FakeGroupProvider(
            new[] { visibleGroup, exclusionGroup },
            new Dictionary<string, IReadOnlyList<MeshContact>>(StringComparer.OrdinalIgnoreCase)
            {
                ["team"] = new[] { externalContact }
            });
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(directoryProvider, groupProvider, contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    ExclusionGroups = new[] { "blocked-group" }
                }
            },
            CancellationToken.None);

        Assert.Equal(2, result.TargetCount);
        Assert.Equal(2, contactProvider.AppliedChanges.Count);
        Assert.All(contactProvider.AppliedChanges.Values, changes =>
        {
            Assert.Contains(changes.Creates, contact => contact.SourceId is "user-1" or "user-2");
            Assert.Contains(changes.Creates, contact => contact.SourceId == "group:team");
            Assert.Contains(changes.Creates, contact => contact.SourceId == "external-1");
            Assert.DoesNotContain(changes.Creates, contact => contact.SourceId == "blocked");
            Assert.DoesNotContain(changes.Creates, contact => contact.SourceId == "suspended");
        });
        var directoryContacts = contactProvider.AppliedChanges.Values
            .SelectMany(changes => changes.Creates)
            .Where(contact => contact.SourceId is "user-1" or "user-2");
        Assert.All(directoryContacts, contact => Assert.Contains(ContactSyncOrchestrator.DirectoryLabel, contact.Labels));
        Assert.All(
            contactProvider.AppliedChanges.Values.SelectMany(changes => changes.Creates).Where(contact => contact.SourceId == "external-1"),
            contact => Assert.Contains("team@example.org", contact.Labels));
    }

    [Fact]
    public async Task RunAsync_Reports_Target_Errors_And_Continues()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("user-1", "first@example.org"),
            User("user-2", "second@example.org")
        });
        var contactProvider = new CapturingContactProvider
        {
            FailingUserId = "user-1"
        };
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(Array.Empty<MeshGroup>(), new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions { DryRun = true },
            CancellationToken.None);

        Assert.Equal(2, result.TargetCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Contains("Target sync failed for user-1", Assert.Single(result.Errors));
        Assert.Contains(result.Results, syncResult => syncResult.TargetUserId == "user-2" && !syncResult.HasErrors);
    }

    [Fact]
    public async Task RunAsync_TargetUserScope_Limits_Recipients_But_Not_Directory_Source_Contacts()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org"),
            User("source", "source@example.org")
        });
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(Array.Empty<MeshGroup>(), new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" }
                }
            },
            CancellationToken.None);

        var syncResult = Assert.Single(result.Results);
        Assert.Equal("target", syncResult.TargetUserId);

        var applied = Assert.Single(contactProvider.AppliedChanges);
        Assert.Equal("target", applied.Key);
        Assert.Contains(applied.Value.Creates, contact => contact.SourceId == "source");
        Assert.DoesNotContain(applied.Value.Creates, contact => contact.SourceId == "target");
    }

    [Fact]
    public async Task RunAsync_MainContactsGroup_Limits_Directory_Source_Contacts_And_Applies_Label()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org"),
            User("direct", "direct@example.org"),
            User("nested", "nested@example.org"),
            User("outside", "outside@example.org")
        });
        var rootGroup = new MeshGroup
        {
            Id = "root",
            Email = "staff@example.org",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "direct",
                    Email = "direct@example.org",
                    Type = MeshGroupMemberType.User
                },
                new MeshGroupMember
                {
                    Id = "nested-group",
                    Email = "nested-group@example.org",
                    Type = MeshGroupMemberType.Group
                }
            }
        };
        var nestedGroup = Group(
            "nested-group",
            "nested-group@example.org",
            MeshGroupVisibility.Hidden,
            MeshGroupVisibility.Hidden,
            "nested");
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(new[] { rootGroup, nestedGroup }, new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" },
                    MainContactsGroupEmail = "staff@example.org",
                    MainContactsGroupLable = "-Directory"
                }
            },
            CancellationToken.None);

        var syncResult = Assert.Single(result.Results);
        Assert.Equal("target", syncResult.TargetUserId);

        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        Assert.Contains(applied.Creates, contact => contact.SourceId == "direct");
        Assert.Contains(applied.Creates, contact => contact.SourceId == "nested");
        Assert.DoesNotContain(applied.Creates, contact => contact.SourceId == "outside");
        Assert.All(applied.Creates.Where(contact => contact.SourceId is "direct" or "nested"), contact =>
            Assert.Contains("-Directory", contact.Labels));
    }

    private static MeshUser User(string id, string email, bool isSuspended = false)
    {
        return new MeshUser { Id = id, Email = email, IsSuspended = isSuspended };
    }

    private static MeshGroup Group(
        string id,
        string email,
        MeshGroupVisibility groupVisibility,
        MeshGroupVisibility memberVisibility,
        params string[] memberIds)
    {
        return new MeshGroup
        {
            Id = id,
            Email = email,
            DisplayName = email,
            GroupVisibility = groupVisibility,
            MemberVisibility = memberVisibility,
            Members = memberIds
                .Select(memberId => new MeshGroupMember
                {
                    Id = memberId,
                    Email = $"{memberId}@example.org",
                    Type = MeshGroupMemberType.User
                })
                .ToList()
        };
    }

    private sealed class FakeDirectoryProvider : IDirectoryProvider
    {
        private readonly IReadOnlyList<MeshUser> users;

        public FakeDirectoryProvider(IReadOnlyList<MeshUser> users)
        {
            this.users = users;
        }

        public Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(this.users);
        }
    }

    private sealed class FakeGroupProvider : IGroupProvider
    {
        private readonly IReadOnlyList<MeshGroup> groups;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<MeshContact>> contactsByGroupId;

        public FakeGroupProvider(
            IReadOnlyList<MeshGroup> groups,
            IReadOnlyDictionary<string, IReadOnlyList<MeshContact>> contactsByGroupId)
        {
            this.groups = groups;
            this.contactsByGroupId = contactsByGroupId;
        }

        public Task<IReadOnlyList<MeshGroup>> GetGroupsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(this.groups);
        }

        public Task<IReadOnlyList<MeshContact>> GetGroupContactsAsync(string groupId, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                this.contactsByGroupId.TryGetValue(groupId, out var contacts)
                    ? contacts
                    : Array.Empty<MeshContact>());
        }
    }

    private sealed class CapturingContactProvider : IContactProvider
    {
        public IDictionary<string, ContactChangeSet> AppliedChanges { get; } =
            new Dictionary<string, ContactChangeSet>(StringComparer.OrdinalIgnoreCase);

        public string? FailingUserId { get; init; }

        public Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
        {
            if (string.Equals(userId, this.FailingUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("contact store unavailable");
            }

            return Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
        }

        public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
        {
            this.AppliedChanges[userId] = changes;

            return Task.CompletedTask;
        }
    }
}
