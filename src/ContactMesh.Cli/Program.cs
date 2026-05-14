using System.Text.Json;
using ContactMesh.Core.Models;

var configPath = args.FirstOrDefault(arg => arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ?? "appsettings.json";
var options = File.Exists(configPath)
    ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(configPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.ContactMesh ?? new ContactMeshOptions()
    : new ContactMeshOptions();

Console.WriteLine($"ContactMesh CLI");
Console.WriteLine($"Provider: {options.Provider}");
Console.WriteLine($"Dry run: {options.DryRun}");
Console.WriteLine("Provider implementations are scaffolded; wire credentials before running a live sync.");

internal sealed record AppSettings(ContactMeshOptions ContactMesh);
