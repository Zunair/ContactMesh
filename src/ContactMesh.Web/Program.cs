using ContactMesh.Core.Models;
using ContactMesh.Google.Auth;
using ContactMesh.Hosting;
using ContactMesh.Microsoft365.Auth;
using ContactMesh.Web.Settings;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var configPath = ContactMeshConfiguration.ResolveConfigPath(args);
builder.Configuration.AddContactMeshConfigFile(configPath, args);
builder.Services.AddContactMeshApp(builder.Configuration);

var app = builder.Build();

app.MapGet("/", RenderSettings);
app.MapGet("/settings", RenderSettings);

IResult RenderSettings(
    IOptions<ContactMeshOptions> contactMesh,
    IOptions<GoogleWorkspaceOptions> googleWorkspace,
    IOptions<Microsoft365Options> microsoft365)
{
    return Results.Content(
        SettingsPageRenderer.Render(
            contactMesh.Value,
            googleWorkspace.Value,
            microsoft365.Value,
            configPath),
        "text/html; charset=utf-8");
}

app.Run();
