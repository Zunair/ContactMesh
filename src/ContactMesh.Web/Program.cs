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
app.MapPost("/settings", SaveSettings);

IResult RenderSettings(
    IOptionsMonitor<ContactMeshOptions> contactMesh,
    IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
    IOptionsMonitor<Microsoft365Options> microsoft365)
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

async Task<IResult> SaveSettings(
    HttpRequest request,
    IOptionsMonitor<GoogleWorkspaceOptions> googleWorkspace,
    IOptionsMonitor<Microsoft365Options> microsoft365,
    CancellationToken cancellationToken)
{
    var form = await request.ReadFormAsync(cancellationToken);
    var settings = SettingsFormModel.FromForm(
        form,
        googleWorkspace.CurrentValue,
        microsoft365.CurrentValue);
    await settings.SaveAsync(configPath, cancellationToken);

    return Results.Content(
        SettingsPageRenderer.Render(
            settings.ContactMesh,
            settings.GoogleWorkspace,
            settings.Microsoft365,
            configPath,
            "Settings saved to the JSON config file. The current page reflects the saved values."),
        "text/html; charset=utf-8");
}

app.Run();
