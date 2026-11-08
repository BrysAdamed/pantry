using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Modules register their services here (see docs/architecture.md).

var app = builder.Build();

// The Angular app is published into wwwroot (see Dockerfile). In development it runs on `ng serve`.
app.UseDefaultFiles();
app.MapStaticAssets();

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new HealthResponse(
    "ok",
    Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev")));

// Unknown /api routes must return 404, everything else falls back to the SPA.
api.MapFallback(() => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public sealed record HealthResponse(string Status, string Version);

public partial class Program;
