using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class StaleContactCleanupEngine
{
    private readonly StaleContactCleanupOptions options;

    public StaleContactCleanupEngine(StaleContactCleanupOptions? options = null)
    {
        this.options = options ?? new StaleContactCleanupOptions();
    }

    public StaleContactCleanupResult Clean(MeshContact contact)
    {
        var cleaned = contact with
        {
            SourceId = null,
            DisplayName = null,
            GivenName = null,
            FamilyName = null,
            CompanyName = null,
            Department = null,
            JobTitle = null,
            Emails = contact.Emails.Where(email => !IsManagedEmail(email)).ToList(),
            Phones = contact.Phones.Where(phone => !IsManagedPhone(phone)).ToList(),
            Labels = contact.Labels
                .Where(label => !this.options.ManagedLabels.Contains(label))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            Metadata = contact.Metadata
                .Where(item => !this.options.ManagedMetadataKeys.Contains(item.Key))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
        };

        return HasUserOwnedData(cleaned)
            ? new StaleContactCleanupResult
            {
                ShouldDelete = false,
                Contact = cleaned,
                Reason = $"Managed contact is stale; preserving user-owned details ({DescribeUserOwnedData(cleaned)}) and removing managed fields."
            }
            : new StaleContactCleanupResult
            {
                ShouldDelete = true,
                Contact = contact,
                Reason = "Managed contact is stale and has no user-owned details."
            };
    }

    public bool HasManagedEmail(MeshContact contact)
    {
        return contact.Emails.Any(IsManagedEmail);
    }

    private bool IsManagedEmail(ContactEmail email)
    {
        return this.options.ManagedEmailDomains.Any(domain =>
            email.Address.EndsWith(NormalizeDomain(domain), StringComparison.OrdinalIgnoreCase));
    }

    private bool IsManagedPhone(ContactPhone phone)
    {
        return this.options.ManagedPhoneTypes.Contains(phone.Type);
    }

    private static bool HasUserOwnedData(MeshContact contact)
    {
        return contact.Emails.Count > 0
            || contact.Phones.Count > 0
            || contact.Labels.Count > 0
            || contact.Metadata.Count > 0
            || !string.IsNullOrWhiteSpace(contact.Notes);
    }

    private static string DescribeUserOwnedData(MeshContact contact)
    {
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(contact.Notes))
        {
            details.Add("notes");
        }

        if (contact.Emails.Count > 0)
        {
            details.Add(DescribeCount(contact.Emails.Count, "email"));
        }

        if (contact.Phones.Count > 0)
        {
            details.Add(DescribeCount(contact.Phones.Count, "phone"));
        }

        if (contact.Labels.Count > 0)
        {
            details.Add(DescribeCount(contact.Labels.Count, "label"));
        }

        if (contact.Metadata.Count > 0)
        {
            details.Add(DescribeCount(contact.Metadata.Count, "metadata field"));
        }

        return string.Join(", ", details);
    }

    private static string DescribeCount(int count, string singular)
    {
        return count == 1 ? $"1 {singular}" : $"{count} {singular}s";
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.StartsWith('@') ? domain : $"@{domain}";
    }
}
