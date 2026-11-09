using Microsoft.EntityFrameworkCore;

namespace Pantry.Api.Modules.Identity.Persistence;

/// <summary>Postgres advisory locks held until the current transaction ends.</summary>
internal static class IdentityLocks
{
    private const long FirstRunKey = 0x5041_4E54_5259_0001;
    private const long UserAdministrationKey = 0x5041_4E54_5259_0002;
    private const int PasswordCheckSpace = 0x5041_4E01;

    /// <summary>Serializes first runs, so two concurrent ones create one admin.</summary>
    public static Task LockFirstRunAsync(this IdentityModuleDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({FirstRunKey})", cancellationToken);

    /// <summary>
    /// Serializes changes to users, passkey links (issue, revoke, redeem, remove, change role) and passkey removal,
    /// so checks like "single use", "last admin" and "last passkey" hold under concurrent requests.
    /// </summary>
    public static Task LockUserAdministrationAsync(this IdentityModuleDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({UserAdministrationKey})", cancellationToken);

    /// <summary>
    /// Serializes password checks of one user (sign-in, changing the password), so every failed attempt counts towards
    /// the lockout even when guesses arrive in parallel. Uses the two-key lock space, apart from the locks above; users
    /// whose keys collide only wait for each other.
    /// </summary>
    public static Task LockUserPasswordChecksAsync(this IdentityModuleDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var bytes = userId.ToByteArray();
        var userKey = BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4) ^ BitConverter.ToInt32(bytes, 8) ^ BitConverter.ToInt32(bytes, 12);
        return db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({PasswordCheckSpace}, {userKey})", cancellationToken);
    }
}
