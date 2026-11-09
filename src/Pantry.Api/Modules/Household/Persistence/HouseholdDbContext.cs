using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Modules.Household.Persistence;

public sealed class HouseholdDbContext(DbContextOptions<HouseholdDbContext> options) : DbContext(options)
{
    public const string Schema = "household";
    public const string DislikeIngredientIndex = "ix_dislikes_ingredient_id";

    private const int DislikeKindMaxLength = 16;

    public DbSet<Domain.Household> Households => Set<Domain.Household>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<MealSlot> MealSlots => Set<MealSlot>();

    public DbSet<PlanningRule> PlanningRules => Set<PlanningRule>();

    public DbSet<Dislike> Dislikes => Set<Dislike>();

    public static bool IsDuplicateDislike(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: DislikeIngredientIndex };

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Domain.Household>(household =>
        {
            household.ToTable("households", table => table.HasCheckConstraint(
                "ck_households_singleton", $"id = {Domain.Household.SingletonId}"));
            household.Property(h => h.Id).ValueGeneratedNever();
            household.Property(h => h.ContentLanguage)
                .HasConversion(language => language!.Code, code => ContentLanguage.Parse(code))
                .HasMaxLength(ContentLanguage.CodeMaxLength);
            household.HasData(new Domain.Household());
        });

        builder.Entity<PlanningRule>(rule =>
        {
            rule.ToTable("planning_rules");
            rule.Property(r => r.Id).ValueGeneratedNever();
            rule.Property(r => r.Text).HasMaxLength(PlanningRule.TextMaxLength);
        });

        builder.Entity<Dislike>(dislike =>
        {
            dislike.ToTable("dislikes", table =>
            {
                table.HasCheckConstraint("ck_dislikes_kind", "kind IN ('never', 'avoid')");
                table.HasCheckConstraint("ck_dislikes_reason", "reason IS NULL OR kind = 'never'");
            });
            dislike.Property(d => d.Id).ValueGeneratedNever();
            dislike.Property(d => d.Kind)
                .HasConversion(kind => kind == DislikeKind.Never ? "never" : "avoid", code => code == "never" ? DislikeKind.Never : DislikeKind.Avoid)
                .HasMaxLength(DislikeKindMaxLength);
            dislike.Property(d => d.Reason).HasMaxLength(Dislike.ReasonMaxLength);
            dislike.HasIndex(d => d.IngredientId).IsUnique().HasDatabaseName(DislikeIngredientIndex);
        });

        builder.Entity<Dislike>(dislike =>
        {
            dislike.ToTable("dislikes", table =>
            {
                table.HasCheckConstraint("ck_dislikes_kind", "kind IN ('never', 'avoid')");
                table.HasCheckConstraint("ck_dislikes_reason", "reason IS NULL OR kind = 'never'");
            });
            dislike.Property(d => d.Id).ValueGeneratedNever();
            dislike.Property(d => d.Kind)
                .HasConversion(kind => kind == DislikeKind.Never ? "never" : "avoid", code => code == "never" ? DislikeKind.Never : DislikeKind.Avoid)
                .HasMaxLength(DislikeKindMaxLength);
            dislike.Property(d => d.Reason).HasMaxLength(Dislike.ReasonMaxLength);
            dislike.HasIndex(d => d.IngredientId).IsUnique().HasDatabaseName(DislikeIngredientIndex);
        });

        builder.Entity<Member>(member =>
        {
            member.ToTable("members", table => table.HasCheckConstraint(
                "ck_members_portion_factor",
                FormattableString.Invariant($"portion_factor >= {Member.MinPortionFactor} AND portion_factor <= {Member.MaxPortionFactor}")));
            member.Property(m => m.Id).ValueGeneratedNever();
            member.Property(m => m.Name).HasMaxLength(Member.NameMaxLength);
            member.Property(m => m.PortionFactor).HasPrecision(3, 2);
            member.HasIndex(m => m.UserId).IsUnique();
        });

        builder.Entity<MealSlot>(slot =>
        {
            slot.ToTable("meal_slots");
            slot.Property(s => s.Id).ValueGeneratedNever();
            slot.Property(s => s.Name).HasMaxLength(MealSlot.NameMaxLength);
        });
    }

    /// <summary>
    /// Locks the household row until the current transaction ends, so concurrent meal slot and planning rule writes
    /// that check the order or the enabled slots run one after another.
    /// </summary>
    public Task LockHouseholdAsync(CancellationToken cancellationToken) =>
        Database.ExecuteSqlAsync($"SELECT 1 FROM household.households FOR UPDATE", cancellationToken);
}
