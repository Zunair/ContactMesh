using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class SyncPlanner
{
    private readonly ContactMergeEngine mergeEngine;
    private readonly StaleContactCleanupEngine staleContactCleanupEngine;
    private readonly EmailNormalizer emailNormalizer;

    public SyncPlanner(
        ContactMergeEngine? mergeEngine = null,
        StaleContactCleanupEngine? staleContactCleanupEngine = null,
        EmailNormalizer? emailNormalizer = null)
    {
        this.mergeEngine = mergeEngine ?? new ContactMergeEngine();
        this.staleContactCleanupEngine = staleContactCleanupEngine ?? new StaleContactCleanupEngine();
        this.emailNormalizer = emailNormalizer ?? new EmailNormalizer();
    }

    public IReadOnlyList<SyncOperation> CreatePlan(IReadOnlyList<MeshContact> desiredContacts, IReadOnlyList<MeshContact> existingContacts)
    {
        var operations = new List<SyncOperation>();
        var existingBySourceId = existingContacts
            .Where(c => !string.IsNullOrWhiteSpace(c.SourceId))
            .GroupBy(c => c.SourceId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var existingByEmail = this.BuildUniqueExistingEmailIndex(existingContacts);
        var matchedExistingSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var desired in desiredContacts)
        {
            MeshContact? existing = null;
            var matchedBySourceId = !string.IsNullOrWhiteSpace(desired.SourceId)
                && existingBySourceId.TryGetValue(desired.SourceId, out existing);

            if (!matchedBySourceId && !this.TryFindExistingByEmail(desired, existingByEmail, out existing))
            {
                operations.Add(new SyncOperation
                {
                    OperationType = SyncOperationType.Create,
                    DesiredContact = desired,
                    Reason = "Managed contact does not exist."
                });

                continue;
            }

            if (!string.IsNullOrWhiteSpace(existing!.SourceId))
            {
                matchedExistingSourceIds.Add(existing.SourceId!);
            }

            var merged = this.mergeEngine.Merge(desired, existing);
            var type = AreEquivalent(merged, existing) ? SyncOperationType.NoChange : SyncOperationType.Update;

            operations.Add(new SyncOperation
            {
                OperationType = type,
                DesiredContact = merged,
                ExistingContact = existing,
                Reason = type == SyncOperationType.NoChange
                    ? "No managed fields changed."
                    : matchedBySourceId ? "Managed fields changed." : "Existing contact matched by email."
            });
        }

        var desiredSourceIds = desiredContacts
            .Where(c => !string.IsNullOrWhiteSpace(c.SourceId))
            .Select(c => c.SourceId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var staleContact in existingBySourceId.Values.Where(c =>
            !desiredSourceIds.Contains(c.SourceId!)
            && !matchedExistingSourceIds.Contains(c.SourceId!)))
        {
            var cleanup = this.staleContactCleanupEngine.Clean(staleContact);

            operations.Add(new SyncOperation
            {
                OperationType = cleanup.ShouldDelete ? SyncOperationType.Delete : SyncOperationType.Update,
                DesiredContact = cleanup.Contact,
                ExistingContact = staleContact,
                Reason = cleanup.Reason
            });
        }

        return operations;
    }

    private Dictionary<string, MeshContact> BuildUniqueExistingEmailIndex(IReadOnlyList<MeshContact> existingContacts)
    {
        return existingContacts
            .SelectMany(contact => contact.Emails
                .Select(email => (email: this.emailNormalizer.NormalizeForComparison(email.Address), contact)))
            .Where(item => item.email.Length > 0)
            .GroupBy(item => item.email, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.contact).Distinct().Count() == 1)
            .ToDictionary(group => group.Key, group => group.First().contact, StringComparer.OrdinalIgnoreCase);
    }

    private bool TryFindExistingByEmail(
        MeshContact desired,
        IReadOnlyDictionary<string, MeshContact> existingByEmail,
        out MeshContact? existing)
    {
        foreach (var email in desired.Emails.Select(email => this.emailNormalizer.NormalizeForComparison(email.Address)))
        {
            if (email.Length > 0 && existingByEmail.TryGetValue(email, out existing))
            {
                return true;
            }
        }

        existing = null;
        return false;
    }

    private static bool AreEquivalent(MeshContact left, MeshContact right)
    {
        return string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            && string.Equals(left.GivenName, right.GivenName, StringComparison.Ordinal)
            && string.Equals(left.FamilyName, right.FamilyName, StringComparison.Ordinal)
            && string.Equals(left.CompanyName, right.CompanyName, StringComparison.Ordinal)
            && string.Equals(left.Department, right.Department, StringComparison.Ordinal)
            && string.Equals(left.JobTitle, right.JobTitle, StringComparison.Ordinal)
            && ContactEmailsEqual(left.Emails, right.Emails)
            && ContactPhonesEqual(left.Phones, right.Phones)
            && left.Labels.SetEquals(right.Labels)
            && DictionariesEqual(left.Metadata, right.Metadata);
    }

    private static bool ContactEmailsEqual(IReadOnlyList<ContactEmail> left, IReadOnlyList<ContactEmail> right)
    {
        return left.Count == right.Count
            && left.Zip(right).All(pair =>
                string.Equals(pair.First.Address, pair.Second.Address, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pair.First.Type, pair.Second.Type, StringComparison.Ordinal)
                && pair.First.IsPrimary == pair.Second.IsPrimary);
    }

    private static bool ContactPhonesEqual(IReadOnlyList<ContactPhone> left, IReadOnlyList<ContactPhone> right)
    {
        return left.Count == right.Count
            && left.Zip(right).All(pair =>
                string.Equals(pair.First.Number, pair.Second.Number, StringComparison.Ordinal)
                && string.Equals(pair.First.Type, pair.Second.Type, StringComparison.Ordinal)
                && pair.First.IsPrimary == pair.Second.IsPrimary);
    }

    private static bool DictionariesEqual(IDictionary<string, string> left, IDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(item => right.TryGetValue(item.Key, out var rightValue)
                && string.Equals(item.Value, rightValue, StringComparison.Ordinal));
    }
}
