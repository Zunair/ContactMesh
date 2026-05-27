using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactMesh.Core.Notifications;
using ContactMesh.Microsoft365.Auth;

namespace ContactMesh.Microsoft365.Notifications;

public sealed class MicrosoftGraphMailNotificationSender : IRunNotificationSender
{
    private static readonly Uri DefaultBaseAddress = new("https://graph.microsoft.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly IMicrosoftGraphAccessTokenProvider accessTokenProvider;
    private readonly Uri baseAddress;

    public MicrosoftGraphMailNotificationSender(
        HttpClient httpClient,
        IMicrosoftGraphAccessTokenProvider accessTokenProvider,
        Uri? baseAddress = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        this.httpClient = httpClient;
        this.accessTokenProvider = accessTokenProvider;
        this.baseAddress = baseAddress ?? httpClient.BaseAddress ?? DefaultBaseAddress;
    }

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.From))
        {
            throw new InvalidOperationException("NotificationMessage.From must be set for Microsoft Graph sendMail.");
        }
        if (message.To.Count == 0)
        {
            throw new InvalidOperationException("NotificationMessage.To must contain at least one recipient.");
        }

        var payload = new SendMailPayload
        {
            Message = new GraphMessage
            {
                Subject = message.Subject,
                Body = new GraphItemBody
                {
                    ContentType = "Text",
                    Content = message.Body
                },
                ToRecipients = message.To
                    .Select(address => new GraphRecipient { EmailAddress = new GraphEmailAddress { Address = address } })
                    .ToList(),
                Attachments = message.Attachments.Count == 0
                    ? null
                    : message.Attachments
                        .Select(attachment => new GraphFileAttachment
                        {
                            Name = attachment.FileName,
                            ContentType = attachment.ContentType,
                            ContentBytes = Convert.ToBase64String(attachment.Content)
                        })
                        .ToList()
            },
            SaveToSentItems = false
        };

        var requestUri = new Uri(this.baseAddress, $"v1.0/users/{Uri.EscapeDataString(message.From.Trim())}/sendMail");
        var accessToken = await this.accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Microsoft Graph access token provider returned an empty token.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var excerpt = body.Length <= 2048 ? body : body[..2048];
        throw new HttpRequestException(
            $"Microsoft Graph sendMail failed: {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {excerpt}");
    }

    private sealed class SendMailPayload
    {
        [JsonPropertyName("message")] public GraphMessage Message { get; set; } = new();
        [JsonPropertyName("saveToSentItems")] public bool SaveToSentItems { get; set; }
    }

    private sealed class GraphMessage
    {
        [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
        [JsonPropertyName("body")] public GraphItemBody Body { get; set; } = new();
        [JsonPropertyName("toRecipients")] public List<GraphRecipient> ToRecipients { get; set; } = new();
        [JsonPropertyName("attachments")] public List<GraphFileAttachment>? Attachments { get; set; }
    }

    private sealed class GraphItemBody
    {
        [JsonPropertyName("contentType")] public string ContentType { get; set; } = "Text";
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class GraphRecipient
    {
        [JsonPropertyName("emailAddress")] public GraphEmailAddress EmailAddress { get; set; } = new();
    }

    private sealed class GraphEmailAddress
    {
        [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;
    }

    private sealed class GraphFileAttachment
    {
        [JsonPropertyName("@odata.type")] public string OdataType { get; set; } = "#microsoft.graph.fileAttachment";
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("contentType")] public string ContentType { get; set; } = "application/octet-stream";
        [JsonPropertyName("contentBytes")] public string ContentBytes { get; set; } = string.Empty;
    }
}
