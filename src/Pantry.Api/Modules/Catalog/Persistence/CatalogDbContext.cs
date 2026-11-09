using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;
using Pantry.Api.Modules.Catalog.Domain;

namespace Pantry.Api.Modules.Catalog.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";
    public const string NameKeyIndex = "ix_ingredient_names_key";

    private const int CodeMaxLength = 32;

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    /// <summary>
    /// Locks the rows of the ingredients until the current transaction ends, always in the same order, so merges and
    /// deletes of the same ingredients run one after another without deadlocking.
    /// </summary>
    public Task LockIngredientsAsync(Guid[] ids, CancellationToken cancellationToken) =>
        Database.ExecuteSqlAsync($"SELECT 1 FROM catalog.ingredients WHERE id = ANY({ids}) ORDER BY id FOR UPDATE", cancellationToken);

    public static bool IsDuplicateName(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NameKeyIndex };

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Ingredient>(ingredient =>
        {
            ingredient.ToTable("ingredients", table => table.HasCheckConstraint(
                "ck_ingredients_base_unit",
                $"base_unit IN ({string.Join(", ", Units.BaseUnits.Select(u => $"'{CatalogCode.Of(u)}'"))})"));
            ingredient.Property(i => i.Id).ValueGeneratedNever();
            ingredient.Property(i => i.Name).HasMaxLength(Ingredient.NameMaxLength);
            ingredient.Property(i => i.NameKey).HasMaxLength(Ingredient.NameMaxLength * 2);
            ingredient.Property(i => i.BaseUnit).HasConversion(new CodeConverter<Unit>()).HasMaxLength(CodeMaxLength);
            ingredient.Property(i => i.Category).HasConversion(new CodeConverter<IngredientCategory>()).HasMaxLength(CodeMaxLength);
            ingredient.HasIndex(i => i.NameKey);
            ingredient.Ignore(i => i.Aliases);

            ingredient.OwnsMany(i => i.Names, name =>
            {
                name.ToTable("ingredient_names");
                name.WithOwner().HasForeignKey("IngredientId");
                name.HasKey("IngredientId", nameof(IngredientName.Key));
                name.Property(n => n.Value).HasMaxLength(Ingredient.NameMaxLength);
                name.Property(n => n.Key).HasMaxLength(Ingredient.NameMaxLength * 2);
                name.HasIndex(n => n.Key).IsUnique().HasDatabaseName(NameKeyIndex);
            });
            ingredient.Navigation(i => i.Names).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private sealed class CodeConverter<TEnum>() : ValueConverter<TEnum, string>(
        value => CatalogCode.Of(value),
        code => CatalogCode.Parse<TEnum>(code))
        where TEnum : struct, Enum;
}
