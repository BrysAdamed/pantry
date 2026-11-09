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

    public async Task<IReadOnlyList<HouseholdPlanningRule>> GetEnabledPlanningRulesAsync(CancellationToken cancellationToken = default) =>
        await db.PlanningRules.AsNoTracking()
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Position)
            .Select(r => new HouseholdPlanningRule(r.Id, r.Text))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HouseholdDislike>> GetDislikesAsync(CancellationToken cancellationToken = default) =>
        await db.Dislikes.AsNoTracking()
            .OrderBy(d => d.Kind == DislikeKind.Never ? 0 : 1)
            .ThenBy(d => d.Id)
            .Select(d => new HouseholdDislike(d.IngredientId, d.Kind, d.Reason))
            .ToListAsync(cancellationToken);

    public async Task<ContentLanguage> GetContentLanguageAsync(CancellationToken cancellationToken = default)
    {
        var household = await db.Households.AsNoTracking().SingleAsync(cancellationToken);
        return household.ContentLanguage ?? ContentLanguage.Default;
    }

    public Task<int> GetCooldownDaysAsync(CancellationToken cancellationToken = default) =>
        db.Households.Select(h => h.CooldownDays).SingleAsync(cancellationToken);

    public async Task CompleteFirstRunAsync(FirstRunAdmin admin, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = await JoinAsync(transaction, cancellationToken);

        var household = await joined.Households.SingleAsync(cancellationToken);
        household.ContentLanguage ??= admin.Language;

        if (!await joined.Members.AnyAsync(m => m.UserId == admin.UserId, cancellationToken))
        {
            var position = await joined.Members.CountAsync(cancellationToken);
            joined.Members.Add(new Member { Name = admin.Name, UserId = admin.UserId, Position = position });
        }

        if (!await joined.MealSlots.AnyAsync(cancellationToken))
        {
            joined.MealSlots.AddRange(MealSlot.CreateDefaults(household.ContentLanguage));
        }

        await joined.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> LinkMemberToUserAsync(Guid memberId, Guid userId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = await JoinAsync(transaction, cancellationToken);
        var member = await joined.Members.FindAsync([memberId], cancellationToken);
        if (member is null || member.UserId is not null)
        {
            return false;
        }

        member.UserId = userId;
        await joined.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UnlinkUserAsync(Guid userId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = await JoinAsync(transaction, cancellationToken);
        await joined.Members.Where(m => m.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.UserId, (Guid?)null), cancellationToken);
    }

    private async Task<HouseholdDbContext> JoinAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        var joined = new HouseholdDbContext(options);
        await joined.Database.JoinTransactionAsync(transaction, cancellationToken);
        return joined;
    }
}
