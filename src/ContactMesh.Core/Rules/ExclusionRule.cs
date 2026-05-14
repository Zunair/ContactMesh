using ContactMesh.Core.Models;

namespace ContactMesh.Core.Rules;

public sealed class ExclusionRule
{
    private readonly IReadOnlySet<string> excludedUserIds;

    public ExclusionRule(IEnumerable<string> excludedUserIds)
    {
        this.excludedUserIds = excludedUserIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsExcluded(MeshUser user)
    {
        return this.excludedUserIds.Contains(user.Id) || this.excludedUserIds.Contains(user.Email);
    }
}
