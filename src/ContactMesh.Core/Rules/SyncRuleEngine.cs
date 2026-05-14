using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules;

public sealed class SyncRuleEngine
{
    private readonly ExclusionRule exclusionRule;
    private readonly GroupVisibilityRule visibilityRule;

    public SyncRuleEngine(ExclusionRule? exclusionRule = null, GroupVisibilityRule? visibilityRule = null)
    {
        this.exclusionRule = exclusionRule ?? new ExclusionRule(Array.Empty<string>());
        this.visibilityRule = visibilityRule ?? new GroupVisibilityRule();
    }

    public IReadOnlyList<SyncTarget> CreateTargets(IEnumerable<MeshUser> users)
    {
        return users
            .Where(user => !user.IsSuspended)
            .Where(user => !this.exclusionRule.IsExcluded(user))
            .Select(user => new SyncTarget { UserId = user.Id, UserEmail = user.Email })
            .ToList();
    }

    public IReadOnlyList<MeshContact> FilterContactsForTarget(IEnumerable<MeshContact> contacts, SyncTarget target)
    {
        return contacts.Where(contact => this.visibilityRule.IsVisibleToTarget(contact, target)).ToList();
    }
}
