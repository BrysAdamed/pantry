using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity;

/// <summary>In-process contract of the Identity module for other modules.</summary>
public interface IIdentityModule
{
    /// <summary>All users who can sign in, with their role.</summary>
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default);
}

public sealed record UserAccount(Guid Id, UserRole Role);

internal sealed class IdentityModuleContract(IdentityModuleDbContext db) : IIdentityModule
{
    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().Select(u => new UserAccount(u.Id, u.Role)).ToListAsync(cancellationToken);
}
