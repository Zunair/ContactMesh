using ContactMesh.Core.Models;
using ContactMesh.Core.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContactMesh.Hosting;

public sealed class ContactMeshAppHost : IDisposable
{
    private readonly IHost host;

    private ContactMeshAppHost(IHost host, string configPath)
    {
        this.host = host;
        this.ConfigPath = configPath;
    }

    public string ConfigPath { get; }

    public IServiceProvider Services => this.host.Services;

    public ContactMeshOptions Options => this.Services.GetRequiredService<IOptions<ContactMeshOptions>>().Value;

    public ContactSyncOrchestrator Orchestrator => this.Services.GetRequiredService<ContactSyncOrchestrator>();

    public static ContactMeshAppHost Build(string[] args)
    {
        var configPath = ContactMeshConfiguration.ResolveConfigPath(args);
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddContactMeshConfigFile(configPath, args);
        builder.Services.AddContactMeshApp(builder.Configuration);

        return new ContactMeshAppHost(builder.Build(), configPath);
    }

    public void Dispose()
    {
        this.host.Dispose();
    }
}
