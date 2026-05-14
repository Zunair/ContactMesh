var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Name = "ContactMesh", Status = "Settings UI roadmap placeholder" }));

app.Run();
