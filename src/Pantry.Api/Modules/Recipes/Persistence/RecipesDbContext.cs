using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes.Domain;

namespace Pantry.Api.Modules.Recipes.Persistence;

public sealed class RecipesDbContext(DbContextOptions<RecipesDbContext> options) : DbContext(options)
{
    public const string Schema = "recipes";

    private const int CodeMaxLength = 32;

    public DbSet<Recipe> Recipes => Set<Recipe>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Recipe>(recipe =>
        {
            recipe.ToTable("recipes", table =>
            {
                table.HasCheckConstraint("ck_recipes_servings", "servings > 0");
                table.HasCheckConstraint("ck_recipes_prep_time_minutes", "prep_time_minutes IS NULL OR prep_time_minutes > 0");
                table.HasCheckConstraint("ck_recipes_cooldown_days", "cooldown_days IS NULL OR cooldown_days >= 0");
            });
            recipe.Property(r => r.Id).ValueGeneratedNever();
            recipe.Property(r => r.Name).HasMaxLength(RecipeContent.NameMaxLength);
            recipe.Property(r => r.SourceUrl).HasMaxLength(RecipeContent.SourceUrlMaxLength);
            recipe.Ignore(r => r.IsArchived);
            recipe.HasIndex(r => r.ArchivedAt);

            recipe.ComplexProperty(r => r.Nutrition, nutrition =>
            {
                nutrition.Property(n => n.Kcal).HasColumnName("kcal").HasPrecision(9, Nutrition.Scale);
                nutrition.Property(n => n.Protein).HasColumnName("protein").HasPrecision(9, Nutrition.Scale);
                nutrition.Property(n => n.Fat).HasColumnName("fat").HasPrecision(9, Nutrition.Scale);
                nutrition.Property(n => n.Carbs).HasColumnName("carbs").HasPrecision(9, Nutrition.Scale);
                nutrition.Ignore(n => n.IsEmpty);
            });

            recipe.OwnsMany(r => r.Tags, tag =>
            {
                tag.ToTable("recipe_tags");
                tag.WithOwner().HasForeignKey("RecipeId");
                tag.HasKey("RecipeId", nameof(RecipeTag.Value));
                tag.Property(t => t.Value).HasColumnName("tag").HasMaxLength(RecipeTag.MaxLength);
                tag.HasIndex(t => t.Value);
            });

            recipe.OwnsMany(r => r.Steps, step =>
            {
                step.ToTable("recipe_steps");
                step.WithOwner().HasForeignKey("RecipeId");
                step.HasKey("RecipeId", nameof(RecipeStep.Position));
                step.Property(s => s.Position).ValueGeneratedNever();
                step.Property(s => s.Text).HasMaxLength(RecipeStep.TextMaxLength);
            });

            recipe.OwnsMany(r => r.Lines, line =>
            {
                line.ToTable("recipe_ingredient_lines", table =>
                {
                    table.HasCheckConstraint("ck_recipe_ingredient_lines_quantity", "quantity IS NULL OR quantity > 0");
                    table.HasCheckConstraint(
                        "ck_recipe_ingredient_lines_unit", "(quantity IS NULL) = (unit IS NULL) AND (quantity IS NULL) = (quantity_per_serving IS NULL)");
                });
                line.WithOwner().HasForeignKey("RecipeId");
                line.HasKey("RecipeId", nameof(RecipeIngredientLine.Position));
                line.Property(l => l.Position).ValueGeneratedNever();
                line.Property(l => l.Section).HasMaxLength(RecipeContent.SectionNameMaxLength);
                line.Property(l => l.Quantity).HasPrecision(18, RecipeQuantities.QuantityScale);
                line.Property(l => l.QuantityPerServing).HasPrecision(18, RecipeQuantities.PerServingScale);
                line.Property(l => l.Unit).HasConversion(new UnitCodeConverter()).HasMaxLength(CodeMaxLength);
                line.Property(l => l.Note).HasMaxLength(RecipeIngredientLine.NoteMaxLength);
                line.Ignore(l => l.IsToTaste);
                line.HasIndex(l => l.IngredientId);
            });
            recipe.Navigation(r => r.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
            recipe.Navigation(r => r.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
            recipe.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private sealed class UnitCodeConverter() : ValueConverter<Unit, string>(
        unit => CatalogCode.Of(unit),
        code => CatalogCode.Parse<Unit>(code));
}
