using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Pantry.Api.Infrastructure.Database;

public static class DatabaseServiceCollectionExtensions
{
    public const string ConnectionStringName = "Pantry";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing. Set ConnectionStrings__{ConnectionStringName}.");
        }

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }

    /// <summary>
    /// Registers a module's DbContext in its own Postgres schema, with its own migrations history table.
    /// Registered contexts are migrated on startup by <see cref="DatabaseMigrationExtensions.MigrateModulesAsync"/>.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention());
        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext), schema));
        return services;
    }
}

public sealed record ModuleDbContextRegistration(Type ContextType, string Schema);
