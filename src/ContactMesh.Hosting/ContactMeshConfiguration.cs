using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContactMesh.Hosting;

public static class ContactMeshConfiguration
{
    private const string LocalConfigFileName = "appsettings.local.json";
    private const string DefaultConfigFileName = "appsettings.json";

    public static string ResolveConfigPath(IEnumerable<string> args)
    {
        return args.FirstOrDefault(arg => arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            ?? FindDefaultConfigPath();
    }

    public static IConfigurationBuilder AddContactMeshConfigFile(
        this IConfigurationBuilder configuration,
        string configPath,
        IEnumerable<string>? args = null)
    {
        if (File.Exists(configPath))
        {
            configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
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

    private static string FindDefaultConfigPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var localPath = Path.Combine(directory.FullName, LocalConfigFileName);
            if (File.Exists(localPath))
            {
                return localPath;
            }

            var solutionPath = Path.Combine(directory.FullName, "ContactMesh.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(directory.FullName, DefaultConfigFileName);
            }

            directory = directory.Parent;
        }

        return DefaultConfigFileName;
    }
}
