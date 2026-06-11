// File: AuditLogOptions.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Audit
{
    public sealed record AuditLogOptions
    {
        public bool Enabled { get; init; } = true;
        public string Directory { get; init; } = "logs/audit";
        public bool IncludeNoChange { get; init; } = false;
        public bool IncludeDryRunPlannedAsWrites { get; init; } = false;
    }
}
