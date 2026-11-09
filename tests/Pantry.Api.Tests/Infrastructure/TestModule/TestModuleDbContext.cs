using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pantry.Api.Infrastructure.Database;

namespace Pantry.Api.Tests.Infrastructure.TestModule;

public sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : DbContext(options)
{
    public const string Schema = "test_module";

    public DbSet<TestItem> TestItems => Set<TestItem>();

    public DbSet<TestIngredientReference> IngredientReferences => Set<TestIngredientReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<TestIngredientReference>().HasIndex(r => r.IngredientId);
    }
}

public sealed class TestItem
{
    public Guid Id { get; init; }
    public required string DisplayName { get; set; }
}

public sealed class TestIngredientReference
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid IngredientId { get; set; }
}

public sealed class TestModuleDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TestModuleDbContext>
{
    public TestModuleDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<TestModuleDbContext>()
        .UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(DatabaseServiceCollectionExtensions.MigrationsHistoryTable, TestModuleDbContext.Schema))
        .UseSnakeCaseNamingConvention()
        .Options);
}
