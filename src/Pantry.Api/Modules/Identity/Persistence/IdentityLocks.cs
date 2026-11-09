using Microsoft.EntityFrameworkCore;

namespace Pantry.Api.Modules.Identity.Persistence;

/// <summary>Postgres advisory locks held until the current transaction ends.</summary>
internal static class IdentityLocks
{
    private const long FirstRunKey = 0x5041_4E54_5259_0001;
    private const long UserAdministrationKey = 0x5041_4E54_5259_0002;

    /// <summary>Serializes first runs, so two concurrent ones create one admin.</summary>
    public static Task LockFirstRunAsync(this IdentityModuleDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({FirstRunKey})", cancellationToken);

    /// <summary>
    /// Serializes changes to users, passkey links (issue, revoke, redeem, remove, change role) and passkey removal,
    /// so checks like "single use", "last admin" and "last passkey" hold under concurrent requests.
    /// </summary>
    public static Task LockUserAdministrationAsync(this IdentityModuleDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({UserAdministrationKey})", cancellationToken);
}
