using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Modules.Household.Persistence;

public sealed class HouseholdDbContext(DbContextOptions<HouseholdDbContext> options) : DbContext(options)
{
    public const string Schema = "household";

    public DbSet<Domain.Household> Households => Set<Domain.Household>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<MealSlot> MealSlots => Set<MealSlot>();

    public DbSet<PlanningRule> PlanningRules => Set<PlanningRule>();

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
