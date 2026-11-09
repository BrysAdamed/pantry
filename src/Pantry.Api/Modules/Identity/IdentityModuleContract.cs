using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity;

internal sealed class IdentityModuleContract(IdentityModuleDbContext db) : IIdentityModule
{
    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.AsNoTracking().Select(u => new { u.Id, u.Role }).ToListAsync(cancellationToken);
        return users.Select(u => new UserAccount(u.Id, ToAccountRole(u.Role))).ToList();
    }

    public static AccountRole ToAccountRole(UserRole role) => role switch
    {
        UserRole.Admin => AccountRole.Admin,
        UserRole.User => AccountRole.User,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static UserRole ToUserRole(AccountRole role) => role switch
    {
        AccountRole.Admin => UserRole.Admin,
        AccountRole.User => UserRole.User,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
