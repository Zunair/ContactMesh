using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class ContactEmailPolicyEngine
{
    private readonly ContactEmailPolicyOptions options;

    public ContactEmailPolicyEngine(ContactEmailPolicyOptions? options = null)
    {
        this.options = options ?? new ContactEmailPolicyOptions();
    }

    public MeshContact Apply(MeshContact contact, string? resolvedSendAsEmail = null)
    {
        var deduplicated = DeduplicateEmails(contact.Emails);
        var primaryIndex = GetPrimaryEmailIndex(deduplicated, resolvedSendAsEmail);

        if (primaryIndex < 0)
        {
            return contact with { Emails = deduplicated };
        }

        var updatedEmails = deduplicated
            .Select((email, index) => email with
            {
                Type = index == primaryIndex ? "work" : email.Type,
                IsPrimary = index == primaryIndex
            })
            .ToList();

        return contact with { Emails = updatedEmails };
    }

    private static IReadOnlyList<ContactEmail> DeduplicateEmails(IEnumerable<ContactEmail> emails)
    {
        return emails
            .Where(email => !string.IsNullOrWhiteSpace(email.Address))
            .GroupBy(email => email.Address.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(email => email.IsPrimary).First())
            .ToList();
    }

    private int GetPrimaryEmailIndex(IReadOnlyList<ContactEmail> emails, string? resolvedSendAsEmail)
    {
        if (!string.IsNullOrWhiteSpace(resolvedSendAsEmail))
        {
            var sendAsIndex = FindEmailIndex(emails, resolvedSendAsEmail);
            if (sendAsIndex >= 0)
            {
                return sendAsIndex;
            }
        }

        var managedIndex = FindManagedEmailIndex(emails);

        if (managedIndex >= 0)
        {
            return managedIndex;
        }

        if (this.options.ForceNormalizeEmailTypes && emails.Count > 0)
        {
            return FindPrimaryEmailIndex(emails) is var primaryIndex && primaryIndex >= 0
                ? primaryIndex
                : 0;
        }

        return FindPrimaryEmailIndex(emails);
    }

    private bool IsManagedEmail(ContactEmail email)
    {
        return this.options.ManagedEmailDomains.Any(domain =>
            email.Address.EndsWith(NormalizeDomain(domain), StringComparison.OrdinalIgnoreCase));
    }

    private static int FindEmailIndex(IReadOnlyList<ContactEmail> emails, string address)
    {
        for (var index = 0; index < emails.Count; index++)
        {
            if (string.Equals(emails[index].Address, address, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindManagedEmailIndex(IReadOnlyList<ContactEmail> emails)
    {
        for (var index = 0; index < emails.Count; index++)
        {
            if (IsManagedEmail(emails[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindPrimaryEmailIndex(IReadOnlyList<ContactEmail> emails)
    {
        for (var index = 0; index < emails.Count; index++)
        {
            if (emails[index].IsPrimary)
            {
                return index;
            }
        }

        return -1;
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.StartsWith('@') ? domain : $"@{domain}";
    }
}
