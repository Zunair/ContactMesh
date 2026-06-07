using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Directory;

public static class MicrosoftDirectoryMapper
{
    public static MeshUser ToMeshUser(MicrosoftGraphUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var id = RequireValue(user.Id, "Graph user id");
        var identity = ResolveEmailIdentity(user.Mail, user.UserPrincipalName, user.ProxyAddresses);

        return new MeshUser
        {
            Id = id,
            Email = identity.PrimaryEmail,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            FamilyName = user.Surname,
            CompanyName = user.CompanyName,
            Department = user.Department,
            JobTitle = user.JobTitle,
            AlternateEmails = identity.AlternateEmails,
            Phones = ToPhones(user).ToList(),
            IsSuspended = user.AccountEnabled == false,
            IsExternal = IsExternalUser(user.UserType),
            Warnings = BuildWarnings("Microsoft 365 user", id, user.DisplayName, identity)
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

    private static bool IsExternalUser(string? userType) =>
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

    private static EmailIdentity ResolveEmailIdentity(
        string? mail,
        string? userPrincipalName,
        IReadOnlyList<string> proxyAddresses)
    {
        var primaryProxy = GetPrimaryProxyAddress(proxyAddresses);
        var primaryEmail = FirstValue(primaryProxy, mail, userPrincipalName);
        var alternates = GetDistinctEmails(new[] { mail, userPrincipalName }
                .Concat(GetSmtpProxyAddresses(proxyAddresses)))
            .Where(email => !string.Equals(email, primaryEmail, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new EmailIdentity(
            primaryEmail,
            mail?.Trim(),
            userPrincipalName?.Trim(),
            primaryProxy,
            alternates);
    }

    private static IReadOnlyList<string> BuildWarnings(
        string subject,
        string id,
        string? displayName,
        EmailIdentity identity)
    {
        var compared = GetDistinctEmails(new[]
        {
            identity.PrimaryEmail,
            identity.Mail,
            identity.UserPrincipalName,
            identity.PrimaryProxyAddress
        });
        if (compared.Count <= 1)
        {
            return Array.Empty<string>();
        }

        var name = string.IsNullOrWhiteSpace(displayName) ? id : $"{displayName.Trim()} ({id})";
        return new[]
        {
            $"{subject} {name} has mismatched email identity values: selectedPrimary={identity.PrimaryEmail}; mail={identity.Mail ?? "(blank)"}; userPrincipalName={identity.UserPrincipalName ?? "(blank)"}; primaryProxy={identity.PrimaryProxyAddress ?? "(blank)"}. ContactMesh will match aliases, but account cleanup may be needed."
        };
    }

    private static string? GetPrimaryProxyAddress(IEnumerable<string> proxyAddresses)
    {
        return proxyAddresses
            .Select(address => address?.Trim())
            .FirstOrDefault(address => address is not null && address.StartsWith("SMTP:", StringComparison.Ordinal))
            ?[5..];
    }

    private static IEnumerable<string> GetSmtpProxyAddresses(IEnumerable<string> proxyAddresses)
    {
        foreach (var proxyAddress in proxyAddresses)
        {
            var value = proxyAddress?.Trim();
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("smtp:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var email = value[5..].Trim();
            if (!string.IsNullOrWhiteSpace(email))
            {
                yield return email;
            }
        }
    }

    private static IReadOnlyList<string> GetDistinctEmails(IEnumerable<string?> emails)
    {
        return emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private sealed record EmailIdentity(
        string PrimaryEmail,
        string? Mail,
        string? UserPrincipalName,
        string? PrimaryProxyAddress,
        IReadOnlyList<string> AlternateEmails);
}
