using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Modules.Household.Persistence;

public sealed class HouseholdDbContext(DbContextOptions<HouseholdDbContext> options) : DbContext(options)
{
    public const string Schema = "household";

    public DbSet<Domain.Household> Households => Set<Domain.Household>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<MealSlot> MealSlots => Set<MealSlot>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Domain.Household>(household =>
        {
            household.ToTable("households", table => table.HasCheckConstraint(
                "ck_households_singleton", $"id = {Domain.Household.SingletonId}"));
            household.Property(h => h.Id).ValueGeneratedNever();
            household.HasData(new Domain.Household());
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
    /// Locks the household row until the current transaction ends, so concurrent meal slot writes that check
    /// the order or the enabled slots run one after another.
    /// </summary>
    public Task LockHouseholdAsync(CancellationToken cancellationToken) =>
        Database.ExecuteSqlAsync($"SELECT 1 FROM household.households FOR UPDATE", cancellationToken);
}
