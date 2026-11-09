using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Tests.Infrastructure.TestModule;
using Testcontainers.PostgreSql;

namespace Pantry.Api.Tests.Infrastructure;

public sealed class PantryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _database.GetConnectionString();

    public Task InitializeAsync() => _database.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseServiceCollectionExtensions.ConnectionStringName}", ConnectionString);
        builder.ConfigureTestServices(services => services.AddModuleDbContext<TestModuleDbContext>(TestModuleDbContext.Schema));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PantryApiFactory>
{
    public const string Name = "api";
}
