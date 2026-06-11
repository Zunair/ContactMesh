// File: ContactPruneOptions.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Sync
{
    public sealed record ContactPruneOptions
    {
        public IReadOnlyList<string> ManagedEmailDomains { get; init; } = Array.Empty<string>();
    }
}
