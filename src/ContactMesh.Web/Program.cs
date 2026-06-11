// File: Program.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Models;
using ContactMesh.Core.Security;
using ContactMesh.Google.Auth;
using ContactMesh.Hosting;
using ContactMesh.Hosting.Notifications;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Web.Settings;
using Microsoft.Extensions.Options;

namespace ContactMesh.Web
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configPath = ContactMeshConfiguration.ResolveConfigPath(args);
            builder.Configuration.AddContactMeshConfigFile(configPath, args);
            builder.Services.AddContactMeshApp(builder.Configuration);

            var app = builder.Build();

            app.MapGet(
                "/",
                (
                    IOptionsMonitor<ContactMeshOptions> contactMesh,
                    IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
                    IOptionsMonitor<Microsoft365Options> microsoft365) =>
                    RenderSettings(contactMesh, googleWorkspace, microsoft365, configPath));
            app.MapGet(
                "/settings",
                (
                    IOptionsMonitor<ContactMeshOptions> contactMesh,
                    IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
                    IOptionsMonitor<Microsoft365Options> microsoft365) =>
                    RenderSettings(contactMesh, googleWorkspace, microsoft365, configPath));
            app.MapPost(
                "/settings",
                (
                    HttpRequest request,
                    IOptionsMonitor<ContactMeshOptions> contactMesh,
                    IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
                    IOptionsMonitor<Microsoft365Options> microsoft365,
                    ISecretProtector secretProtector,
                    CancellationToken cancellationToken) =>
                    SaveSettings(request, contactMesh, googleWorkspace, microsoft365, secretProtector, configPath, cancellationToken));
            app.MapPost(
                "/settings/test-email",
                (
                    HttpRequest request,
                    IOptionsMonitor<ContactMeshOptions> contactMesh,
                    IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
                    IOptionsMonitor<Microsoft365Options> microsoft365,
                    NotificationTestEmailService testEmailService,
                    CancellationToken cancellationToken) =>
                    SendTestEmail(request, contactMesh, googleWorkspace, microsoft365, testEmailService, configPath, cancellationToken));

            app.Run();
        }

        private static IResult RenderSettings(
            IOptionsMonitor<ContactMeshOptions> contactMesh,
            IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
            IOptionsMonitor<Microsoft365Options> microsoft365,
            string configPath)
        {
            return Results.Content(
                SettingsPageRenderer.Render(
                    contactMesh.CurrentValue,
                    googleWorkspace.CurrentValue,
                    microsoft365.CurrentValue,
                    configPath,
                    null),
                "text/html; charset=utf-8");
        }

        private static async Task<IResult> SaveSettings(
            HttpRequest request,
            IOptionsMonitor<ContactMeshOptions> contactMesh,
            IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
            IOptionsMonitor<Microsoft365Options> microsoft365,
            ISecretProtector secretProtector,
            string configPath,
            CancellationToken cancellationToken)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var settings = SettingsFormModel.FromForm(
                form,
                contactMesh.CurrentValue,
                googleWorkspace.CurrentValue,
                microsoft365.CurrentValue);
            await settings.SaveAsync(configPath, secretProtector, cancellationToken);

            return Results.Content(
                SettingsPageRenderer.Render(
                    settings.ContactMesh,
                    settings.GoogleWorkspace,
                    settings.Microsoft365,
                    configPath,
                    "Settings saved to the JSON config file. The current page reflects the saved values."),
                "text/html; charset=utf-8");
        }

        private static async Task<IResult> SendTestEmail(
            HttpRequest request,
            IOptionsMonitor<ContactMeshOptions> contactMesh,
            IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
            IOptionsMonitor<Microsoft365Options> microsoft365,
            NotificationTestEmailService testEmailService,
            string configPath,
            CancellationToken cancellationToken)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var settings = SettingsFormModel.FromForm(
                form,
                contactMesh.CurrentValue,
                googleWorkspace.CurrentValue,
                microsoft365.CurrentValue);

            string notice;
            try
            {
                var result = await testEmailService
                    .SendAsync(settings.ContactMesh, settings.Microsoft365, configPath, cancellationToken)
                    .ConfigureAwait(false);
                notice = result.Outcome == ContactMesh.Core.Notifications.RunNotificationOutcome.Sent
                    ? result.Reason ?? "Test notification email sent."
                    : $"Test notification skipped: {result.Reason}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notice = $"Test notification failed: {ex.Message}";
            }

            return Results.Content(
                SettingsPageRenderer.Render(
                    settings.ContactMesh,
                    settings.GoogleWorkspace,
                    settings.Microsoft365,
                    configPath,
                    notice),
                "text/html; charset=utf-8");
        }
    }
}
