using System.Net;
using System.Text;
using System.Text.Json;
using ContactMesh.Core.Notifications;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Microsoft365.Notifications;
using Xunit;

namespace ContactMesh.Microsoft365.Tests;

public sealed class MicrosoftGraphMailNotificationSenderTests
{
    [Fact]
    public async Task SendAsync_Posts_SendMail_With_Recipients_And_Attachments()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Accepted));
        var sender = new MicrosoftGraphMailNotificationSender(
            new HttpClient(handler),
            new FakeTokenProvider("graph-token"),
            new Uri("https://graph.test/"));

        var attachment = new NotificationAttachment("audit.csv", "text/csv", Encoding.UTF8.GetBytes("header\nrow"));
        var message = new NotificationMessage(
            From: "ops@example.com",
            To: new[] { "team@example.com", "oncall@example.com" },
            Subject: "Sync FAILED",
            Body: "details",
            Attachments: new[] { attachment });

        await sender.SendAsync(message, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://graph.test/v1.0/users/ops%40example.com/sendMail", request.Uri?.ToString());
        Assert.Equal("Bearer graph-token", request.Authorization);

        using var doc = JsonDocument.Parse(request.Body!);
        var msg = doc.RootElement.GetProperty("message");
        Assert.Equal("Sync FAILED", msg.GetProperty("subject").GetString());
        Assert.Equal("details", msg.GetProperty("body").GetProperty("content").GetString());

        var recipients = msg.GetProperty("toRecipients").EnumerateArray()
            .Select(value => value.GetProperty("emailAddress").GetProperty("address").GetString())
            .ToArray();
        Assert.Equal(new[] { "team@example.com", "oncall@example.com" }, recipients);

        var attachments = msg.GetProperty("attachments").EnumerateArray().ToArray();
        var single = Assert.Single(attachments);
        Assert.Equal("#microsoft.graph.fileAttachment", single.GetProperty("@odata.type").GetString());
        Assert.Equal("audit.csv", single.GetProperty("name").GetString());
        Assert.Equal("text/csv", single.GetProperty("contentType").GetString());
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("header\nrow")),
            single.GetProperty("contentBytes").GetString());

        Assert.False(doc.RootElement.GetProperty("saveToSentItems").GetBoolean());
    }

    [Fact]
    public async Task SendAsync_Throws_When_Graph_Returns_Failure()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("denied")
        });
        var sender = new MicrosoftGraphMailNotificationSender(
            new HttpClient(handler),
            new FakeTokenProvider("graph-token"),
            new Uri("https://graph.test/"));

        var message = new NotificationMessage(
            From: "ops@example.com",
            To: new[] { "team@example.com" },
            Subject: "Subject",
            Body: "Body",
            Attachments: Array.Empty<NotificationAttachment>());

        await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_Validates_From_And_Recipients()
    {
        var sender = new MicrosoftGraphMailNotificationSender(
            new HttpClient(new RecordingHandler()),
            new FakeTokenProvider("graph-token"),
            new Uri("https://graph.test/"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            new NotificationMessage(string.Empty, new[] { "a@b" }, "s", "b", Array.Empty<NotificationAttachment>()),
            CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            new NotificationMessage("from@example.com", Array.Empty<string>(), "s", "b", Array.Empty<NotificationAttachment>()),
            CancellationToken.None));
    }

    private sealed class FakeTokenProvider : IMicrosoftGraphAccessTokenProvider
    {
        private readonly string token;
        public FakeTokenProvider(string token) { this.token = token; }
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult(this.token);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;
        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            this.Requests.Add(new RecordedRequest(request.RequestUri, request.Headers.Authorization?.ToString(), body));
            return this.responses.Count == 0
                ? new HttpResponseMessage(HttpStatusCode.Accepted)
                : this.responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(Uri? Uri, string? Authorization, string? Body);
}
