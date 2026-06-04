using ContactMesh.Core.Merge;
using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync;

public sealed class SyncPlanner
{
    private readonly ContactMergeEngine mergeEngine;
    private readonly StaleContactCleanupEngine staleContactCleanupEngine;
    private readonly EmailNormalizer emailNormalizer;
    private readonly ContactEmailPolicyEngine? emailPolicyEngine;

    public SyncPlanner(
        ContactMergeEngine? mergeEngine = null,
        StaleContactCleanupEngine? staleContactCleanupEngine = null,
        EmailNormalizer? emailNormalizer = null,
        ContactEmailPolicyEngine? emailPolicyEngine = null)
    {
        this.mergeEngine = mergeEngine ?? new ContactMergeEngine();
        this.staleContactCleanupEngine = staleContactCleanupEngine ?? new StaleContactCleanupEngine();
        this.emailNormalizer = emailNormalizer ?? new EmailNormalizer();
        this.emailPolicyEngine = emailPolicyEngine;
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
        var matchedExistingContacts = new HashSet<MeshContact>();

        foreach (var desired in desiredContacts)
        {
            MeshContact? existing = null;
            var matchedBySourceId = !string.IsNullOrWhiteSpace(desired.SourceId)
                && existingBySourceId.TryGetValue(desired.SourceId, out existing);

            var matchedByAlternateEmail = false;
            var matchedEmail = string.Empty;
            if (!matchedBySourceId && !this.TryFindExistingByEmail(
                desired,
                existingByEmail,
                out existing,
                out matchedEmail,
                out matchedByAlternateEmail))
            {
                operations.Add(new SyncOperation
                {
                    OperationType = SyncOperationType.Create,
                    DesiredContact = this.ApplyEmailPolicy(desired),
                    Reason = "Managed contact does not exist."
                });

                continue;
            }

            if (!string.IsNullOrWhiteSpace(existing!.SourceId))
            {
                matchedExistingSourceIds.Add(existing.SourceId!);
            }

            matchedExistingContacts.Add(existing);

            var merged = this.ApplyEmailPolicy(this.mergeEngine.Merge(desired, existing));
            var type = AreEquivalent(merged, existing) ? SyncOperationType.NoChange : SyncOperationType.Update;

            operations.Add(new SyncOperation
            {
                OperationType = type,
                DesiredContact = merged,
                ExistingContact = existing,
                Reason = type == SyncOperationType.NoChange
                    ? "No managed fields changed."
                    : matchedBySourceId ? "Managed fields changed." : "Existing contact matched by email.",
                Warnings = matchedByAlternateEmail
                    ? new[]
                    {
                        $"Existing contact for {FormatIdentity(desired)} matched by alternate email {matchedEmail}; check account primary email and aliases."
                    }
                    : Array.Empty<string>()
            });
        }

        foreach (var staleContact in existingContacts.Where(contact =>
            string.IsNullOrWhiteSpace(contact.SourceId)
            && !matchedExistingContacts.Contains(contact)
            && this.staleContactCleanupEngine.HasManagedEmail(contact)))
        {
            var cleanup = this.staleContactCleanupEngine.Clean(staleContact);
            var operationType = cleanup.ShouldDelete
                ? SyncOperationType.Delete
                : AreEquivalent(cleanup.Contact, staleContact) ? SyncOperationType.NoChange
                : SyncOperationType.Update;

            operations.Add(new SyncOperation
            {
                OperationType = operationType,
                DesiredContact = cleanup.Contact,
                ExistingContact = staleContact,
                Reason = cleanup.Reason
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
            var operationType = cleanup.ShouldDelete
                ? SyncOperationType.Delete
                : AreEquivalent(cleanup.Contact, staleContact) ? SyncOperationType.NoChange
                : SyncOperationType.Update;

            operations.Add(new SyncOperation
            {
                OperationType = operationType,
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
            .SelectMany(contact => GetContactMatchEmails(contact)
                .Select(email => (email: this.emailNormalizer.NormalizeForComparison(email), contact)))
            .Where(item => item.email.Length > 0)
            .GroupBy(item => item.email, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.contact).Distinct().Count() == 1)
            .ToDictionary(group => group.Key, group => group.First().contact, StringComparer.OrdinalIgnoreCase);
    }

    private MeshContact ApplyEmailPolicy(MeshContact contact)
    {
        return this.emailPolicyEngine?.Apply(contact) ?? contact;
    }

    private bool TryFindExistingByEmail(
        MeshContact desired,
        IReadOnlyDictionary<string, MeshContact> existingByEmail,
        out MeshContact? existing,
        out string matchedEmail,
        out bool matchedByAlternateEmail)
    {
        var primaryEmails = desired.Emails
            .Select(email => this.emailNormalizer.NormalizeForComparison(email.Address))
            .Where(email => email.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var email in GetContactMatchEmails(desired).Select(email => this.emailNormalizer.NormalizeForComparison(email)))
        {
            if (email.Length > 0 && existingByEmail.TryGetValue(email, out existing))
            {
                matchedEmail = email;
                matchedByAlternateEmail = !primaryEmails.Contains(email);
                return true;
            }
        }

        existing = null;
        matchedEmail = string.Empty;
        matchedByAlternateEmail = false;
        return false;
    }

    private static IEnumerable<string> GetContactMatchEmails(MeshContact contact)
    {
        foreach (var email in contact.Emails.Select(email => email.Address))
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                yield return email;
            }
        }

        foreach (var email in contact.MatchEmails)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                yield return email;
            }
        }
    }

    private static string FormatIdentity(MeshContact contact)
    {
        return new[]
            {
                contact.DisplayName,
                contact.SourceId,
                contact.Emails.FirstOrDefault(email => email.IsPrimary)?.Address,
                contact.Emails.FirstOrDefault()?.Address
            }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "(unknown contact)";
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
        if (left.Count != right.Count) return false;
        var sortedLeft = left.OrderBy(p => p.Type, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Number, StringComparer.OrdinalIgnoreCase);
        var sortedRight = right.OrderBy(p => p.Type, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Number, StringComparer.OrdinalIgnoreCase);
        return sortedLeft.Zip(sortedRight).All(pair =>
            string.Equals(pair.First.Number, pair.Second.Number, StringComparison.Ordinal)
            && string.Equals(pair.First.Type, pair.Second.Type, StringComparison.Ordinal)
            && pair.First.IsPrimary == pair.Second.IsPrimary);
    }

    private static bool DictionariesEqual(IDictionary<string, string> left, IDictionary<string, string> right)
    {
        // Only compare metadata keys that exist in `right` (the existing/persisted contact).
        // Extra keys in `left` (merged) come from ephemeral source metadata that is never written
        // to or read back from any provider, and must not trigger spurious Update operations.
        return right.All(item => left.TryGetValue(item.Key, out var leftValue)
            && string.Equals(item.Value, leftValue, StringComparison.Ordinal));
    }
}
