// File: GroupContactFactory.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync
{
    public sealed class GroupContactFactory
    {
        public const string DefaultGroupContactPrefix = "+";

        public MeshContact CreateGroupContact(
            MeshGroup group,
            IEnumerable<string>? labels = null,
            string? prefix = DefaultGroupContactPrefix)
        {
            var baseDisplayName = string.IsNullOrWhiteSpace(group.DisplayName)
                ? group.Email
                : HyphenateWhitespace(group.DisplayName);
            var displayName = ApplyPrefix(baseDisplayName, prefix);
            var fullDisplayName = string.IsNullOrWhiteSpace(displayName) ? displayName : $"{displayName} Group";

            return new MeshContact
            {
                SourceId = $"group:{group.Id}",
                DisplayName = fullDisplayName,
                GivenName = displayName,
                FamilyName = "Group",
                Emails = new[] { new ContactEmail(group.Email, "work", true) },
                Labels = (labels ?? Array.Empty<string>())
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["groupId"] = group.Id,
                    ["groupEmail"] = group.Email,
                    ["sourceType"] = "group"
                }
            };
        }

        private static string? ApplyPrefix(string? displayName, string? prefix)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix.Trim();
            if (normalizedPrefix.Length == 0 || displayName.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            {
                return displayName;
            }

            return $"{normalizedPrefix}{displayName}";
        }

        private static string HyphenateWhitespace(string displayName)
        {
            return string.Join(
                '-',
                displayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
