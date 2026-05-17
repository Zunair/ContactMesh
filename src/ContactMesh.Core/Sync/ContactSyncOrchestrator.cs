using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Logging;
using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;
using ContactMesh.Core.Rules;

namespace ContactMesh.Core.Sync;

public sealed class ContactSyncOrchestrator
{
    public const string DirectoryLabel = "Directory";
    public const string SourceRuleMetadataKey = "sourceRule";
    public const string SourceGroupIdMetadataKey = "sourceGroupId";
    public const string SourceGroupEmailMetadataKey = "sourceGroupEmail";
    public const string SourceGroupDisplayNameMetadataKey = "sourceGroupDisplayName";

    private readonly IDirectoryProvider directoryProvider;
    private readonly IGroupProvider groupProvider;
    private readonly IContactProvider contactProvider;
    private readonly DirectoryContactFactory directoryContactFactory;
    private readonly GroupContactFactory groupContactFactory;
    private readonly GroupMappingEngine groupMappingEngine;
    private readonly IReadOnlyList<string> additionalManagedMetadataKeys;

    public ContactSyncOrchestrator(
        IDirectoryProvider directoryProvider,
        IGroupProvider groupProvider,
        IContactProvider contactProvider,
        DirectoryContactFactory? directoryContactFactory = null,
        GroupContactFactory? groupContactFactory = null,
        GroupMappingEngine? groupMappingEngine = null,
        IEnumerable<string>? additionalManagedMetadataKeys = null)
    {
        this.directoryProvider = directoryProvider;
        this.groupProvider = groupProvider;
        this.contactProvider = contactProvider;
        this.directoryContactFactory = directoryContactFactory ?? new DirectoryContactFactory();
        this.groupContactFactory = groupContactFactory ?? new GroupContactFactory();
        this.groupMappingEngine = groupMappingEngine ?? new GroupMappingEngine();
        this.additionalManagedMetadataKeys = additionalManagedMetadataKeys is null
            ? Array.Empty<string>()
            : additionalManagedMetadataKeys.ToList();
    }

    public async Task<ContactSyncRunResult> RunAsync(
        ContactMeshOptions options,
        CancellationToken cancellationToken,
        SyncProgressCallback? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var users = await this.directoryProvider.GetUsersAsync(cancellationToken).ConfigureAwait(false);
        var groups = await this.groupProvider.GetGroupsAsync(cancellationToken).ConfigureAwait(false);
        var mappedGroups = this.groupMappingEngine.ApplyMappings(groups, options.Rules.GroupMappings);
        var groupContactSourceGroups = ResolveGroupsToSyncByGroup(
            mappedGroups,
            options.Rules.GroupsToSyncByGroup);
        var excludedUsers = ResolveUserIdsFromGroups(mappedGroups, options.Rules.ExclusionGroups);
        var ruleEngine = new SyncRuleEngine(
            new ExclusionRule(excludedUsers),
            organizationUnitRule: new OrganizationUnitRule(
                options.Rules.IncludedOrganizationUnits,
                options.Rules.ExcludedOrganizationUnits),
            targetUsers: options.Rules.TargetUsers);

        var eligibleUsers = ruleEngine.CreateEligibleUsers(users);
        var sourceUsers = ResolveDirectorySourceUsers(eligibleUsers, mappedGroups, options.Rules);
        var directoryLabel = ResolveDirectoryLabel(options.Rules);
        var groupContactPrefix = ResolveGroupContactPrefix(options.Rules);
        var targetUsers = ruleEngine.CreateTargets(users)
            .ToDictionary(target => target.UserId, StringComparer.OrdinalIgnoreCase);
        var targetEligibleUsers = eligibleUsers
            .Where(user => targetUsers.ContainsKey(user.Id))
            .ToList();

        var results = new List<SyncResult>();
        var planner = CreatePlanner(options, groupContactSourceGroups, this.additionalManagedMetadataKeys);
        var syncEngine = new ContactSyncEngine(
            this.contactProvider,
            planner,
            new SyncExecutor(this.contactProvider));
        var groupContactsByGroupId = new Dictionary<string, IReadOnlyList<MeshContact>>(StringComparer.OrdinalIgnoreCase);

        for (var targetIndex = 0; targetIndex < targetEligibleUsers.Count; targetIndex++)
        {
            var targetUser = targetEligibleUsers[targetIndex];
            var baseTarget = targetUsers[targetUser.Id];
            var visibleGroups = ruleEngine.FilterGroupsForTarget(mappedGroups, baseTarget);
            var target = baseTarget with
            {
                LabelNames = BuildTargetLabels(directoryLabel, groupContactSourceGroups)
            };

            await ReportProgressAsync(
                progress,
                new SyncProgress(
                    SyncProgressKind.TargetStarted,
                    target.UserId,
                    target.UserEmail,
                    targetIndex + 1,
                    targetEligibleUsers.Count),
                cancellationToken).ConfigureAwait(false);

            try
            {
                var desiredContacts = await this.CreateDesiredContactsAsync(
                    sourceUsers,
                    target,
                    mappedGroups,
                    visibleGroups,
                    groupContactSourceGroups,
                    ruleEngine,
                    directoryLabel,
                    groupContactPrefix,
                    groupContactsByGroupId,
                    cancellationToken).ConfigureAwait(false);

                var result = await syncEngine.SyncAsync(
                    target,
                    desiredContacts,
                    options.DryRun,
                    cancellationToken,
                    options.DisableDeletes).ConfigureAwait(false);
                results.Add(result);
                await ReportProgressAsync(
                    progress,
                    new SyncProgress(
                        SyncProgressKind.TargetCompleted,
                        target.UserId,
                        target.UserEmail,
                        targetIndex + 1,
                        targetEligibleUsers.Count,
                        result.CreateCount,
                        result.UpdateCount,
                        result.DeleteCount,
                        result.NoChangeCount),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var result = CreateErrorResult(target, options.DryRun, ex);
                results.Add(result);
                await ReportProgressAsync(
                    progress,
                    new SyncProgress(
                        SyncProgressKind.TargetFailed,
                        target.UserId,
                        target.UserEmail,
                        targetIndex + 1,
                        targetEligibleUsers.Count,
                        ErrorMessage: ex.Message),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return new ContactSyncRunResult
        {
            DryRun = options.DryRun,
            Results = results
        };
    }

    private static Task ReportProgressAsync(
        SyncProgressCallback? progress,
        SyncProgress update,
        CancellationToken cancellationToken)
    {
        return progress is null
            ? Task.CompletedTask
            : progress(update, cancellationToken);
    }

    private async Task<IReadOnlyList<MeshContact>> CreateDesiredContactsAsync(
        IReadOnlyList<MeshUser> eligibleUsers,
        SyncTarget target,
        IReadOnlyList<MeshGroup> allGroups,
        IReadOnlyList<GroupVisibilityDecision> visibleGroups,
        IReadOnlyList<MeshGroup> groupContactSourceGroups,
        SyncRuleEngine ruleEngine,
        string directoryLabel,
        string groupContactPrefix,
        IDictionary<string, IReadOnlyList<MeshContact>> groupContactsByGroupId,
        CancellationToken cancellationToken)
    {
        var contacts = eligibleUsers
            .Where(user => !IsTargetUser(user, target))
            .Select(user => AddMetadata(
                this.directoryContactFactory.CreateUserContact(
                    user,
                    BuildDirectoryContactLabels(user, directoryLabel, groupContactSourceGroups, allGroups)),
                (SourceRuleMetadataKey, "Directory")))
            .ToList();

        foreach (var decision in visibleGroups)
        {
            contacts.Add(AddGroupRuleMetadata(
                this.groupContactFactory.CreateGroupContact(decision.Group, prefix: groupContactPrefix),
                "VisibleGroupContact",
                decision.Group));

            if (decision.CanSeeMembers)
            {
                if (!groupContactsByGroupId.TryGetValue(decision.Group.Id, out var groupContacts))
                {
                    groupContacts = await this.groupProvider.GetGroupContactsAsync(decision.Group.Id, cancellationToken)
                        .ConfigureAwait(false);
                    groupContactsByGroupId[decision.Group.Id] = groupContacts;
                }

                contacts.AddRange(groupContacts.Select(contact => AddGroupRuleMetadata(
                    contact,
                    "VisibleGroupMember",
                    decision.Group)));
            }
        }

        foreach (var group in groupContactSourceGroups)
        {
            var groupLabels = GetGroupContactLabels(group);
            contacts.Add(AddGroupRuleMetadata(
                this.groupContactFactory.CreateGroupContact(group, groupLabels, groupContactPrefix),
                "GroupsToSyncByGroup",
                group));
        }

        return ruleEngine.FilterContactsForTarget(DeduplicateContacts(contacts), target);
    }

    private static IReadOnlyList<MeshUser> ResolveDirectorySourceUsers(
        IReadOnlyList<MeshUser> eligibleUsers,
        IReadOnlyList<MeshGroup> groups,
        SyncRuleOptions rules)
    {
        if (string.IsNullOrWhiteSpace(rules.MainContactsGroupEmail))
        {
            return eligibleUsers;
        }

        var rootGroup = groups.FirstOrDefault(group => MatchesGroup(group, rules.MainContactsGroupEmail));
        if (rootGroup is null)
        {
            return Array.Empty<MeshUser>();
        }

        var memberKeys = ResolveUserMemberKeys(rootGroup, groups);

        return eligibleUsers
            .Where(user => memberKeys.Contains(user.Id) || memberKeys.Contains(user.Email))
            .ToList();
    }

    private static IReadOnlySet<string> ResolveUserMemberKeys(MeshGroup rootGroup, IReadOnlyList<MeshGroup> groups)
    {
        var memberKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingGroups = new Stack<MeshGroup>();

        pendingGroups.Push(rootGroup);

        while (pendingGroups.Count > 0)
        {
            var group = pendingGroups.Pop();
            if (!visitedGroupKeys.Add(GroupKey(group)))
            {
                continue;
            }

            foreach (var member in group.Members)
            {
                if (member.Type == MeshGroupMemberType.Group)
                {
                    var childGroup = groups.FirstOrDefault(candidate =>
                        MatchesGroup(candidate, member.Id) || MatchesGroup(candidate, member.Email));
                    if (childGroup is not null)
                    {
                        pendingGroups.Push(childGroup);
                    }

                    continue;
                }

                AddIfPresent(memberKeys, member.Id);
                AddIfPresent(memberKeys, member.Email);
            }
        }

        return memberKeys;
    }

    private static IReadOnlyList<MeshGroup> ResolveGroupsToSyncByGroup(
        IReadOnlyList<MeshGroup> groups,
        IReadOnlyList<string> groupIdsOrEmails)
    {
        if (groupIdsOrEmails.Count == 0)
        {
            return Array.Empty<MeshGroup>();
        }

        var groupContacts = new Dictionary<string, MeshGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var idOrEmail in groupIdsOrEmails.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var container = groups.FirstOrDefault(group => MatchesGroup(group, idOrEmail.Trim()));
            if (container is null)
            {
                continue;
            }

            foreach (var member in container.Members.Where(member => member.Type == MeshGroupMemberType.Group))
            {
                var group = groups.FirstOrDefault(candidate =>
                    MatchesGroup(candidate, member.Id) || MatchesGroup(candidate, member.Email))
                    ?? CreateGroupFromMember(member);
                if (group is null)
                {
                    continue;
                }

                groupContacts.TryAdd(GroupKey(group), group);
            }
        }

        return groupContacts.Values.ToList();
    }

    private static MeshGroup? CreateGroupFromMember(MeshGroupMember member)
    {
        if (string.IsNullOrWhiteSpace(member.Email))
        {
            return null;
        }

        var id = string.IsNullOrWhiteSpace(member.Id) ? member.Email : member.Id;

        return new MeshGroup
        {
            Id = id,
            Email = member.Email,
            DisplayName = member.DisplayName
        };
    }

    private static IReadOnlyList<MeshContact> DeduplicateContacts(IEnumerable<MeshContact> contacts)
    {
        var uniqueContacts = new Dictionary<string, MeshContact>(StringComparer.OrdinalIgnoreCase);

        foreach (var contact in contacts)
        {
            uniqueContacts.TryAdd(ContactKey(contact), contact);
        }

        return uniqueContacts.Values.ToList();
    }

    private static IReadOnlySet<string> ResolveUserIdsFromGroups(
        IReadOnlyList<MeshGroup> groups,
        IReadOnlyList<string> exclusionGroupIds)
    {
        if (exclusionGroupIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var exclusions = exclusionGroupIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return groups
            .Where(group => exclusions.Contains(group.Id) || exclusions.Contains(group.Email))
            .SelectMany(group => group.Members)
            .SelectMany(member => new[] { member.Id, member.Email })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> BuildTargetLabels(
        string directoryLabel,
        IReadOnlyList<MeshGroup> groupContactSourceGroups)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            directoryLabel
        };

        foreach (var group in groupContactSourceGroups)
        {
            foreach (var label in GetGroupContactLabels(group))
            {
                labels.Add(label);
            }
        }

        return labels;
    }

    private static IReadOnlySet<string> BuildDirectoryContactLabels(
        MeshUser user,
        string directoryLabel,
        IReadOnlyList<MeshGroup> groupContactSourceGroups,
        IReadOnlyList<MeshGroup> allGroups)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            directoryLabel
        };

        foreach (var group in groupContactSourceGroups)
        {
            if (!ContainsUserMember(group, user, allGroups))
            {
                continue;
            }

            foreach (var label in GetGroupContactLabels(group))
            {
                labels.Add(label);
            }
        }

        return labels;
    }

    private static bool ContainsUserMember(MeshGroup rootGroup, MeshUser user, IReadOnlyList<MeshGroup> allGroups)
    {
        var visitedGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingGroups = new Stack<MeshGroup>();
        pendingGroups.Push(rootGroup);

        while (pendingGroups.Count > 0)
        {
            var group = pendingGroups.Pop();
            if (!visitedGroupKeys.Add(GroupKey(group)))
            {
                continue;
            }

            foreach (var member in group.Members)
            {
                if (member.Type == MeshGroupMemberType.User && MatchesUser(member, user))
                {
                    return true;
                }

                if (member.Type != MeshGroupMemberType.Group)
                {
                    continue;
                }

                var childGroup = allGroups.FirstOrDefault(candidate =>
                    MatchesGroup(candidate, member.Id) || MatchesGroup(candidate, member.Email));
                if (childGroup is not null)
                {
                    pendingGroups.Push(childGroup);
                }
            }
        }

        return false;
    }

    private static IReadOnlyList<string> GetGroupContactLabels(MeshGroup group)
    {
        return new[] { group.DisplayName, group.Email, group.Id }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Take(1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveDirectoryLabel(SyncRuleOptions rules)
    {
        if (!string.IsNullOrWhiteSpace(rules.MainContactsGroupLabel))
        {
            return rules.MainContactsGroupLabel.Trim();
        }

        return DirectoryLabel;
    }

    private static string ResolveGroupContactPrefix(SyncRuleOptions rules)
    {
        return string.IsNullOrWhiteSpace(rules.GroupContactPrefix)
            ? string.Empty
            : rules.GroupContactPrefix.Trim();
    }

    private static SyncPlanner CreatePlanner(
        ContactMeshOptions options,
        IReadOnlyList<MeshGroup> groupContactSourceGroups,
        IReadOnlyList<string> additionalManagedMetadataKeys)
    {
        var managedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ResolveDirectoryLabel(options.Rules)
        };

        foreach (var group in groupContactSourceGroups)
        {
            foreach (var value in new[] { group.DisplayName, group.Email, group.Id })
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    managedLabels.Add(value);
                }
            }
        }

        var managedMetadataKeys = new StaleContactCleanupOptions()
            .ManagedMetadataKeys
            .Concat(additionalManagedMetadataKeys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SyncPlanner(
            mergeEngine: new ContactMergeEngine(options: new ContactMergeOptions
            {
                ManagedLabels = managedLabels,
                ForceResetLabels = options.ForceResetLabels
            }),
            staleContactCleanupEngine: new StaleContactCleanupEngine(new StaleContactCleanupOptions
            {
                ManagedEmailDomains = options.ManagedEmailDomains,
                ManagedLabels = managedLabels,
                ManagedMetadataKeys = managedMetadataKeys
            }),
            emailPolicyEngine: new ContactEmailPolicyEngine(new ContactEmailPolicyOptions
            {
                ManagedEmailDomains = options.ManagedEmailDomains
            }));
    }

    private static bool MatchesGroup(MeshGroup group, string idOrEmail)
    {
        return string.Equals(group.Id, idOrEmail, StringComparison.OrdinalIgnoreCase)
            || string.Equals(group.Email, idOrEmail, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesUser(MeshGroupMember member, MeshUser user)
    {
        return string.Equals(member.Id, user.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.Email, user.Email, StringComparison.OrdinalIgnoreCase);
    }

    private static string GroupKey(MeshGroup group)
    {
        return string.IsNullOrWhiteSpace(group.Id) ? group.Email : group.Id;
    }

    private static string ContactKey(MeshContact contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.SourceId))
        {
            return contact.SourceId;
        }

        var email = contact.Emails.FirstOrDefault(email => !string.IsNullOrWhiteSpace(email.Address))?.Address;
        return string.IsNullOrWhiteSpace(email) ? Guid.NewGuid().ToString("N") : email;
    }

    private static void AddIfPresent(ISet<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    private static MeshContact AddLabels(MeshContact contact, IReadOnlyList<string> labels)
    {
        return contact with
        {
            Labels = contact.Labels
                .Concat(labels)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static MeshContact AddGroupRuleMetadata(MeshContact contact, string sourceRule, MeshGroup group)
    {
        return AddMetadata(
            contact,
            (SourceRuleMetadataKey, sourceRule),
            (SourceGroupIdMetadataKey, group.Id),
            (SourceGroupEmailMetadataKey, group.Email),
            (SourceGroupDisplayNameMetadataKey, group.DisplayName));
    }

    private static MeshContact AddMetadata(MeshContact contact, params (string Key, string? Value)[] values)
    {
        var metadata = new Dictionary<string, string>(contact.Metadata, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        return contact with { Metadata = metadata };
    }

    private static bool IsTargetUser(MeshUser user, SyncTarget target)
    {
        return string.Equals(user.Id, target.UserId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Email, target.UserEmail, StringComparison.OrdinalIgnoreCase);
    }

    private static SyncResult CreateErrorResult(SyncTarget target, bool dryRun, Exception exception)
    {
        var message = $"Target sync failed for {target.UserId}: {exception.Message}";

        return new SyncResult
        {
            TargetUserId = target.UserId,
            DryRun = dryRun,
            Errors = new[] { message },
            LogEntries = new[]
            {
                new SyncLogEntry(
                    DateTimeOffset.UtcNow,
                    SyncLogLevel.Error,
                    message,
                    target.UserId)
            }
        };
    }
}
