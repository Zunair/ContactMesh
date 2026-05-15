using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;

namespace ContactMesh.Microsoft365.Groups;

public sealed class MicrosoftGroupProvider : IGroupProvider
{
    private readonly IMicrosoftGraphGroupClient? client;

    public MicrosoftGroupProvider(IMicrosoftGraphGroupClient? client = null)
    {
        this.client = client;
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
            !string.IsNullOrWhiteSpace(group.Id) && !string.IsNullOrWhiteSpace(group.Mail)))
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
}
