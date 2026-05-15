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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContactMesh.Hosting;

public static class ContactMeshHostFactory
{
    public static IServiceCollection AddContactMeshApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddContactMeshOptions(configuration);
        services.AddSingleton<HttpClient>();
        services.AddSingleton(Create);

        return services;
    }

    public static ContactSyncOrchestrator Create(IServiceProvider services)
    {
        var contactMesh = services.GetRequiredService<IOptions<ContactMeshOptions>>().Value;
        var googleWorkspace = services.GetRequiredService<IOptions<GoogleWorkspaceOptions>>().Value;
        var microsoft365 = services.GetRequiredService<IOptions<Microsoft365Options>>().Value;
        var httpClient = services.GetRequiredService<HttpClient>();

        return Create(contactMesh, googleWorkspace, microsoft365, httpClient);
    }

    public static ContactSyncOrchestrator Create(
        ContactMeshOptions contactMesh,
        GoogleWorkspaceOptions googleWorkspace,
        Microsoft365Options microsoft365,
        HttpClient httpClient)
    {
        return contactMesh.Provider.Trim().ToUpperInvariant() switch
        {
            "GOOGLE" => CreateGoogle(googleWorkspace, httpClient),
            "MICROSOFT365" or "MICROSOFT" => CreateMicrosoft365(microsoft365, httpClient),
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

    private static ContactSyncOrchestrator CreateMicrosoft365(Microsoft365Options options, HttpClient httpClient)
    {
        var graphClientFactory = new MicrosoftGraphClientFactory(options);
        var accessTokenProvider = graphClientFactory.CreateAccessTokenProvider(httpClient);
        var directoryClient = new MicrosoftGraphDirectoryClient(httpClient, accessTokenProvider);
        var groupClient = new MicrosoftGraphGroupClient(httpClient, accessTokenProvider);
        var contactClient = new MicrosoftGraphContactClient(httpClient, accessTokenProvider);
        var contactWriter = new MicrosoftContactBatchWriter(contactClient);

        return new ContactSyncOrchestrator(
            new MicrosoftUserProvider(directoryClient),
            new MicrosoftGroupProvider(groupClient),
            new MicrosoftContactProvider(contactClient, contactWriter));
    }

    private static ContactSyncOrchestrator CreateScaffolded()
    {
        return new ContactSyncOrchestrator(
            new EmptyDirectoryProvider(),
            new EmptyGroupProvider(),
            new EmptyContactProvider());
    }

    private sealed class EmptyDirectoryProvider : IDirectoryProvider
    {
        public Task<IReadOnlyList<MeshUser>> GetUsersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<MeshUser>>(Array.Empty<MeshUser>());
        }
    }

    private sealed class EmptyGroupProvider : IGroupProvider
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

    private sealed class EmptyContactProvider : IContactProvider
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
}
