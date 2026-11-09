using System.Text.Json;
using System.Text.Json.Serialization;
using Pantry.Api.Infrastructure.Antiforgery;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Infrastructure.DataProtection;
using Pantry.Api.Infrastructure.Health;
using Pantry.Api.Infrastructure.Http;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddPersistentDataProtection();
builder.Services.AddApiAntiforgery();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddHouseholdModule();
builder.Services.AddCatalogModule();

var app = builder.Build();

await app.MigrateModulesAsync();

// The Angular app is published into wwwroot (see Dockerfile). In development it runs on `ng serve`.
app.UseDefaultFiles();
app.MapStaticAssets();

app.UseAuthentication();
app.UseApiNoStore();
app.UseApiAntiforgeryTokens();
app.UseAuthorization();
app.UseApiAntiforgeryValidation();

// Every /api endpoint requires a signed-in user unless it opts out with AllowAnonymous (ADR 0014).
var api = app.MapGroup("/api").RequireAuthorization();
api.MapHealthEndpoint();
api.MapAntiforgeryTokenEndpoint();
api.MapIdentityEndpoints();
api.MapHouseholdEndpoints();
api.MapCatalogEndpoints();

// Unknown /api routes must return 404, everything else falls back to the SPA.
api.MapFallback(() => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
