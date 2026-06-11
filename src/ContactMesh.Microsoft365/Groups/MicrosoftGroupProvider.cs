// File: MicrosoftGroupProvider.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Groups
{
    public sealed class MicrosoftGroupProvider : IGroupProvider
    {
        private readonly IMicrosoftGraphGroupClient? client;
        private readonly IReadOnlySet<string> allowedGroupTypes;

        public MicrosoftGroupProvider(
            IMicrosoftGraphGroupClient? client = null,
            IReadOnlyList<string>? allowedGroupTypes = null)
        {
            this.client = client;
            this.allowedGroupTypes = allowedGroupTypes is { Count: > 0 }
                ? new HashSet<string>(allowedGroupTypes, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<IReadOnlyList<MeshGroup>> GetGroupsAsync(CancellationToken cancellationToken)
        {
            if (this.client is null)
            {
                return Array.Empty<MeshGroup>();
            }

            var graphGroups = await this.client.ListGroupsAsync(cancellationToken).ConfigureAwait(false);
            var groups = new List<MeshGroup>();

            foreach (var graphGroup in graphGroups.Where(group =>
                !string.IsNullOrWhiteSpace(group.Id) &&
                !string.IsNullOrWhiteSpace(group.Mail) &&
                this.IsAllowedGroupType(group)))
            {
                var members = await this.client.ListGroupMembersAsync(graphGroup.Id!, cancellationToken)
                    .ConfigureAwait(false);
                groups.Add(MicrosoftGroupMapper.ToMeshGroup(graphGroup, members));
            }

            return groups;
        }

        public async Task<IReadOnlyList<MeshContact>> GetGroupContactsAsync(
            string groupId,
            CancellationToken cancellationToken)
        {
            if (this.client is null)
            {
                return Array.Empty<MeshContact>();
            }

            var members = await this.client.ListGroupMembersAsync(groupId, cancellationToken).ConfigureAwait(false);

            return members
                .Where(MicrosoftGroupMapper.IsOrgContact)
                .Where(member => !string.IsNullOrWhiteSpace(member.Id) && !string.IsNullOrWhiteSpace(member.Mail))
                .Select(MicrosoftGroupMapper.ToMeshContact)
                .ToList();
        }

        private bool IsAllowedGroupType(MicrosoftGraphGroup group)
        {
            if (this.allowedGroupTypes.Count == 0)
            {
                return true;
            }

            var groupType = MicrosoftGroupMapper.GetGroupType(group);
            if (!groupType.HasValue)
            {
                return false;
            }

            // Allow "Security" in config to match MailEnabledSecurity.
            // Pure security groups (no mail address) are already excluded by the mail check above.
            if (groupType.Value == MicrosoftGroupType.MailEnabledSecurity &&
                this.allowedGroupTypes.Contains("Security"))
            {
                return true;
            }

            return this.allowedGroupTypes.Contains(groupType.Value.ToString());
        }
    }
}
