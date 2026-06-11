// File: SyncTarget.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Models
{
    public sealed record SyncTarget
    {
        public required string UserId { get; init; }
        public required string UserEmail { get; init; }
        public IReadOnlySet<string> AlternateEmails { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> LabelNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
