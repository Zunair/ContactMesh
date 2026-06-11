// File: SyncProgress.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Logging
{
    public static class SyncProgressKind
    {
        public const string RunStarted = "RunStarted";
        public const string TargetStarted = "TargetStarted";
        public const string TargetCompleted = "TargetCompleted";
        public const string TargetFailed = "TargetFailed";
    }

    public sealed record SyncProgress(
        string Kind,
        string TargetUserId,
        string? TargetUserEmail,
        int TargetIndex,
        int TargetCount,
        int CreateCount = 0,
        int UpdateCount = 0,
        int DeleteCount = 0,
        int NoChangeCount = 0,
        string? ErrorMessage = null,
        string? Message = null);

    public delegate Task SyncProgressCallback(SyncProgress progress, CancellationToken cancellationToken);

    public static class SyncProgressFormatter
    {
        public static string Format(SyncProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);

            if (progress.Kind == SyncProgressKind.RunStarted)
            {
                return $"Scope: {progress.Message} ({progress.TargetCount} targets)";
            }

            var target = FormatTarget(progress);

            return progress.Kind switch
            {
                SyncProgressKind.TargetStarted => $"Target {progress.TargetIndex}/{progress.TargetCount} {target}: started.",
                SyncProgressKind.TargetCompleted => $"Target {progress.TargetIndex}/{progress.TargetCount} {target}: completed with {progress.CreateCount} create, {progress.UpdateCount} update, {progress.DeleteCount} delete, {progress.NoChangeCount} unchanged.",
                SyncProgressKind.TargetFailed => $"Target {progress.TargetIndex}/{progress.TargetCount} {target}: failed - {progress.ErrorMessage}",
                _ => $"Target {progress.TargetIndex}/{progress.TargetCount} {target}: {progress.Kind}."
            };
        }

        private static string FormatTarget(SyncProgress progress)
        {
            return string.IsNullOrWhiteSpace(progress.TargetUserEmail)
                || string.Equals(progress.TargetUserId, progress.TargetUserEmail, StringComparison.OrdinalIgnoreCase)
                ? progress.TargetUserId
                : $"{progress.TargetUserId} <{progress.TargetUserEmail}>";
        }
    }
}
