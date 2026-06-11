// File: SyncScheduleOptions.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Worker.Scheduling
{
    public sealed record SyncScheduleOptions
    {
        public const string SectionName = "Schedule";

        public string Cron { get; init; } = "0 */6 * * *";
    }
}
