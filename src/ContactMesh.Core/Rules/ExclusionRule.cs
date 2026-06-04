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
        return this.excludedUserIds.Contains(user.Id)
            || GetUserEmails(user).Any(this.excludedUserIds.Contains);
    }

    private static IEnumerable<string> GetUserEmails(MeshUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            yield return user.Email;
        }

        foreach (var email in user.AlternateEmails.Where(email => !string.IsNullOrWhiteSpace(email)))
        {
            yield return email;
        }
    }
}
