// File: NotificationOptions.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Notifications
{
    public sealed record NotificationOptions
    {
        public bool Enabled { get; init; } = true;
        public string From { get; init; } = string.Empty;
        public IReadOnlyList<string> SuccessTo { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> FailureTo { get; init; } = Array.Empty<string>();
        public string SubjectPrefix { get; init; } = "[ContactMesh]";
        public bool AttachCsvOnFailure { get; init; } = true;
        public int MaxAttachmentBytes { get; init; } = 3 * 1024 * 1024;
    }
}
