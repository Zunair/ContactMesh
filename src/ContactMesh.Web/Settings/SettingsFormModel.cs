using System.Text.Json;
using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Microsoft365.Auth;
using Microsoft.AspNetCore.Http;

namespace ContactMesh.Web.Settings;

public sealed record SettingsFormModel(
    ContactMeshOptions ContactMesh,
    GoogleWorkspaceOptions GoogleWorkspace,
    Microsoft365Options Microsoft365)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static SettingsFormModel FromForm(
        IFormCollection form,
        GoogleWorkspaceOptions currentGoogleWorkspace,
        Microsoft365Options currentMicrosoft365)
    {
        var microsoftClientSecret = Read(form, "Microsoft365.ClientSecret");
        var googleWorkspace = new GoogleWorkspaceOptions
        {
            ServiceAccountFile = Read(form, "GoogleWorkspace.ServiceAccountFile"),
            AdminUserEmail = Read(form, "GoogleWorkspace.AdminUserEmail"),
            Scopes = Lines(form, "GoogleWorkspace.Scopes")
        };
        var microsoft365 = new Microsoft365Options
        {
            TenantId = Read(form, "Microsoft365.TenantId"),
            ClientId = Read(form, "Microsoft365.ClientId"),
            ClientSecret = string.IsNullOrWhiteSpace(microsoftClientSecret)
                ? currentMicrosoft365.ClientSecret
                : microsoftClientSecret,
            Scopes = Lines(form, "Microsoft365.Scopes"),
            GroupTypes = Lines(form, "Microsoft365.GroupTypes")
        };
        var contactMesh = new ContactMeshOptions
        {
            Provider = Read(form, "ContactMesh.Provider"),
            DryRun = form.ContainsKey("ContactMesh.DryRun"),
            DisableDeletes = form.ContainsKey("ContactMesh.DisableDeletes"),
            ForceResetLabels = form.ContainsKey("ContactMesh.ForceResetLabels"),
            ForceDeduplicatePhones = form.ContainsKey("ContactMesh.ForceDeduplicatePhones"),
            ManagedEmailDomains = Lines(form, "ContactMesh.ManagedEmailDomains"),
            Rules = new SyncRuleOptions
            {
                MainContactsGroupEmail = Read(form, "ContactMesh.Rules.MainContactsGroupEmail"),
                MainContactsGroupLabel = Read(form, "ContactMesh.Rules.MainContactsGroupLabel"),
                GroupContactPrefix = Read(form, "ContactMesh.Rules.GroupContactPrefix"),
                TargetUsers = Lines(form, "ContactMesh.Rules.TargetUsers"),
                GlobalUserGroups = Lines(form, "ContactMesh.Rules.GlobalUserGroups"),
                GlobalExternalContactGroups = Lines(form, "ContactMesh.Rules.GlobalExternalContactGroups"),
                GroupsToSyncByGroup = Lines(form, "ContactMesh.Rules.GroupsToSyncByGroup"),
                ExclusionGroups = Lines(form, "ContactMesh.Rules.ExclusionGroups"),
                ScopedGroupRoots = Lines(form, "ContactMesh.Rules.ScopedGroupRoots"),
                IncludedOrganizationUnits = Lines(form, "ContactMesh.Rules.IncludedOrganizationUnits"),
                ExcludedOrganizationUnits = Lines(form, "ContactMesh.Rules.ExcludedOrganizationUnits"),
                GroupMappings = Mappings(form, "ContactMesh.Rules.GroupMappings")
            }
        };

        return new SettingsFormModel(contactMesh, googleWorkspace, microsoft365);
    }

    public async Task SaveAsync(string configPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(configPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new SettingsFile(this.ContactMesh, this.GoogleWorkspace, this.Microsoft365);
        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }

    private static string Read(IFormCollection form, string key)
    {
        return form.TryGetValue(key, out var value) ? value.ToString().Trim() : string.Empty;
    }

    private static IReadOnlyList<string> Lines(IFormCollection form, string key)
    {
        return Read(form, key)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<GroupMapping> Mappings(IFormCollection form, string key)
    {
        return Lines(form, key)
            .Select(ParseMapping)
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.From) && !string.IsNullOrWhiteSpace(mapping.To))
            .ToArray();
    }

    private static GroupMapping ParseMapping(string line)
    {
        var parts = line.Split("->", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? new GroupMapping(parts[0], parts[1])
            : new GroupMapping(line, string.Empty);
    }

    private sealed record SettingsFile(
        ContactMeshOptions ContactMesh,
        GoogleWorkspaceOptions GoogleWorkspace,
        Microsoft365Options Microsoft365);
}
