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
    private readonly IReadOnlyList<string> additionalOperationalMetadataKeys;

    public ContactSyncOrchestrator(
        IDirectoryProvider directoryProvider,
        IGroupProvider groupProvider,
        IContactProvider contactProvider,
        DirectoryContactFactory? directoryContactFactory = null,
        GroupContactFactory? groupContactFactory = null,
        GroupMappingEngine? groupMappingEngine = null,
        IEnumerable<string>? additionalManagedMetadataKeys = null,
        IEnumerable<string>? additionalOperationalMetadataKeys = null)
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
        this.additionalOperationalMetadataKeys = additionalOperationalMetadataKeys is null
            ? Array.Empty<string>()
            : additionalOperationalMetadataKeys.ToList();
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
        var groupContactSources = ResolveGroupsToSyncByGroup(
            mappedGroups,
            options.Rules.GroupsToSyncByGroup);
        var excludedUsers = ResolveUserIdsFromGroups(mappedGroups, options.Rules.ExclusionGroups);
        var globalGroupUsers = ResolveUserIdsFromGroups(mappedGroups, options.Rules.GlobalUserGroups);
        var effectiveTargetUsers = options.Rules.TargetUsers
            .Concat(globalGroupUsers)
            .ToList();
        var ruleEngine = new SyncRuleEngine(
            new ExclusionRule(excludedUsers),
            organizationUnitRule: new OrganizationUnitRule(
                options.Rules.IncludedOrganizationUnits,
                options.Rules.ExcludedOrganizationUnits),
            targetUsers: effectiveTargetUsers);

        var eligibleUsers = ruleEngine.CreateEligibleUsers(users);
        var sourceUsers = ResolveDirectorySourceUsers(eligibleUsers, mappedGroups, options.Rules);
        var directoryLabel = ResolveDirectoryLabel(options.Rules);
        var groupContactPrefix = ResolveGroupContactPrefix(options.Rules);
        var targetUsers = ruleEngine.CreateTargets(users)
            .ToDictionary(target => target.UserId, StringComparer.OrdinalIgnoreCase);
        var targetEligibleUsers = eligibleUsers
            .Where(user => targetUsers.ContainsKey(user.Id))
            .ToList();

        await ReportProgressAsync(
            progress,
            new SyncProgress(
                SyncProgressKind.RunStarted,
                TargetUserId: string.Empty,
                TargetUserEmail: null,
                TargetIndex: 0,
                TargetCount: targetEligibleUsers.Count,
                Message: BuildScopeDescription(options.Rules, globalGroupUsers, eligibleUsers.Count)),
            cancellationToken).ConfigureAwait(false);

        var results = new List<SyncResult>();
        var planner = CreatePlanner(options, groupContactSources, this.additionalManagedMetadataKeys, this.additionalOperationalMetadataKeys);
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
                LabelNames = BuildTargetLabels(directoryLabel, groupContactSources)
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
                    groupContactSources,
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
        IReadOnlyList<GroupContactSource> groupContactSources,
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
                    BuildDirectoryContactLabels(user, directoryLabel)),
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

        foreach (var entry in groupContactSources)
        {
            contacts.Add(AddGroupRuleMetadata(
                this.groupContactFactory.CreateGroupContact(entry.Group, new[] { entry.LabelName }, groupContactPrefix),
                "GroupsToSyncByGroup",
                entry.Group));
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

    // Level 1 = configured container group; Level 2 = direct group members that become the label;
    // Level 3 = direct group members of each Level-2 group that become the contact entries.
    private sealed record GroupContactSource(MeshGroup Group, string LabelName);

    private static IReadOnlyList<GroupContactSource> ResolveGroupsToSyncByGroup(
        IReadOnlyList<MeshGroup> groups,
        IReadOnlyList<string> groupIdsOrEmails)
    {
        if (groupIdsOrEmails.Count == 0)
        {
            return Array.Empty<GroupContactSource>();
        }

        var contactSources = new Dictionary<string, GroupContactSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var idOrEmail in groupIdsOrEmails.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            // Level 1: the configured container group
            var container = groups.FirstOrDefault(group => MatchesGroup(group, idOrEmail.Trim()));
            if (container is null)
            {
                continue;
            }

            // Level 2: direct group members of the container – their display name becomes the label
            foreach (var labelMember in container.Members.Where(m => m.Type == MeshGroupMemberType.Group))
            {
                var labelGroup = groups.FirstOrDefault(candidate =>
                    MatchesGroup(candidate, labelMember.Id) || MatchesGroup(candidate, labelMember.Email));
                var labelName =
                    labelGroup?.DisplayName ??
                    labelMember.DisplayName ??
                    labelMember.Email;
                var labelSource = labelGroup ?? CreateGroupFromMember(labelMember);
                if (labelSource is null)
                {
                    continue;
                }

                // Level 3: direct group members of the label group – these become the contact entries
                foreach (var contactMember in labelSource.Members.Where(m => m.Type == MeshGroupMemberType.Group))
                {
                    var contactGroup = groups.FirstOrDefault(candidate =>
                        MatchesGroup(candidate, contactMember.Id) || MatchesGroup(candidate, contactMember.Email))
                        ?? CreateGroupFromMember(contactMember);
                    if (contactGroup is null)
                    {
                        continue;
                    }

                    var key = GroupKey(contactGroup);
                    contactSources.TryAdd(key, new GroupContactSource(contactGroup, labelName ?? contactGroup.Email));
                }
            }
        }

        return contactSources.Values.ToList();
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

    private static string BuildScopeDescription(
        SyncRuleOptions rules,
        IReadOnlySet<string> globalGroupUserIds,
        int eligibleCount)
    {
        var parts = new List<string>();

        if (rules.TargetUsers.Count > 0)
        {
            parts.Add($"TargetUsers [{string.Join(", ", rules.TargetUsers)}]");
        }

        if (rules.GlobalUserGroups.Count > 0)
        {
            var groupList = string.Join(", ", rules.GlobalUserGroups);
            if (globalGroupUserIds.Count == 0)
            {
                parts.Add($"GlobalUserGroups [{groupList}] (no members found - check GroupTypes config)");
            }
            else
            {
                parts.Add($"GlobalUserGroups [{groupList}]");
            }
        }

        var scope = parts.Count > 0
            ? string.Join(" + ", parts)
            : $"all {eligibleCount} eligible directory users";

        return scope;
    }

    private static IReadOnlySet<string> BuildTargetLabels(
        string directoryLabel,
        IReadOnlyList<GroupContactSource> groupContactSources)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            directoryLabel
        };

        foreach (var entry in groupContactSources)
        {
            labels.Add(entry.LabelName);
        }

        return labels;
    }

    private static IReadOnlySet<string> BuildDirectoryContactLabels(
        MeshUser user,
        string directoryLabel)
    {
        // Level-3 group members (users inside GroupsToSyncByGroup contact groups) are not labeled;
        // only the directory label applies to directory-sourced contacts.
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            directoryLabel
        };
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
        IReadOnlyList<GroupContactSource> groupContactSources,
        IReadOnlyList<string> additionalManagedMetadataKeys,
        IReadOnlyList<string> additionalOperationalMetadataKeys)
    {
        var managedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ResolveDirectoryLabel(options.Rules)
        };

        foreach (var entry in groupContactSources)
        {
            managedLabels.Add(entry.LabelName);
            foreach (var value in new[] { entry.Group.Email, entry.Group.Id })
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

        var operationalMetadataKeys = additionalOperationalMetadataKeys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SyncPlanner(
            mergeEngine: new ContactMergeEngine(options: new ContactMergeOptions
            {
                ManagedLabels = managedLabels,
                ForceResetLabels = options.ForceResetLabels,
                ForceDeduplicatePhones = options.ForceDeduplicatePhones
            }),
            staleContactCleanupEngine: new StaleContactCleanupEngine(new StaleContactCleanupOptions
            {
                ManagedEmailDomains = options.ManagedEmailDomains,
                ManagedLabels = managedLabels,
                ManagedMetadataKeys = managedMetadataKeys,
                ForceResetLabels = options.ForceResetLabels,
                OperationalMetadataKeys = operationalMetadataKeys
            }),
            emailPolicyEngine: new ContactEmailPolicyEngine(new ContactEmailPolicyOptions
            {
                ManagedEmailDomains = options.ManagedEmailDomains,
                ForceNormalizeEmailTypes = options.ForceNormalizeEmailTypes
            }));
    }

    private static bool MatchesGroup(MeshGroup group, string idOrEmail)
    {
        return string.Equals(group.Id, idOrEmail, StringComparison.OrdinalIgnoreCase)
            || string.Equals(group.Email, idOrEmail, StringComparison.OrdinalIgnoreCase);
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
            TargetUserEmail = target.UserEmail,
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
