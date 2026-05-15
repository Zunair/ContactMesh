namespace ContactMesh.Microsoft365.Groups;

public interface IMicrosoftGraphGroupClient
{
    Task<IReadOnlyList<MicrosoftGraphGroup>> ListGroupsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MicrosoftGraphGroupMember>> ListGroupMembersAsync(
        string groupId,
        CancellationToken cancellationToken);
}
