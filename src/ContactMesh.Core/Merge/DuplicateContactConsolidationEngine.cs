// File: DuplicateContactConsolidationEngine.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Merge
{
    public sealed class DuplicateContactConsolidationEngine
    {
        private readonly EmailNormalizer emailNormalizer;

        public DuplicateContactConsolidationEngine(EmailNormalizer? emailNormalizer = null)
        {
            this.emailNormalizer = emailNormalizer ?? new EmailNormalizer();
        }

        public IReadOnlyList<SyncOperation> CreatePlan(IReadOnlyList<MeshContact> contacts)
        {
            var operations = new List<SyncOperation>();
            var duplicateGroups = contacts
                .Where(contact => !string.IsNullOrWhiteSpace(contact.SourceId))
                .Select(contact => (contact, primaryEmail: GetPrimaryEmail(contact)))
                .Where(item => !string.IsNullOrWhiteSpace(item.primaryEmail))
                .GroupBy(item => item.primaryEmail, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1);

            foreach (var duplicateGroup in duplicateGroups)
            {
                var contactsToMerge = duplicateGroup.Select(item => item.contact).ToList();
                var keeper = contactsToMerge[0];
                var duplicates = contactsToMerge.Skip(1).ToList();
                var merged = MergeDuplicates(keeper, duplicates);

                if (!AreEquivalent(keeper, merged))
                {
                    operations.Add(new SyncOperation
                    {
                        OperationType = SyncOperationType.Update,
                        DesiredContact = merged,
                        ExistingContact = keeper,
                        Reason = "Duplicate managed contacts were merged into the first matching contact."
                    });
                }

                foreach (var duplicate in duplicates)
                {
                    operations.Add(new SyncOperation
                    {
                        OperationType = SyncOperationType.Delete,
                        DesiredContact = duplicate,
                        ExistingContact = duplicate,
                        Reason = "Duplicate managed contact was merged into another contact."
                    });
                }
            }

            return operations;
        }

        private MeshContact MergeDuplicates(MeshContact keeper, IReadOnlyList<MeshContact> duplicates)
        {
            var allContacts = new[] { keeper }.Concat(duplicates).ToList();

            return keeper with
            {
                Emails = MergeEmails(allContacts),
                Phones = MergePhones(allContacts),
                Labels = allContacts
                    .SelectMany(contact => contact.Labels)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                Metadata = MergeMetadata(allContacts),
                Notes = MergeNotes(allContacts)
            };
        }

        private IReadOnlyList<ContactEmail> MergeEmails(IEnumerable<MeshContact> contacts)
        {
            return contacts
                .SelectMany(contact => contact.Emails)
                .Where(email => !string.IsNullOrWhiteSpace(email.Address))
                .GroupBy(email => this.emailNormalizer.NormalizeForComparison(email.Address), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(email => email.IsPrimary).First())
                .ToList();
        }

        private static IReadOnlyList<ContactPhone> MergePhones(IEnumerable<MeshContact> contacts)
        {
            var normalizer = new PhoneNormalizer();

            return contacts
                .SelectMany(contact => contact.Phones)
                .Where(phone => !string.IsNullOrWhiteSpace(phone.Number))
                .GroupBy(phone => normalizer.NormalizeForComparison(phone.Number), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(phone => phone.IsPrimary).First())
                .Select(phone => phone with { Number = normalizer.FormatForDisplay(phone.Number) })
                .ToList();
        }

        private static IDictionary<string, string> MergeMetadata(IEnumerable<MeshContact> contacts)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var contact in contacts)
            {
                foreach (var item in contact.Metadata)
                {
                    merged[item.Key] = item.Value;
                }
            }

            return merged;
        }

        private static string? MergeNotes(IEnumerable<MeshContact> contacts)
        {
            var notes = contacts
                .Select(contact => contact.Notes)
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return notes.Count == 0 ? null : string.Join(Environment.NewLine, notes);
        }

        private string GetPrimaryEmail(MeshContact contact)
        {
            var email = contact.Emails.FirstOrDefault(email => email.IsPrimary) ?? contact.Emails.FirstOrDefault();

            return this.emailNormalizer.NormalizeForComparison(email?.Address);
        }

        private static bool AreEquivalent(MeshContact left, MeshContact right)
        {
            return string.Equals(left.SourceId, right.SourceId, StringComparison.Ordinal)
                && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
                && string.Equals(left.GivenName, right.GivenName, StringComparison.Ordinal)
                && string.Equals(left.FamilyName, right.FamilyName, StringComparison.Ordinal)
                && string.Equals(left.CompanyName, right.CompanyName, StringComparison.Ordinal)
                && string.Equals(left.Department, right.Department, StringComparison.Ordinal)
                && string.Equals(left.JobTitle, right.JobTitle, StringComparison.Ordinal)
                && string.Equals(left.Notes, right.Notes, StringComparison.Ordinal)
                && left.Emails.SequenceEqual(right.Emails)
                && left.Phones.SequenceEqual(right.Phones)
                && left.Labels.SetEquals(right.Labels)
                && DictionariesEqual(left.Metadata, right.Metadata);
        }

        private static bool DictionariesEqual(IDictionary<string, string> left, IDictionary<string, string> right)
        {
            return left.Count == right.Count
                && left.All(item => right.TryGetValue(item.Key, out var rightValue)
                    && string.Equals(item.Value, rightValue, StringComparison.Ordinal));
        }
    }
}
