using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules;

public sealed class SyncRuleEngine
{
    private readonly ExclusionRule exclusionRule;
    private readonly GroupVisibilityRule visibilityRule;
    private readonly OrganizationUnitRule organizationUnitRule;
    private readonly IReadOnlySet<string> targetUsers;

    public SyncRuleEngine(
        ExclusionRule? exclusionRule = null,
        GroupVisibilityRule? visibilityRule = null,
        OrganizationUnitRule? organizationUnitRule = null,
        IEnumerable<string>? targetUsers = null)
    {
        this.exclusionRule = exclusionRule ?? new ExclusionRule(Array.Empty<string>());
        this.visibilityRule = visibilityRule ?? new GroupVisibilityRule();
        this.organizationUnitRule = organizationUnitRule ?? new OrganizationUnitRule();
        this.targetUsers = NormalizeTargetUsers(targetUsers);
    }

    public IReadOnlyList<MeshUser> CreateEligibleUsers(IEnumerable<MeshUser> users)
    {
        return users
            .Where(user => !user.IsSuspended)
            .Where(user => !this.exclusionRule.IsExcluded(user))
            .Where(user => this.organizationUnitRule.Evaluate(user).IsIncluded)
            .ToList();
    }

    public IReadOnlyList<SyncTarget> CreateTargets(IEnumerable<MeshUser> users)
    {
        return this.CreateEligibleUsers(users)
            .Where(this.IsInTargetScope)
            .Select(user => new SyncTarget { UserId = user.Id, UserEmail = user.Email })
            .ToList();
    }

    public IReadOnlyList<MeshContact> FilterContactsForTarget(IEnumerable<MeshContact> contacts, SyncTarget target)
    {
        return contacts.Where(contact => this.visibilityRule.IsVisibleToTarget(contact, target)).ToList();
    }

    public IReadOnlyList<GroupVisibilityDecision> FilterGroupsForTarget(IEnumerable<MeshGroup> groups, SyncTarget target)
    {
        return this.visibilityRule.FilterVisibleGroups(groups, target);
    }

    private bool IsInTargetScope(MeshUser user)
    {
        return this.targetUsers.Count == 0
            || this.targetUsers.Contains(user.Id)
            || this.targetUsers.Contains(user.Email);
    }

    private static IReadOnlySet<string> NormalizeTargetUsers(IEnumerable<string>? targetUsers)
    {
        return (targetUsers ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
