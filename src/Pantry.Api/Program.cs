using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration);

// Modules register here: builder.Services.Add<Name>Module() (see docs/architecture.md).

var app = builder.Build();

await app.MigrateModulesAsync();

// The Angular app is published into wwwroot (see Dockerfile). In development it runs on `ng serve`.
app.UseDefaultFiles();
app.MapStaticAssets();

var api = app.MapGroup("/api");
api.MapHealthEndpoint();

// Unknown /api routes must return 404, everything else falls back to the SPA.
api.MapFallback(() => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
