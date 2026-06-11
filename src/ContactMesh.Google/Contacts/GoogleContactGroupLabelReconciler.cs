// File: GoogleContactGroupLabelReconciler.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Google.Contacts
{
    public sealed class GoogleContactGroupLabelReconciler
    {
        public const string AppIdClientDataKey = "contactmesh.appId";
        public const string LabelNameClientDataKey = "contactmesh.labelName";

        public GoogleContactGroupLabelPlan CreatePlan(
            string appId,
            IEnumerable<string> desiredLabels,
            IEnumerable<GoogleContactGroupLabel> existingLabels)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appId);
            ArgumentNullException.ThrowIfNull(desiredLabels);
            ArgumentNullException.ThrowIfNull(existingLabels);

            var desired = NormalizeLabels(desiredLabels);
            var desiredSet = desired.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matchedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var labelsToCreate = new List<string>();
            var labelsToUpdate = new List<GoogleContactGroupLabelUpdate>();
            var labelsToDelete = new List<GoogleContactGroupLabel>();
            var resourceNamesByLabel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in existingLabels.Where(label => label.IsOwnedBy(appId)))
            {
                var managedName = existing.ManagedLabelName.Trim();
                if (managedName.Length == 0 || !desiredSet.Contains(managedName))
                {
                    labelsToDelete.Add(existing);
                    continue;
                }

                var desiredName = desired.First(label => string.Equals(label, managedName, StringComparison.OrdinalIgnoreCase));
                matchedLabels.Add(desiredName);

                if (!string.IsNullOrWhiteSpace(existing.ResourceName))
                {
                    resourceNamesByLabel[desiredName] = existing.ResourceName;
                }

                if (!string.Equals(existing.Name, desiredName, StringComparison.Ordinal)
                    || !string.Equals(existing.ManagedLabelName, desiredName, StringComparison.Ordinal))
                {
                    labelsToUpdate.Add(new GoogleContactGroupLabelUpdate(existing, desiredName));
                }
            }

            labelsToCreate.AddRange(desired.Where(label => !matchedLabels.Contains(label)));

            return new GoogleContactGroupLabelPlan(
                labelsToCreate,
                labelsToUpdate,
                labelsToDelete,
                resourceNamesByLabel);
        }

        public static IReadOnlyDictionary<string, string> CreateClientData(string appId, string labelName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appId);
            ArgumentException.ThrowIfNullOrWhiteSpace(labelName);

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AppIdClientDataKey] = appId,
                [LabelNameClientDataKey] = labelName.Trim()
            };
        }

        private static IReadOnlyList<string> NormalizeLabels(IEnumerable<string> labels)
        {
            return labels
                .Select(label => label.Trim())
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
