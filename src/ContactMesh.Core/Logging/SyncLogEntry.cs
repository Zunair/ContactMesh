using ContactMesh.Core.Models;

namespace ContactMesh.Core.Logging;

public static class SyncLogLevel
{
    public const string Information = "Information";
    public const string Warning = "Warning";
    public const string Error = "Error";
}

public sealed record SyncLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? TargetUserId = null,
    SyncOperationType? OperationType = null,
    string? SourceId = null,
    string? Reason = null);
