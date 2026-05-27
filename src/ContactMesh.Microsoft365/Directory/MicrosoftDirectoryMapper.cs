using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Directory;

public static class MicrosoftDirectoryMapper
{
    public static MeshUser ToMeshUser(MicrosoftGraphUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var id = RequireValue(user.Id, "Graph user id");
        var email = FirstValue(user.Mail, user.UserPrincipalName);

        return new MeshUser
        {
            Id = id,
            Email = email,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            FamilyName = user.Surname,
            CompanyName = user.CompanyName,
            Department = user.Department,
            JobTitle = user.JobTitle,
            Phones = ToPhones(user).ToList(),
            IsSuspended = user.AccountEnabled == false || IsGuestUser(user.UserType)
        };
    }

    public static MeshUser ToMeshUser(
        string id,
        string email,
        string? displayName = null,
        string? givenName = null,
        string? familyName = null,
        string? organizationUnit = null)
    {
        return new MeshUser
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            GivenName = givenName,
            FamilyName = familyName,
            OrganizationUnit = organizationUnit
        };
    }

    private static bool IsGuestUser(string? userType) =>
        !string.IsNullOrWhiteSpace(userType) &&
        !string.Equals(userType, "Member", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ContactPhone> ToPhones(MicrosoftGraphUser user)
    {
        foreach (var phone in user.BusinessPhones.Where(phone => !string.IsNullOrWhiteSpace(phone)))
        {
            yield return new ContactPhone(phone.Trim(), "work");
        }

        if (!string.IsNullOrWhiteSpace(user.MobilePhone))
        {
            yield return new ContactPhone(user.MobilePhone.Trim(), "mobile");
        }
    }

    private static string FirstValue(params string?[] values)
    {
        var value = values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Graph user mail or userPrincipalName must be present.");
        }

        return value;
    }

    private static string RequireValue(string? value, string name)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{name} must be present.");
        }

        return trimmed;
    }
}
