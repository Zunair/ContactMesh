namespace ContactMesh.Core.Models;

public sealed record MeshGroup
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public MeshGroupVisibility GroupVisibility { get; init; } = MeshGroupVisibility.Hidden;
    public MeshGroupVisibility MemberVisibility { get; init; } = MeshGroupVisibility.Hidden;
    public IReadOnlyList<MeshGroupMember> Members { get; init; } = Array.Empty<MeshGroupMember>();
}

public sealed record MeshGroupMember
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required MeshGroupMemberType Type { get; init; }
    public string? DisplayName { get; init; }
}

public enum MeshGroupMemberType
{
    User,
    Group,
    Contact,
    Unknown
}

public enum MeshGroupVisibility
{
    Hidden,
    Members,
    Domain
}
