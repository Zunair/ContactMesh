// File: IRunNotificationSender.cs
// Author: Zunair
// Producer: Copilot

namespace ContactMesh.Core.Notifications
{
    public sealed record NotificationMessage(
        string From,
        IReadOnlyList<string> To,
        string Subject,
        string Body,
        IReadOnlyList<NotificationAttachment> Attachments);

    public sealed record NotificationAttachment(string FileName, string ContentType, byte[] Content);

    public interface IRunNotificationSender
    {
        Task SendAsync(NotificationMessage message, CancellationToken cancellationToken);
    }
}
