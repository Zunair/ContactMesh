using ContactMesh.Core.Logging;
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
        var managedContacts = contactProvider.AppliedChanges.Values
            .SelectMany(changes => changes.Creates);
        Assert.All(managedContacts, contact => Assert.Contains(ContactSyncOrchestrator.DirectoryLabel, contact.Labels));
        Assert.All(
            contactProvider.AppliedChanges.Values.SelectMany(changes => changes.Creates).Where(contact => contact.SourceId == "external-1"),
            contact => Assert.DoesNotContain("team@example.org", contact.Labels));
        Assert.Equal(1, groupProvider.GroupContactReadCounts["team"]);
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
    public async Task RunAsync_Reports_Progress_For_Each_Target()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("user-1", "first@example.org"),
            User("user-2", "second@example.org")
        });
        var contactProvider = new CapturingContactProvider
        {
            FailingUserId = "user-2"
        };
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(Array.Empty<MeshGroup>(), new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);
        var progressUpdates = new List<SyncProgress>();

        await orchestrator.RunAsync(
            new ContactMeshOptions { DryRun = false },
            CancellationToken.None,
            (progress, _) =>
            {
                progressUpdates.Add(progress);
                return Task.CompletedTask;
            });

        Assert.Collection(
            progressUpdates,
            progress =>
            {
                Assert.Equal(SyncProgressKind.RunStarted, progress.Kind);
                Assert.Equal(2, progress.TargetCount);
                Assert.NotNull(progress.Message);
            },
            progress =>
            {
                Assert.Equal(SyncProgressKind.TargetStarted, progress.Kind);
                Assert.Equal("user-1", progress.TargetUserId);
                Assert.Equal(1, progress.TargetIndex);
                Assert.Equal(2, progress.TargetCount);
            },
            progress =>
            {
                Assert.Equal(SyncProgressKind.TargetCompleted, progress.Kind);
                Assert.Equal("user-1", progress.TargetUserId);
                Assert.Equal(1, progress.CreateCount);
            },
            progress =>
            {
                Assert.Equal(SyncProgressKind.TargetStarted, progress.Kind);
                Assert.Equal("user-2", progress.TargetUserId);
                Assert.Equal(2, progress.TargetIndex);
                Assert.Equal(2, progress.TargetCount);
            },
            progress =>
            {
                Assert.Equal(SyncProgressKind.TargetFailed, progress.Kind);
                Assert.Equal("user-2", progress.TargetUserId);
                Assert.Equal("contact store unavailable", progress.ErrorMessage);
            });
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
    public async Task RunAsync_GlobalUserGroups_Limits_Recipients_To_Group_Members()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("member", "member@example.org"),
            User("non-member", "non-member@example.org")
        });
        var globalGroup = new MeshGroup
        {
            Id = "global-group",
            Email = "global-group@example.org",
            GroupVisibility = MeshGroupVisibility.Domain,
            MemberVisibility = MeshGroupVisibility.Domain,
            Members = new[]
            {
                new MeshGroupMember { Id = "member", Email = "member@example.org", Type = MeshGroupMemberType.User }
            }
        };
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(new[] { globalGroup }, new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    GlobalUserGroups = new[] { "global-group@example.org" }
                }
            },
            CancellationToken.None);

        var syncResult = Assert.Single(result.Results);
        Assert.Equal("member", syncResult.TargetUserId);
        Assert.DoesNotContain(result.Results, r => r.TargetUserId == "non-member");
    }

    [Fact]
    public async Task RunAsync_GlobalUserGroups_Combines_With_TargetUsers()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("group-member", "group-member@example.org"),
            User("explicit-target", "explicit-target@example.org"),
            User("other", "other@example.org")
        });
        var globalGroup = new MeshGroup
        {
            Id = "global-group",
            Email = "global-group@example.org",
            GroupVisibility = MeshGroupVisibility.Domain,
            MemberVisibility = MeshGroupVisibility.Domain,
            Members = new[]
            {
                new MeshGroupMember { Id = "group-member", Email = "group-member@example.org", Type = MeshGroupMemberType.User }
            }
        };
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(new[] { globalGroup }, new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "explicit-target@example.org" },
                    GlobalUserGroups = new[] { "global-group@example.org" }
                }
            },
            CancellationToken.None);

        Assert.Equal(2, result.TargetCount);
        Assert.Contains(result.Results, r => r.TargetUserId == "group-member");
        Assert.Contains(result.Results, r => r.TargetUserId == "explicit-target");
        Assert.DoesNotContain(result.Results, r => r.TargetUserId == "other");
    }

    [Fact]
    public async Task RunAsync_MainContactsGroup_Limits_Directory_Source_Contacts_And_Applies_Label()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org"),
            User("direct", "direct@example.org"),
            User("nested", "nested@example.org"),
            User("contractor", "contractor@example.org"),
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
        var contractorGroup = Group(
            "contractors",
            "contractors@example.org",
            MeshGroupVisibility.Hidden,
            MeshGroupVisibility.Hidden,
            "contractor");
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(
                new[] { rootGroup, nestedGroup, contractorGroup },
                new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" },
                    MainContactsGroupEmails = new[] { "staff@example.org", "contractors@example.org" },
                    MainContactsGroupLabel = "-Directory"
                }
            },
            CancellationToken.None);

        var syncResult = Assert.Single(result.Results);
        Assert.Equal("target", syncResult.TargetUserId);

        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        Assert.Contains(applied.Creates, contact => contact.SourceId == "direct");
        Assert.Contains(applied.Creates, contact => contact.SourceId == "nested");
        Assert.Contains(applied.Creates, contact => contact.SourceId == "contractor");
        Assert.DoesNotContain(applied.Creates, contact => contact.SourceId == "outside");
        Assert.All(applied.Creates.Where(contact => contact.SourceId is "direct" or "nested" or "contractor"), contact =>
            Assert.Contains("-Directory", contact.Labels));
    }

    [Fact]
    public async Task RunAsync_MainContactsGroup_Matches_User_By_Alternate_Email_And_Reports_Warning()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org"),
            new MeshUser
            {
                Id = "source",
                Email = "primary@example.org",
                AlternateEmails = new[] { "alias@example.org" },
                Warnings = new[] { "source account alias mismatch" }
            }
        });
        var rootGroup = new MeshGroup
        {
            Id = "root",
            Email = "staff@example.org",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "source",
                    Email = "alias@example.org",
                    Type = MeshGroupMemberType.User
                }
            }
        };
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(new[] { rootGroup }, new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" },
                    MainContactsGroupEmails = new[] { "staff@example.org" }
                }
            },
            CancellationToken.None);

        Assert.Contains("source account alias mismatch", result.RunWarnings);
        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        var contact = Assert.Single(applied.Creates, c => c.SourceId == "source");
        Assert.Equal("primary@example.org", Assert.Single(contact.Emails).Address);
        Assert.Contains("alias@example.org", contact.MatchEmails);
    }

    [Fact]
    public async Task RunAsync_GroupsToSyncByGroup_Creates_Group_Email_Contacts_From_Container_Members()
    {
        // Hierarchy: container (L1) → support label group (L2) → help / list-only contact groups (L3)
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org")
        });
        // Level 3 contact group – resolved from groups list
        var helpGroup = new MeshGroup
        {
            Id = "help",
            Email = "help@example.org",
            DisplayName = "Help Desk",
            Members = Array.Empty<MeshGroupMember>()
        };
        // Level 2 label group
        var supportGroup = new MeshGroup
        {
            Id = "support",
            Email = "support@example.org",
            DisplayName = "Support",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "help",
                    Email = "help@example.org",
                    DisplayName = "Help Desk",
                    Type = MeshGroupMemberType.Group
                },
                new MeshGroupMember
                {
                    Id = "list-only",
                    Email = "list-only@example.org",
                    DisplayName = "List Only",
                    Type = MeshGroupMemberType.Group
                }
            }
        };
        // Level 1 container
        var containerGroup = new MeshGroup
        {
            Id = "contact-labels",
            Email = "contact-labels@example.org",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "support",
                    Email = "support@example.org",
                    DisplayName = "Support",
                    Type = MeshGroupMemberType.Group
                },
                new MeshGroupMember
                {
                    Id = "not-a-group",
                    Email = "not-a-group@example.org",
                    Type = MeshGroupMemberType.User
                }
            }
        };
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(new[] { containerGroup, supportGroup, helpGroup }, new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" },
                    GroupContactPrefix = "#",
                    GroupsToSyncByGroup = new[] { "contact-labels@example.org" }
                }
            },
            CancellationToken.None);

        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        // Level 3 groups become the contacts; label comes from Level 2 ("Support")
        var helpContact = Assert.Single(applied.Creates, contact => contact.SourceId == "group:help");
        var listOnlyContact = Assert.Single(applied.Creates, contact => contact.SourceId == "group:list-only");

        Assert.Contains(new ContactEmail("help@example.org", "work", true), helpContact.Emails);
        Assert.Contains(ContactSyncOrchestrator.DirectoryLabel, helpContact.Labels);
        Assert.Contains("#Support", helpContact.Labels);
        Assert.Equal("GroupsToSyncByGroup", helpContact.Metadata[ContactSyncOrchestrator.SourceRuleMetadataKey]);
        Assert.Equal("help@example.org", helpContact.Metadata[ContactSyncOrchestrator.SourceGroupEmailMetadataKey]);
        Assert.Equal("#List-Only Group", listOnlyContact.DisplayName);
        Assert.Contains(ContactSyncOrchestrator.DirectoryLabel, listOnlyContact.Labels);
        Assert.Contains("#Support", listOnlyContact.Labels);
        // Level 2 label group and user member of container are not promoted to contacts
        Assert.DoesNotContain(applied.Creates, contact => contact.SourceId == "group:support");
        Assert.DoesNotContain(applied.Creates, contact => contact.SourceId == "group:not-a-group");
    }

    [Fact]
    public async Task RunAsync_GroupsToSyncByGroup_Creates_Level3_Group_Contact_With_Level2_Label()
    {
        // Hierarchy: labels (L1) → location (L2, label="Location") → branch (L3, becomes contact)
        // Users inside the L3 group are NOT labeled with the L2 label.
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org"),
            User("branch-user", "branch-user@example.org")
        });
        var labelsContainer = new MeshGroup
        {
            Id = "labels",
            Email = "labels@example.org",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "location",
                    Email = "location@example.org",
                    DisplayName = "Location",
                    Type = MeshGroupMemberType.Group
                }
            }
        };
        // Level 2: label group
        var locationGroup = new MeshGroup
        {
            Id = "location",
            Email = "location@example.org",
            DisplayName = "Location",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "branch",
                    Email = "branch@example.org",
                    DisplayName = "Branch",
                    Type = MeshGroupMemberType.Group
                }
            }
        };
        // Level 3: contact group (contains users; users are not labeled)
        var branchGroup = Group(
            "branch",
            "branch@example.org",
            MeshGroupVisibility.Hidden,
            MeshGroupVisibility.Hidden,
            "branch-user");
        var contactProvider = new CapturingContactProvider();
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(
                new[] { labelsContainer, locationGroup, branchGroup },
                new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" },
                    GroupsToSyncByGroup = new[] { "labels@example.org" }
                }
            },
            CancellationToken.None);

        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        // L3 group becomes the contact with L2 label
        var branchContact = Assert.Single(applied.Creates, c => c.SourceId == "group:branch");
        Assert.Contains(ContactSyncOrchestrator.DirectoryLabel, branchContact.Labels);
        Assert.Contains("+Location", branchContact.Labels);
        // L2 group is NOT promoted to a contact
        Assert.DoesNotContain(applied.Creates, c => c.SourceId == "group:location");
        // Directory users inside L3 are NOT labeled with the L2 label
        var branchUserContact = Assert.Single(applied.Creates, c => c.SourceId == "branch-user");
        Assert.DoesNotContain("+Location", branchUserContact.Labels);
        Assert.DoesNotContain("Location", branchUserContact.Labels);
    }

    [Fact]
    public async Task RunAsync_Deletes_Unmatched_Company_Domain_Contacts_When_Configured()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org")
        });
        var staleContact = new MeshContact
        {
            DisplayName = "Former Employee",
            CompanyName = "Example",
            Emails = new[] { new ContactEmail("former@example.org", "work", true) }
        };
        var contactProvider = new CapturingContactProvider
        {
            ContactsByUserId =
            {
                ["target"] = new[] { staleContact }
            }
        };
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(Array.Empty<MeshGroup>(), new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                ManagedEmailDomains = new[] { "example.org" }
            },
            CancellationToken.None);

        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        Assert.Contains(staleContact, applied.Deletes);
    }

    [Fact]
    public async Task RunAsync_DisableDeletes_Skips_Delete_Writes_But_Keeps_Delete_Report()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org")
        });
        var staleContact = new MeshContact
        {
            DisplayName = "Former Employee",
            CompanyName = "Example",
            Emails = new[] { new ContactEmail("former@example.org", "work", true) }
        };
        var contactProvider = new CapturingContactProvider
        {
            ContactsByUserId =
            {
                ["target"] = new[] { staleContact }
            }
        };
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(Array.Empty<MeshGroup>(), new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        var result = await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                DisableDeletes = true,
                ManagedEmailDomains = new[] { "example.org" }
            },
            CancellationToken.None);

        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        Assert.Empty(applied.Deletes);
        var syncResult = Assert.Single(result.Results);
        Assert.Equal(1, syncResult.DeleteCount);
        Assert.Contains(syncResult.LogEntries, entry => entry.Message.Contains("Delete writes disabled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_GroupsToSyncByGroup_Cleans_Stale_Group_Id_And_Email_Labels_From_Existing_Contact()
    {
        var directoryProvider = new FakeDirectoryProvider(new[]
        {
            User("target", "target@example.org")
        });
        // Hierarchy: labels (L1) → dept (L2, label="IT Department") → unit (L3, contact)
        var labelsContainer = new MeshGroup
        {
            Id = "labels",
            Email = "labels@example.org",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "dept-id",
                    Email = "dept@example.org",
                    DisplayName = "IT Department",
                    Type = MeshGroupMemberType.Group
                }
            }
        };
        // Level 2 label group
        var deptGroup = new MeshGroup
        {
            Id = "dept-id",
            Email = "dept@example.org",
            DisplayName = "IT Department",
            Members = new[]
            {
                new MeshGroupMember
                {
                    Id = "unit-id",
                    Email = "unit@example.org",
                    DisplayName = "IT Unit",
                    Type = MeshGroupMemberType.Group
                }
            }
        };
        // Level 3 contact group
        var unitGroup = new MeshGroup
        {
            Id = "unit-id",
            Email = "unit@example.org",
            DisplayName = "IT Unit",
            Members = Array.Empty<MeshGroupMember>()
        };

        // Existing contact has stale labels: the group ID and email from a prior sync
        var existingGroupContact = new MeshContact
        {
            SourceId = "group:unit-id",
            DisplayName = "+IT-Unit",
            Emails = new[] { new ContactEmail("unit@example.org", "work", true) },
            Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "unit-id",
                "IT Department",
                "unit@example.org"
            }
        };
        var contactProvider = new CapturingContactProvider
        {
            ContactsByUserId =
            {
                ["target"] = new[] { existingGroupContact }
            }
        };
        var orchestrator = new ContactSyncOrchestrator(
            directoryProvider,
            new FakeGroupProvider(
                new[] { labelsContainer, deptGroup, unitGroup },
                new Dictionary<string, IReadOnlyList<MeshContact>>()),
            contactProvider);

        await orchestrator.RunAsync(
            new ContactMeshOptions
            {
                DryRun = false,
                Rules = new SyncRuleOptions
                {
                    TargetUsers = new[] { "target@example.org" },
                    GroupsToSyncByGroup = new[] { "labels@example.org" }
                }
            },
            CancellationToken.None);

        var applied = Assert.Single(contactProvider.AppliedChanges).Value;
        var groupContact = Assert.Single(applied.Updates, c => c.SourceId == "group:unit-id");

        Assert.Contains(ContactSyncOrchestrator.DirectoryLabel, groupContact.Labels);
        Assert.Contains("+IT Department", groupContact.Labels);
        Assert.DoesNotContain("IT Department", groupContact.Labels);
        Assert.DoesNotContain("unit-id", groupContact.Labels);
        Assert.DoesNotContain("unit@example.org", groupContact.Labels);
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

        public IDictionary<string, int> GroupContactReadCounts { get; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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
            this.GroupContactReadCounts[groupId] = this.GroupContactReadCounts.TryGetValue(groupId, out var count)
                ? count + 1
                : 1;

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

        public IDictionary<string, IReadOnlyList<MeshContact>> ContactsByUserId { get; } =
            new Dictionary<string, IReadOnlyList<MeshContact>>(StringComparer.OrdinalIgnoreCase);

        public string? FailingUserId { get; init; }

        public Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
        {
            if (string.Equals(userId, this.FailingUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("contact store unavailable");
            }

            return Task.FromResult(
                this.ContactsByUserId.TryGetValue(userId, out var contacts)
                    ? contacts
                    : Array.Empty<MeshContact>());
        }

        public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
        {
            this.AppliedChanges[userId] = changes;

            return Task.CompletedTask;
        }
    }
}
