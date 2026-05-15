using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Contacts;

public static class MicrosoftContactMapper
{
    private const string ContactMeshExtendedPropertySetId = "c7d37545-2710-4a53-a25d-dbbf5ca1d607";

    public const string SourceIdExtendedPropertyId = $"String {{{ContactMeshExtendedPropertySetId}}} Name contactmesh.sourceId";
    public const string ContactIdMetadataKey = "microsoft.graph.contactId";
    public const string ChangeKeyMetadataKey = "microsoft.graph.changeKey";

    public static MeshContact ToMeshContact(string sourceId, string displayName, string email)
    {
        return new MeshContact
        {
            SourceId = sourceId,
            DisplayName = displayName,
            Emails = new[] { new ContactEmail(email, "work", true) }
        };
    }

    public static MeshContact ToMeshContact(MicrosoftGraphContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(contact.Id))
        {
            metadata[ContactIdMetadataKey] = contact.Id;
        }

        if (!string.IsNullOrWhiteSpace(contact.ChangeKey))
        {
            metadata[ChangeKeyMetadataKey] = contact.ChangeKey;
        }

        return new MeshContact
        {
            SourceId = contact.SourceId,
            DisplayName = contact.DisplayName,
            GivenName = contact.GivenName,
            FamilyName = contact.Surname,
            CompanyName = contact.CompanyName,
            Department = contact.Department,
            JobTitle = contact.JobTitle,
            Notes = contact.PersonalNotes,
            Emails = contact.EmailAddresses
                .Where(email => !string.IsNullOrWhiteSpace(email.Address))
                .Select((email, index) => new ContactEmail(email.Address.Trim(), "work", index == 0))
                .ToList(),
            Phones = ToMeshPhones(contact).ToList(),
            Labels = contact.Categories
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            Metadata = metadata
        };
    }

    public static MicrosoftGraphContact ToMicrosoftGraphContact(MeshContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new MicrosoftGraphContact
        {
            Id = TryGetMetadata(contact, ContactIdMetadataKey),
            ChangeKey = TryGetMetadata(contact, ChangeKeyMetadataKey),
            SourceId = contact.SourceId,
            DisplayName = contact.DisplayName,
            GivenName = contact.GivenName,
            Surname = contact.FamilyName,
            CompanyName = contact.CompanyName,
            Department = contact.Department,
            JobTitle = contact.JobTitle,
            PersonalNotes = contact.Notes,
            EmailAddresses = contact.Emails
                .Where(email => !string.IsNullOrWhiteSpace(email.Address))
                .Select(email => new MicrosoftGraphEmailAddress(email.Address.Trim(), contact.DisplayName))
                .ToList(),
            BusinessPhones = contact.Phones
                .Where(phone => !string.IsNullOrWhiteSpace(phone.Number)
                    && !string.Equals(phone.Type, "mobile", StringComparison.OrdinalIgnoreCase))
                .Select(phone => phone.Number.Trim())
                .ToList(),
            MobilePhone = contact.Phones
                .FirstOrDefault(phone => !string.IsNullOrWhiteSpace(phone.Number)
                    && string.Equals(phone.Type, "mobile", StringComparison.OrdinalIgnoreCase))
                ?.Number
                .Trim(),
            Categories = contact.Labels
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static IEnumerable<ContactPhone> ToMeshPhones(MicrosoftGraphContact contact)
    {
        foreach (var phone in contact.BusinessPhones.Where(phone => !string.IsNullOrWhiteSpace(phone)))
        {
            yield return new ContactPhone(phone.Trim(), "work");
        }

        if (!string.IsNullOrWhiteSpace(contact.MobilePhone))
        {
            yield return new ContactPhone(contact.MobilePhone.Trim(), "mobile");
        }
    }

    private static string? TryGetMetadata(MeshContact contact, string key)
    {
        return contact.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
