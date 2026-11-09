using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Pantry.Api.Infrastructure.DataProtection;

public sealed class DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "data_protection";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<DataProtectionKey>().ToTable("keys");
    }
}
