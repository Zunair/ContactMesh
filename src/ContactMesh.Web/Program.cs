using ContactMesh.Core.Models;
using ContactMesh.Hosting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var configPath = ContactMeshConfiguration.ResolveConfigPath(args);
builder.Configuration.AddContactMeshConfigFile(configPath, args);
builder.Services.AddContactMeshApp(builder.Configuration);

var app = builder.Build();

app.MapGet("/", (IOptions<ContactMeshOptions> options) => Results.Ok(new
{
    Name = "ContactMesh",
    Status = "Settings UI roadmap placeholder",
    Config = configPath,
    Provider = options.Value.Provider,
    DryRun = options.Value.DryRun
}));

app.Run();
