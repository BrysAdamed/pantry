using Microsoft.AspNetCore.DataProtection;
using Pantry.Api.Infrastructure.Database;

namespace Pantry.Api.Infrastructure.DataProtection;

public static class DataProtectionServiceCollectionExtensions
{
    public const string ApplicationName = "Pantry";

    /// <summary>
    /// Keeps the Data Protection key ring in Postgres (ADR 0015), so sessions and antiforgery tokens
    /// survive recreating the container.
    /// </summary>
    public static IServiceCollection AddPersistentDataProtection(this IServiceCollection services)
    {
        services.AddModuleDbContext<DataProtectionKeysDbContext>(DataProtectionKeysDbContext.Schema);
        services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<DataProtectionKeysDbContext>();
        return services;
    }
}
