using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Logging;
using ContactMesh.Core.Models;
using ContactMesh.Core.Rules;

namespace ContactMesh.Core.Sync;

public sealed class ContactSyncOrchestrator
{
    public const string DirectoryLabel = "Directory";

    private readonly IDirectoryProvider directoryProvider;
    private readonly IGroupProvider groupProvider;
    private readonly IContactProvider contactProvider;
    private readonly DirectoryContactFactory directoryContactFactory;
    private readonly GroupContactFactory groupContactFactory;
    private readonly GroupMappingEngine groupMappingEngine;
    private readonly SyncPlanner planner;

    public ContactSyncOrchestrator(
        IDirectoryProvider directoryProvider,
        IGroupProvider groupProvider,
        IContactProvider contactProvider,
        DirectoryContactFactory? directoryContactFactory = null,
        GroupContactFactory? groupContactFactory = null,
        GroupMappingEngine? groupMappingEngine = null,
        SyncPlanner? planner = null)
    {
        this.directoryProvider = directoryProvider;
        this.groupProvider = groupProvider;
        this.contactProvider = contactProvider;
        this.directoryContactFactory = directoryContactFactory ?? new DirectoryContactFactory();
        this.groupContactFactory = groupContactFactory ?? new GroupContactFactory();
        this.groupMappingEngine = groupMappingEngine ?? new GroupMappingEngine();
        this.planner = planner ?? new SyncPlanner();
    }

    public async Task<ContactSyncRunResult> RunAsync(ContactMeshOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var users = await this.directoryProvider.GetUsersAsync(cancellationToken).ConfigureAwait(false);
        var groups = await this.groupProvider.GetGroupsAsync(cancellationToken).ConfigureAwait(false);
        var mappedGroups = this.groupMappingEngine.ApplyMappings(groups, options.Rules.GroupMappings);
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
        var targetUsers = ruleEngine.CreateTargets(users)
            .ToDictionary(target => target.UserId, StringComparer.OrdinalIgnoreCase);
        var targetEligibleUsers = eligibleUsers
            .Where(user => targetUsers.ContainsKey(user.Id))
            .ToList();

        var results = new List<SyncResult>();
        var syncEngine = new ContactSyncEngine(
            this.contactProvider,
            this.planner,
            new SyncExecutor(this.contactProvider));

        foreach (var targetUser in targetEligibleUsers)
        {
            var baseTarget = targetUsers[targetUser.Id];
            var visibleGroups = ruleEngine.FilterGroupsForTarget(mappedGroups, baseTarget);
            var target = baseTarget with
            {
                LabelNames = BuildTargetLabels(visibleGroups, directoryLabel)
            };
            var desiredContacts = await this.CreateDesiredContactsAsync(
                sourceUsers,
                target,
                visibleGroups,
                ruleEngine,
                directoryLabel,
                cancellationToken).ConfigureAwait(false);

            try
            {
                var result = await syncEngine.SyncAsync(
                    target,
                    desiredContacts,
                    options.DryRun,
                    cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(CreateErrorResult(target, options.DryRun, ex));
            }
        }

        return new ContactSyncRunResult
        {
            DryRun = options.DryRun,
            Results = results
        };
    }

    private async Task<IReadOnlyList<MeshContact>> CreateDesiredContactsAsync(
        IReadOnlyList<MeshUser> eligibleUsers,
        SyncTarget target,
        IReadOnlyList<GroupVisibilityDecision> visibleGroups,
        SyncRuleEngine ruleEngine,
        string directoryLabel,
        CancellationToken cancellationToken)
    {
        var contacts = eligibleUsers
            .Where(user => !IsTargetUser(user, target))
            .Select(user => this.directoryContactFactory.CreateUserContact(user, new[] { directoryLabel }))
            .ToList();

        foreach (var decision in visibleGroups)
        {
            var groupLabels = GetGroupLabels(decision.Group);
            contacts.Add(this.groupContactFactory.CreateGroupContact(decision.Group, groupLabels));

            if (decision.CanSeeMembers)
            {
                var groupContacts = await this.groupProvider.GetGroupContactsAsync(decision.Group.Id, cancellationToken)
                    .ConfigureAwait(false);
                contacts.AddRange(groupContacts.Select(contact => AddLabels(contact, groupLabels)));
            }
        }

        return ruleEngine.FilterContactsForTarget(contacts, target);
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
        IReadOnlyList<GroupVisibilityDecision> visibleGroups,
        string directoryLabel)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            directoryLabel
        };

        foreach (var decision in visibleGroups)
        {
            foreach (var label in GetGroupLabels(decision.Group))
            {
                labels.Add(label);
            }
        }

        return labels;
    }

    private static IReadOnlyList<string> GetGroupLabels(MeshGroup group)
    {
        return new[] { group.Email, group.Id }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveDirectoryLabel(SyncRuleOptions rules)
    {
        if (!string.IsNullOrWhiteSpace(rules.MainContactsGroupLabel))
        {
            return rules.MainContactsGroupLabel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(rules.MainContactsGroupLable))
        {
            return rules.MainContactsGroupLable.Trim();
        }

        return DirectoryLabel;
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
