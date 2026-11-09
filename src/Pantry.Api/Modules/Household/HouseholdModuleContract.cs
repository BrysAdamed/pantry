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

    public async Task CompleteFirstRunAsync(FirstRunAdmin admin, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = await JoinAsync(transaction, cancellationToken);
        joined.Members.Add(new Member { Name = admin.Name, UserId = admin.UserId, Position = 0 });
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
