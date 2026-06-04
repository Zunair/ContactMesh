using ContactMesh.Core.Models;

namespace ContactMesh.Core.Merge;

public sealed class ContactMergeEngine
{
    private readonly PhoneNormalizer phoneNormalizer;
    private readonly ContactMergeOptions options;

    public ContactMergeEngine(PhoneNormalizer? phoneNormalizer = null, ContactMergeOptions? options = null)
    {
        this.phoneNormalizer = phoneNormalizer ?? new PhoneNormalizer();
        this.options = options ?? new ContactMergeOptions();
    }

    public MeshContact Merge(MeshContact sourceContact, MeshContact existingContact)
    {
        var sourceEmails = sourceContact.Emails
            .Where(e => !string.IsNullOrWhiteSpace(e.Address))
            .DistinctBy(e => e.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sourceMatchEmails = sourceEmails.Select(email => email.Address)
            .Concat(sourceContact.MatchEmails)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var userOwnedEmails = existingContact.Emails
            .Where(e => !sourceMatchEmails.Contains(e.Address))
            .ToList();

        var sourcePhones = sourceContact.Phones
            .Where(p => !string.IsNullOrWhiteSpace(p.Number))
            .DistinctBy(p => (this.phoneNormalizer.NormalizeForComparison(p.Number), p.Type.ToLowerInvariant()))
            .Select(sourcePhone =>
            {
                // Preserve the provider's already-stored number string when the normalized form
                // matches, so the merged contact compares equal to what the provider returns.
                // Without this, providers that reformat numbers (e.g. "+12155550100" stored back
                // as "+1 (215) 555-0100") would trigger a spurious Update on every run.
                var key = this.phoneNormalizer.NormalizeForComparison(sourcePhone.Number);
                var stored = existingContact.Phones.FirstOrDefault(p =>
                    string.Equals(this.phoneNormalizer.NormalizeForComparison(p.Number), key, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Type, sourcePhone.Type, StringComparison.Ordinal));
                return stored is null
                    ? sourcePhone with { Number = this.phoneNormalizer.FormatForDisplay(sourcePhone.Number) }
                    : sourcePhone with { Number = stored.Number };
            })
            .ToList();

        if (this.options.ForceDeduplicatePhones)
        {
            sourcePhones = sourcePhones
                .DistinctBy(p => this.phoneNormalizer.NormalizeForComparison(p.Number), StringComparer.OrdinalIgnoreCase)
                .Select(p => p with { Number = this.phoneNormalizer.FormatForDisplay(p.Number) })
                .ToList();
        }

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
            MatchEmails = sourceContact.MatchEmails.ToList(),
            Phones = sourcePhones.Concat(userOwnedPhones).ToList(),
            Labels = this.MergeLabels(sourceContact.Labels, existingContact.Labels),
            Metadata = MergeMetadata(existingContact.Metadata, sourceContact.Metadata)
        };
    }

    private IReadOnlySet<string> MergeLabels(IReadOnlySet<string> source, IReadOnlySet<string> existing)
    {
        if (this.options.ForceResetLabels)
        {
            return source.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (this.options.ManagedLabels.Count == 0)
        {
            return source.Union(existing, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var merged = existing
            .Where(label => !this.options.ManagedLabels.Contains(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var label in source)
        {
            merged.Add(label);
        }

        return merged;
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
