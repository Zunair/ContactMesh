// File: ContactSyncRunResult.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Logging;
using ContactMesh.Core.Models;

namespace ContactMesh.Core.Sync
{
    public sealed record ContactSyncRunResult
    {
        public bool DryRun { get; init; }
        public IReadOnlyList<string> RunWarnings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<SyncResult> Results { get; init; } = Array.Empty<SyncResult>();

        public int TargetCount => this.Results.Count;
        public int CreateCount => this.Results.Sum(result => result.CreateCount);
        public int UpdateCount => this.Results.Sum(result => result.UpdateCount);
        public int DeleteCount => this.Results.Sum(result => result.DeleteCount);
        public int NoChangeCount => this.Results.Sum(result => result.NoChangeCount);
        public int WriteCount => this.Results.Sum(result => result.WriteCount);
        public int WarningCount => this.RunWarnings.Count + this.Results.Sum(result => result.WarningCount);
        public int ErrorCount => this.Results.Sum(result => result.ErrorCount);
        public bool HasWarnings => this.WarningCount > 0;
        public bool HasErrors => this.ErrorCount > 0;
        public IReadOnlyList<string> Warnings => this.RunWarnings.Concat(this.Results.SelectMany(result => result.Warnings)).ToList();
        public IReadOnlyList<string> Errors => this.Results.SelectMany(result => result.Errors).ToList();
        public IReadOnlyList<SyncLogEntry> LogEntries => this.Results.SelectMany(result => result.LogEntries).ToList();

        public SyncRunSummary Summary => new()
        {
            DryRun = this.DryRun,
            TargetCount = this.TargetCount,
            CreateCount = this.CreateCount,
            UpdateCount = this.UpdateCount,
            DeleteCount = this.DeleteCount,
            NoChangeCount = this.NoChangeCount,
            WarningCount = this.WarningCount,
            ErrorCount = this.ErrorCount
        };
    }

    public sealed record SyncRunSummary
    {
        public bool DryRun { get; init; }
        public int TargetCount { get; init; }
        public int CreateCount { get; init; }
        public int UpdateCount { get; init; }
        public int DeleteCount { get; init; }
        public int NoChangeCount { get; init; }
        public int WarningCount { get; init; }
        public int ErrorCount { get; init; }
        public int WriteCount => this.CreateCount + this.UpdateCount + this.DeleteCount;
        public bool HasWarnings => this.WarningCount > 0;
        public bool HasErrors => this.ErrorCount > 0;
    }
}
