using System.Net;
using System.Net.Http.Json;
using Pantry.Api.Infrastructure.Health;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointTests(PantryApiFactory factory)
{
    [Fact]
    public async Task Health_returns_ok_when_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", health?.Status);
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_instead_of_spa()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
