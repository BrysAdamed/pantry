using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Tests.Infrastructure.TestModule;
using Testcontainers.PostgreSql;

namespace Pantry.Api.Tests.Infrastructure;

public sealed class PantryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string RelyingPartyId = "localhost";
    public static readonly Uri BaseAddress = new("https://localhost");
    public static readonly string Origin = BaseAddress.GetLeftPart(UriPartial.Authority);

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _database.GetConnectionString();

    public Task InitializeAsync() => _database.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>A browser-like client: keeps cookies over HTTPS (session cookies are Secure) and sends the antiforgery header.</summary>
    public HttpClient CreateBrowserClient(CookieContainerHandler? cookies = null) => CreateBrowserClient(this, cookies);

    /// <summary>A browser-like client for <paramref name="app"/>, for example a restarted app made by <c>WithWebHostBuilder</c>.</summary>
    public static HttpClient CreateBrowserClient(WebApplicationFactory<Program> app, CookieContainerHandler? cookies = null)
    {
        cookies ??= new CookieContainerHandler();
        return app.CreateDefaultClient(BaseAddress, new AntiforgeryHandler(cookies), cookies);
    }

    public async Task DeleteAllUsersAsync()
    {
        _ = Services;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("TRUNCATE identity.users CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseServiceCollectionExtensions.ConnectionStringName}", ConnectionString);
        builder.UseSetting("Identity:Passkeys:RelyingPartyId", RelyingPartyId);
        builder.UseSetting("Identity:Passkeys:Origins:0", Origin);
        builder.ConfigureTestServices(services => services.AddModuleDbContext<TestModuleDbContext>(TestModuleDbContext.Schema));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PantryApiFactory>
{
    public const string Name = "api";
}
