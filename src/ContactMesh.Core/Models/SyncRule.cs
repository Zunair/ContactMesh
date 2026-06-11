// File: SyncRule.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Models
{
    public sealed record SyncRule
    {
        public required string Name { get; init; }
        public SyncRuleKind Kind { get; init; }
        public IReadOnlySet<string> GroupIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool Enabled { get; init; } = true;
    }

    public enum SyncRuleKind
    {
        GlobalContact,
        Exclusion,
        GroupVisibility
    }
}
