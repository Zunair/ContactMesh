using ContactMesh.Core.Audit;
using ContactMesh.Core.Notifications;

namespace ContactMesh.Core.Models;

public sealed record ContactMeshOptions
{
    public const string SectionName = "ContactMesh";

    public string Provider { get; init; } = string.Empty;
    public bool DryRun { get; init; } = true;
    public bool DisableDeletes { get; init; }
    public bool ForceResetLabels { get; init; }
    public bool ForceDeduplicatePhones { get; init; }
    public bool ForceNormalizeEmailTypes { get; init; }
    public IReadOnlyList<string> ManagedEmailDomains { get; init; } = Array.Empty<string>();
    public SyncRuleOptions Rules { get; init; } = new();
    public AuditLogOptions AuditLog { get; init; } = new();
    public NotificationOptions Notifications { get; init; } = new();
}

public sealed record SyncRuleOptions
{
    public IReadOnlyList<string> MainContactsGroupEmails { get; init; } = Array.Empty<string>();
    public string MainContactsGroupLabel { get; init; } = string.Empty;
    public string GroupContactPrefix { get; init; } = "+";
    public IReadOnlyList<string> TargetUsers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GlobalUserGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GlobalExternalContactGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GroupsToSyncByGroup { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExclusionGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ScopedGroupRoots { get; init; } = Array.Empty<string>();
    public IReadOnlyList<GroupMapping> GroupMappings { get; init; } = Array.Empty<GroupMapping>();
    public IReadOnlyList<string> IncludedOrganizationUnits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludedOrganizationUnits { get; init; } = Array.Empty<string>();
}

public sealed record GroupMapping(string From, string To);
