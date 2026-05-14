using ContactMesh.Core.Models;

namespace ContactMesh.Core.Merge;

public sealed class DuplicateContactDetector
{
    private readonly EmailNormalizer emailNormalizer;
    private readonly PhoneNormalizer phoneNormalizer;

    public DuplicateContactDetector(EmailNormalizer? emailNormalizer = null, PhoneNormalizer? phoneNormalizer = null)
    {
        this.emailNormalizer = emailNormalizer ?? new EmailNormalizer();
        this.phoneNormalizer = phoneNormalizer ?? new PhoneNormalizer();
    }

    public IReadOnlyList<IReadOnlyList<MeshContact>> FindDuplicates(IEnumerable<MeshContact> contacts)
    {
        return contacts
            .SelectMany(contact => GetKeys(contact).Select(key => (key, contact)))
            .GroupBy(item => item.key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Select(item => item.contact).Distinct().ToList())
            .Where(group => group.Count > 1)
            .Cast<IReadOnlyList<MeshContact>>()
            .ToList();
    }

    private IEnumerable<string> GetKeys(MeshContact contact)
    {
        foreach (var email in contact.Emails.Select(e => this.emailNormalizer.NormalizeForComparison(e.Address)).Where(e => e.Length > 0))
        {
            yield return $"email:{email}";
        }

        foreach (var phone in contact.Phones.Select(p => this.phoneNormalizer.NormalizeForComparison(p.Number)).Where(p => p.Length > 0))
        {
            yield return $"phone:{phone}";
        }
    }
}
