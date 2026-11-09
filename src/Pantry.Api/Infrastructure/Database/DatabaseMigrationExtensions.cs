using Microsoft.EntityFrameworkCore;

namespace Pantry.Api.Infrastructure.Database;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateModulesAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrationExtensions));

        foreach (var registration in scope.ServiceProvider.GetServices<ModuleDbContextRegistration>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            logger.LogInformation("Migrating {Context} (schema {Schema})", registration.ContextType.Name, registration.Schema);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
