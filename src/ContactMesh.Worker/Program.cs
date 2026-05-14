using System.Text.Json;
using ContactMesh.Core.Abstractions;
using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using ContactMesh.Google.Auth;
using ContactMesh.Google.Contacts;
using ContactMesh.Google.Directory;
using ContactMesh.Google.Groups;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Microsoft365.Contacts;
using ContactMesh.Microsoft365.Directory;
using ContactMesh.Microsoft365.Groups;
using ContactMesh.Worker.Jobs;

var configPath = args.FirstOrDefault(arg => arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ?? "appsettings.json";
var settings = AppSettings.Load(configPath);

Console.WriteLine("ContactMesh Worker");
Console.WriteLine($"Provider: {settings.ContactMesh.Provider}");
Console.WriteLine($"Dry run: {settings.ContactMesh.DryRun}");
Console.WriteLine($"Config: {configPath}");

using var httpClient = new HttpClient();
var orchestrator = ContactMeshHostFactory.Create(settings, httpClient);
var job = new ContactSyncJob(settings.ContactMesh, orchestrator);
await job.RunAsync(CancellationToken.None).ConfigureAwait(false);

internal sealed record AppSettings(
    ContactMeshOptions ContactMesh,
    GoogleWorkspaceOptions GoogleWorkspace,
    Microsoft365Options Microsoft365)
{
    public static AppSettings Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new AppSettings(new ContactMeshOptions(), new GoogleWorkspaceOptions(), new Microsoft365Options());
        }

        return JsonSerializer.Deserialize<AppSettings>(
            File.ReadAllText(configPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new AppSettings(new ContactMeshOptions(), new GoogleWorkspaceOptions(), new Microsoft365Options());
    }
}

internal static class ContactMeshHostFactory
{
    public static ContactSyncOrchestrator Create(AppSettings settings, HttpClient httpClient)
    {
        return settings.ContactMesh.Provider.Trim().ToUpperInvariant() switch
        {
            "GOOGLE" => CreateGoogle(settings.GoogleWorkspace, httpClient),
            "MICROSOFT365" or "MICROSOFT" => CreateMicrosoft365(),
            _ => CreateScaffolded()
        };
    }

    private static ContactSyncOrchestrator CreateGoogle(GoogleWorkspaceOptions options, HttpClient httpClient)
    {
        var tokenProvider = new GoogleDelegatedAccessTokenProvider(options);
        var contactClient = new GooglePeopleContactClient(httpClient, tokenProvider);
        var labelClient = new GooglePeopleContactGroupLabelClient(httpClient, tokenProvider);
        var writer = new GoogleContactBatchWriter(contactClient: contactClient, labelClient: labelClient);

        return new ContactSyncOrchestrator(
            new GoogleUserProvider(),
            new GoogleGroupProvider(),
            new GooglePeopleContactProvider(contactClient, labelClient, writer));
    }

    private static ContactSyncOrchestrator CreateMicrosoft365()
    {
        return new ContactSyncOrchestrator(
            new MicrosoftUserProvider(),
            new MicrosoftGroupProvider(),
            new MicrosoftContactProvider());
    }

    private static ContactSyncOrchestrator CreateScaffolded()
    {
        return new ContactSyncOrchestrator(
            new EmptyDirectoryProvider(),
            new EmptyGroupProvider(),
            new EmptyContactProvider());
    }
}

internal sealed class EmptyDirectoryProvider : IDirectoryProvider
{
    public Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshUser>>(Array.Empty<MeshUser>());
    }
}

internal sealed class EmptyGroupProvider : IGroupProvider
{
    public Task<IReadOnlyList<MeshGroup>> GetGroupsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshGroup>>(Array.Empty<MeshGroup>());
    }

    public Task<IReadOnlyList<MeshContact>> GetGroupContactsAsync(string groupId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
    }
}

internal sealed class EmptyContactProvider : IContactProvider
{
    public Task<IReadOnlyList<MeshContact>> GetContactsAsync(string userId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MeshContact>>(Array.Empty<MeshContact>());
    }

    public Task ApplyChangesAsync(string userId, ContactChangeSet changes, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
