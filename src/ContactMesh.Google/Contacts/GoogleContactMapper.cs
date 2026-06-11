// File: GoogleContactMapper.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Google.Contacts
{
    public static class GoogleContactMapper
    {
        public const string SourceIdClientDataKey = "contactmesh.sourceId";
        public const string ResourceNameMetadataKey = "google.people.resourceName";
        public const string ETagMetadataKey = "google.people.etag";

        public static MeshContact ToMeshContact(string sourceId, string displayName, string email)
        {
            return new MeshContact
            {
                SourceId = sourceId,
                DisplayName = displayName,
                Emails = new[] { new ContactEmail(email, "work", true) }
            };
        }

        public static MeshContact ToMeshContact(
            GooglePersonContact contact,
            IReadOnlyDictionary<string, string>? labelNamesByResourceName = null)
        {
            ArgumentNullException.ThrowIfNull(contact);

            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(contact.ResourceName))
            {
                metadata[ResourceNameMetadataKey] = contact.ResourceName;
            }

            if (!string.IsNullOrWhiteSpace(contact.ETag))
            {
                metadata[ETagMetadataKey] = contact.ETag;
            }

            return new MeshContact
            {
                SourceId = contact.SourceId,
                DisplayName = contact.DisplayName,
                GivenName = contact.GivenName,
                FamilyName = contact.FamilyName,
                CompanyName = contact.CompanyName,
                Department = contact.Department,
                JobTitle = contact.JobTitle,
                Emails = contact.Emails
                    .Select(email => new ContactEmail(email.Address, email.Type, email.IsPrimary))
                    .ToList(),
                Phones = contact.Phones
                    .Select(phone => new ContactPhone(phone.Number, phone.Type, phone.IsPrimary))
                    .ToList(),
                Labels = contact.ContactGroupResourceNames
                    .Select(resourceName => labelNamesByResourceName is not null
                        && labelNamesByResourceName.TryGetValue(resourceName, out var labelName)
                            ? labelName
                            : null)
                    .Where(labelName => !string.IsNullOrWhiteSpace(labelName))
                    .Select(labelName => labelName!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                Metadata = metadata
            };
        }

        public static GooglePersonContact ToGooglePersonContact(
            MeshContact contact,
            IReadOnlyDictionary<string, string>? labelResourceNamesByName = null)
        {
            ArgumentNullException.ThrowIfNull(contact);

            return new GooglePersonContact
            {
                ResourceName = TryGetMetadata(contact, ResourceNameMetadataKey),
                ETag = TryGetMetadata(contact, ETagMetadataKey),
                SourceId = contact.SourceId,
                DisplayName = contact.DisplayName,
                GivenName = contact.GivenName,
                FamilyName = contact.FamilyName,
                CompanyName = contact.CompanyName,
                Department = contact.Department,
                JobTitle = contact.JobTitle,
                Emails = contact.Emails
                    .Select(email => new GooglePersonEmail(email.Address, email.Type, email.IsPrimary))
                    .ToList(),
                Phones = contact.Phones
                    .Select(phone => new GooglePersonPhone(phone.Number, phone.Type, phone.IsPrimary))
                    .ToList(),
                ContactGroupResourceNames = contact.Labels
                    .Select(label => labelResourceNamesByName is not null
                        && labelResourceNamesByName.TryGetValue(label, out var resourceName)
                            ? resourceName
                            : null)
                    .Where(resourceName => !string.IsNullOrWhiteSpace(resourceName))
                    .Select(resourceName => resourceName!)
                    .ToList()
            };
        }

        private static string? TryGetMetadata(MeshContact contact, string key)
        {
            return contact.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }
    }
}
