// File: RunAuditContext.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Audit
{
    public sealed record RunAuditContext
    {
        public required string Provider { get; init; }
        public required string RunId { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public bool DryRun { get; init; }
        public string? ConfigPath { get; init; }
        public string? HostKind { get; init; }
        public Exception? Failure { get; init; }

        public static string NewRunId(DateTimeOffset startedAt)
        {
            return $"{startedAt.UtcDateTime:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}".Substring(0, 24);
        }
    }

    public sealed record RunAuditArtifacts(
        string DetailCsvPath,
        string SummaryCsvPath,
        long DetailCsvBytes,
        long SummaryCsvBytes);
}
