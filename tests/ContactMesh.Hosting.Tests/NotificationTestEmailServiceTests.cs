using System.Net;
using System.Text.Json;
using ContactMesh.Core.Models;
using ContactMesh.Core.Notifications;
using ContactMesh.Hosting.Notifications;
using ContactMesh.Microsoft365.Auth;
using Xunit;

namespace ContactMesh.Hosting.Tests;

public sealed class NotificationTestEmailServiceTests
{
    [Fact]
    public async Task SendAsync_Sends_Test_Message_To_Configured_Recipients()
    {
        var handler = new RecordingHandler();
        var service = new NotificationTestEmailService(new HttpClient(handler));
        var contactMesh = new ContactMeshOptions
        {
            Provider = "Microsoft365",
            Notifications = new()
            {
                Enabled = true,
                From = "sender@example.org",
                SuccessTo = new[] { "success@example.org", "both@example.org" },
                FailureTo = new[] { "failure@example.org", "both@example.org" },
                SubjectPrefix = "[Mesh]"
            }
        };
        var microsoft365 = new Microsoft365Options
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        };

        var result = await service.SendAsync(
            contactMesh,
            microsoft365,
            "appsettings.local.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(RunNotificationOutcome.Sent, result.Outcome);
        var request = Assert.Single(handler.SendMailRequests);
        Assert.Equal("https://graph.microsoft.com/v1.0/users/sender%40example.org/sendMail", request.Uri?.ToString());
        Assert.Equal("Bearer test-token", request.Authorization);

        using var doc = JsonDocument.Parse(request.Body!);
        var message = doc.RootElement.GetProperty("message");
        Assert.Equal("[Mesh] Test notification", message.GetProperty("subject").GetString());
        Assert.Contains("settings dashboard", message.GetProperty("body").GetProperty("content").GetString());
        var recipients = message.GetProperty("toRecipients").EnumerateArray()
            .Select(value => value.GetProperty("emailAddress").GetProperty("address").GetString())
            .ToArray();
        Assert.Equal(new[] { "success@example.org", "both@example.org", "failure@example.org" }, recipients);
    }

    [Fact]
    public async Task SendAsync_Skips_When_Provider_Has_No_Sender()
    {
        var service = new NotificationTestEmailService(new HttpClient(new RecordingHandler()));

        var result = await service.SendAsync(
            new ContactMeshOptions { Provider = "Google" },
            new Microsoft365Options(),
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(RunNotificationOutcome.Skipped, result.Outcome);
        Assert.Contains("No notification sender", result.Reason);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> SendMailRequests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (request.RequestUri?.Host == "login.microsoftonline.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"test-token"}""")
                };
            }

            this.SendMailRequests.Add(new RecordedRequest(
                request.RequestUri,
                request.Headers.Authorization?.ToString(),
                body));
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    private sealed record RecordedRequest(Uri? Uri, string? Authorization, string? Body);
}
