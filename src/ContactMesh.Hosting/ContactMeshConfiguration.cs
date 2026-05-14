using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContactMesh.Hosting;

public static class ContactMeshConfiguration
{
    public static string ResolveConfigPath(IEnumerable<string> args)
    {
        return args.FirstOrDefault(arg => arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ?? "appsettings.json";
    }

    public static IConfigurationBuilder AddContactMeshConfigFile(
        this IConfigurationBuilder configuration,
        string configPath,
        IEnumerable<string>? args = null)
    {
        if (File.Exists(configPath))
        {
            configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
        }

        configuration.AddEnvironmentVariables();

        if (args is not null)
        {
            configuration.AddCommandLine(args.ToArray());
        }

        return configuration;
    }

    public static IServiceCollection AddContactMeshOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ContactMeshOptions>()
            .Bind(configuration.GetSection(ContactMeshOptions.SectionName));
        services.AddOptions<SyncRuleOptions>()
            .Bind(configuration.GetSection($"{ContactMeshOptions.SectionName}:Rules"));
        services.AddOptions<GoogleWorkspaceOptions>()
            .Bind(configuration.GetSection(GoogleWorkspaceOptions.SectionName));
        services.AddOptions<Microsoft365Options>()
            .Bind(configuration.GetSection(Microsoft365Options.SectionName));

        return services;
    }
}
