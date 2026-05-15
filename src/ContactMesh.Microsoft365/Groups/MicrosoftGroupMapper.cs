using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Groups;

public static class MicrosoftGroupMapper
{
    public const string OrgContactIdMetadataKey = "microsoft.graph.orgContactId";

    public static MeshGroup ToMeshGroup(
        MicrosoftGraphGroup group,
        IEnumerable<MicrosoftGraphGroupMember> members)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(members);

        return new MeshGroup
        {
            Id = RequireValue(group.Id, "Graph group id"),
            Email = RequireValue(group.Mail, "Graph group mail"),
            DisplayName = group.DisplayName,
            GroupVisibility = ToGroupVisibility(group.Visibility),
            MemberVisibility = ToMemberVisibility(group.Visibility),
            Members = members
                .Select(ToMeshGroupMember)
                .OfType<MeshGroupMember>()
                .ToList()
        };
    }

    public static MeshGroupMember? ToMeshGroupMember(MicrosoftGraphGroupMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var id = member.Id?.Trim();
        var email = FirstValueOrNull(member.Mail, member.UserPrincipalName);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new MeshGroupMember
        {
            Id = id,
            Email = email,
            DisplayName = member.DisplayName,
            Type = ToMemberType(member.ODataType)
        };
    }

    public static MeshContact ToMeshContact(MicrosoftGraphGroupMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var id = RequireValue(member.Id, "Graph organizational contact id");
        var email = RequireValue(member.Mail, "Graph organizational contact mail");

        return new MeshContact
        {
            SourceId = $"orgContact:{id}",
            DisplayName = member.DisplayName,
            GivenName = member.GivenName,
            FamilyName = member.Surname,
            CompanyName = member.CompanyName,
            Department = member.Department,
            JobTitle = member.JobTitle,
            Emails = new[] { new ContactEmail(email, "work", true) },
            Phones = ToPhones(member).ToList(),
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [OrgContactIdMetadataKey] = id
            }
        };
    }

    public static bool IsOrgContact(MicrosoftGraphGroupMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return string.Equals(
            NormalizeODataType(member.ODataType),
            "microsoft.graph.orgContact",
            StringComparison.OrdinalIgnoreCase);
    }

    public static MeshGroupVisibility ToGroupVisibility(string? visibility)
    {
        return visibility?.Trim().ToUpperInvariant() switch
        {
            "PUBLIC" => MeshGroupVisibility.Domain,
            "PRIVATE" => MeshGroupVisibility.Members,
            "HIDDENMEMBERSHIP" => MeshGroupVisibility.Members,
            _ => MeshGroupVisibility.Hidden
        };
    }

    public static MeshGroupVisibility ToMemberVisibility(string? visibility)
    {
        return visibility?.Trim().ToUpperInvariant() switch
        {
            "PUBLIC" => MeshGroupVisibility.Domain,
            "PRIVATE" => MeshGroupVisibility.Members,
            "HIDDENMEMBERSHIP" => MeshGroupVisibility.Hidden,
            _ => MeshGroupVisibility.Hidden
        };
    }

    private static MeshGroupMemberType ToMemberType(string? odataType)
    {
        return NormalizeODataType(odataType) switch
        {
            "microsoft.graph.user" => MeshGroupMemberType.User,
            "microsoft.graph.group" => MeshGroupMemberType.Group,
            "microsoft.graph.orgContact" => MeshGroupMemberType.Contact,
            _ => MeshGroupMemberType.Unknown
        };
    }

    private static IEnumerable<ContactPhone> ToPhones(MicrosoftGraphGroupMember member)
    {
        foreach (var phone in member.BusinessPhones.Where(phone => !string.IsNullOrWhiteSpace(phone)))
        {
            yield return new ContactPhone(phone.Trim(), "work");
        }

        if (!string.IsNullOrWhiteSpace(member.MobilePhone))
        {
            yield return new ContactPhone(member.MobilePhone.Trim(), "mobile");
        }
    }

    private static string? NormalizeODataType(string? odataType)
    {
        var value = odataType?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.StartsWith('#') ? value[1..] : value;
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

    private static string? FirstValueOrNull(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
