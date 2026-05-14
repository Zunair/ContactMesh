using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class DirectoryContactFactory
{
    public MeshContact CreateUserContact(MeshUser user, IEnumerable<string>? labels = null, string? emailOverride = null)
    {
        var email = string.IsNullOrWhiteSpace(emailOverride) ? user.Email : emailOverride;

        return new MeshContact
        {
            SourceId = user.Id,
            DisplayName = GetDisplayName(user),
            GivenName = user.GivenName,
            FamilyName = user.FamilyName,
            CompanyName = user.CompanyName,
            Department = user.Department,
            JobTitle = user.JobTitle,
            Emails = new[] { new ContactEmail(email, "work", true) },
            Phones = user.Phones.ToList(),
            Labels = (labels ?? Array.Empty<string>())
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            Metadata = CreateMetadata(user)
        };
    }

    private static string? GetDisplayName(MeshUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        var name = string.Join(" ", new[] { user.GivenName, user.FamilyName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(name) ? user.Email : name;
    }

    private static IDictionary<string, string> CreateMetadata(MeshUser user)
    {
        var metadata = new Dictionary<string, string>(user.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["userId"] = user.Id
        };

        if (!string.IsNullOrWhiteSpace(user.OrganizationUnit))
        {
            metadata["organizationUnit"] = user.OrganizationUnit;
        }

        return metadata;
    }
}
