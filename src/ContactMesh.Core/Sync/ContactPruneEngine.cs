using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class ContactPruneEngine
{
    private readonly ContactPruneOptions options;

    public ContactPruneEngine(ContactPruneOptions? options = null)
    {
        this.options = options ?? new ContactPruneOptions();
    }

    public IReadOnlyList<SyncOperation> CreatePlan(IEnumerable<MeshContact> contacts)
    {
        return contacts
            .Select(CreateDeleteOperation)
            .Where(operation => operation is not null)
            .Cast<SyncOperation>()
            .ToList();
    }

    public bool IsBlank(MeshContact contact)
    {
        return HasNoOrganization(contact)
            && contact.Emails.Count == 0
            && contact.Phones.Count == 0
            && contact.Labels.Count == 0
            && string.IsNullOrWhiteSpace(contact.Notes);
    }

    public bool ContainsOnlyManagedEmail(MeshContact contact)
    {
        return HasNoOrganization(contact)
            && contact.Emails.Count == 1
            && IsManagedEmail(contact.Emails[0])
            && contact.Phones.Count == 0
            && contact.Labels.Count == 0
            && string.IsNullOrWhiteSpace(contact.Notes);
    }

    private SyncOperation? CreateDeleteOperation(MeshContact contact)
    {
        if (IsBlank(contact))
        {
            return Delete(contact, "Contact is blank.");
        }

        if (ContainsOnlyManagedEmail(contact))
        {
            return Delete(contact, "Contact only contains a managed-domain email.");
        }

        return null;
    }

    private bool IsManagedEmail(ContactEmail email)
    {
        return this.options.ManagedEmailDomains.Any(domain =>
            email.Address.EndsWith(NormalizeDomain(domain), StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasNoOrganization(MeshContact contact)
    {
        return string.IsNullOrWhiteSpace(contact.CompanyName)
            && string.IsNullOrWhiteSpace(contact.Department)
            && string.IsNullOrWhiteSpace(contact.JobTitle);
    }

    private static SyncOperation Delete(MeshContact contact, string reason)
    {
        return new SyncOperation
        {
            OperationType = SyncOperationType.Delete,
            DesiredContact = contact,
            ExistingContact = contact,
            Reason = reason
        };
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.StartsWith('@') ? domain : $"@{domain}";
    }
}
