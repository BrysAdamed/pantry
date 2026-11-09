using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Household.Domain;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household;

internal sealed class HouseholdModuleContract(HouseholdDbContext db, DbContextOptions<HouseholdDbContext> options) : IHouseholdModule
{
    public async Task<IReadOnlyList<HouseholdMember>> GetMembersAsync(CancellationToken cancellationToken = default) =>
        await db.Members.AsNoTracking()
            .OrderBy(m => m.Position)
            .Select(m => new HouseholdMember(m.Id, m.Name, m.PortionFactor, m.Position, m.UserId))
            .ToListAsync(cancellationToken);

    public Task<decimal> GetHouseholdServingAsync(CancellationToken cancellationToken = default) =>
        db.Members.SumAsync(m => m.PortionFactor, cancellationToken);

    public async Task<IReadOnlyList<HouseholdMealSlot>> GetEnabledMealSlotsAsync(CancellationToken cancellationToken = default) =>
        await db.MealSlots.AsNoTracking()
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.Position)
            .Select(s => new HouseholdMealSlot(s.Id, s.Name))
            .ToListAsync(cancellationToken);

    public async Task CompleteFirstRunAsync(FirstRunAdmin admin, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = new HouseholdDbContext(options);
        await joined.Database.JoinTransactionAsync(transaction, cancellationToken);

        if (!await joined.Members.AnyAsync(m => m.UserId == admin.UserId, cancellationToken))
        {
            var position = await joined.Members.CountAsync(cancellationToken);
            joined.Members.Add(new Member { Name = admin.Name, UserId = admin.UserId, Position = position });
        }

        if (!await joined.MealSlots.AnyAsync(cancellationToken))
        {
            joined.MealSlots.AddRange(MealSlot.CreateDefaults(admin.UiLanguage));
        }

        await joined.SaveChangesAsync(cancellationToken);
    }
}
