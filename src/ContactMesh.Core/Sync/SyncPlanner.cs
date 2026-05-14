using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class SyncPlanner
{
    private readonly ContactMergeEngine mergeEngine;
    private readonly StaleContactCleanupEngine staleContactCleanupEngine;

    public SyncPlanner(ContactMergeEngine? mergeEngine = null, StaleContactCleanupEngine? staleContactCleanupEngine = null)
    {
        this.mergeEngine = mergeEngine ?? new ContactMergeEngine();
        this.staleContactCleanupEngine = staleContactCleanupEngine ?? new StaleContactCleanupEngine();
    }

    public IReadOnlyList<SyncOperation> CreatePlan(IReadOnlyList<MeshContact> desiredContacts, IReadOnlyList<MeshContact> existingContacts)
    {
        var operations = new List<SyncOperation>();
        var existingBySourceId = existingContacts
            .Where(c => !string.IsNullOrWhiteSpace(c.SourceId))
            .GroupBy(c => c.SourceId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var desired in desiredContacts)
        {
            if (string.IsNullOrWhiteSpace(desired.SourceId) || !existingBySourceId.TryGetValue(desired.SourceId, out var existing))
            {
                operations.Add(new SyncOperation
                {
                    OperationType = SyncOperationType.Create,
                    DesiredContact = desired,
                    Reason = "Managed contact does not exist."
                });

                continue;
            }

            var merged = this.mergeEngine.Merge(desired, existing);
            var type = AreEquivalent(merged, existing) ? SyncOperationType.NoChange : SyncOperationType.Update;

            operations.Add(new SyncOperation
            {
                OperationType = type,
                DesiredContact = merged,
                ExistingContact = existing,
                Reason = type == SyncOperationType.NoChange ? "No managed fields changed." : "Managed fields changed."
            });
        }

        var desiredSourceIds = desiredContacts
            .Where(c => !string.IsNullOrWhiteSpace(c.SourceId))
            .Select(c => c.SourceId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var staleContact in existingBySourceId.Values.Where(c => !desiredSourceIds.Contains(c.SourceId!)))
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
