namespace ContactMesh.Core.Models;

public sealed record ContactMeshOptions
{
    public const string SectionName = "ContactMesh";

    public string Provider { get; init; } = string.Empty;
    public bool DryRun { get; init; } = true;
    public IReadOnlyList<string> ManagedEmailDomains { get; init; } = Array.Empty<string>();
    public SyncRuleOptions Rules { get; init; } = new();
}

public sealed record SyncRuleOptions
{
    public IReadOnlyList<string> GlobalUserGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GlobalExternalContactGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExclusionGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ScopedGroupRoots { get; init; } = Array.Empty<string>();
    public IReadOnlyList<GroupMapping> GroupMappings { get; init; } = Array.Empty<GroupMapping>();
}

public sealed record GroupMapping(string From, string To);
