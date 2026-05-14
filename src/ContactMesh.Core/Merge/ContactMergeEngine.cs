using ContactMesh.Core.Models;

namespace ContactMesh.Core.Merge;

public sealed class ContactMergeEngine
{
    private readonly PhoneNormalizer phoneNormalizer;

    public ContactMergeEngine(PhoneNormalizer? phoneNormalizer = null)
    {
        this.phoneNormalizer = phoneNormalizer ?? new PhoneNormalizer();
    }

    public MeshContact Merge(MeshContact sourceContact, MeshContact existingContact)
    {
        var sourceEmails = sourceContact.Emails
            .Where(e => !string.IsNullOrWhiteSpace(e.Address))
            .DistinctBy(e => e.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var userOwnedEmails = existingContact.Emails
            .Where(e => !sourceEmails.Any(s => string.Equals(s.Address, e.Address, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var sourcePhones = sourceContact.Phones
            .Where(p => !string.IsNullOrWhiteSpace(p.Number))
            .DistinctBy(p => this.phoneNormalizer.NormalizeForComparison(p.Number))
            .ToList();

        var sourcePhoneKeys = sourcePhones
            .Select(p => this.phoneNormalizer.NormalizeForComparison(p.Number))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var userOwnedPhones = existingContact.Phones
            .Where(p => !sourcePhoneKeys.Contains(this.phoneNormalizer.NormalizeForComparison(p.Number)))
            .ToList();

        return existingContact with
        {
            SourceId = sourceContact.SourceId ?? existingContact.SourceId,
            DisplayName = sourceContact.DisplayName ?? existingContact.DisplayName,
            GivenName = sourceContact.GivenName ?? existingContact.GivenName,
            FamilyName = sourceContact.FamilyName ?? existingContact.FamilyName,
            CompanyName = sourceContact.CompanyName ?? existingContact.CompanyName,
            Department = sourceContact.Department ?? existingContact.Department,
            JobTitle = sourceContact.JobTitle ?? existingContact.JobTitle,
            Emails = sourceEmails.Concat(userOwnedEmails).ToList(),
            Phones = sourcePhones.Concat(userOwnedPhones).ToList(),
            Labels = sourceContact.Labels.Union(existingContact.Labels, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase),
            Metadata = MergeMetadata(existingContact.Metadata, sourceContact.Metadata)
        };
    }

    private static IDictionary<string, string> MergeMetadata(IDictionary<string, string> existing, IDictionary<string, string> source)
    {
        var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var item in source)
        {
            merged[item.Key] = item.Value;
        }

        return merged;
    }
}
