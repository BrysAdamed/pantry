using Microsoft.EntityFrameworkCore;

namespace Pantry.Api.Modules.Identity.Persistence;

/// <summary>
/// When a passkey last signed someone in. Identity's passkey data has no such field, so it is a column of our own
/// (<c>user_passkeys.last_used_at</c>) that Identity's store leaves alone.
/// </summary>
internal static class PasskeyUsage
{
    public const string LastUsedAt = nameof(LastUsedAt);

    public static Task RecordPasskeyUseAsync(
        this IdentityModuleDbContext db, byte[] credentialId, DateTimeOffset usedAt, CancellationToken cancellationToken = default) =>
        db.UserPasskeys
            .Where(p => p.CredentialId == credentialId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => EF.Property<DateTimeOffset?>(p, LastUsedAt), usedAt), cancellationToken);
}
