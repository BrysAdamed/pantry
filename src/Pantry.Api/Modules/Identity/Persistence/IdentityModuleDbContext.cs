using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Persistence;

public sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
    : IdentityUserContext<User, Guid>(options)
{
    public const string Schema = "identity";

    private const string PendingLinkFilter = "used_at IS NULL AND revoked_at IS NULL";

    public DbSet<PasskeyLink> PasskeyLinks => Set<PasskeyLink>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength);
            user.Property(u => u.Role).HasConversion(new LowercaseEnumConverter<UserRole>()).HasMaxLength(16);
            user.Property(u => u.UiLanguage).HasConversion(new LowercaseEnumConverter<UiLanguage>()).HasMaxLength(8);
            user.Property(u => u.Theme).HasConversion(new LowercaseEnumConverter<ThemePreference>()).HasMaxLength(16);
        });
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityUserPasskey<Guid>>(passkey =>
        {
            passkey.ToTable("user_passkeys");
            passkey.Property<DateTimeOffset?>(PasskeyUsage.LastUsedAt);
        });

        builder.Entity<PasskeyLink>(link =>
        {
            link.ToTable("passkey_links", table => table.HasCheckConstraint(
                "ck_passkey_links_target", "(kind = 'invite') = (member_id IS NOT NULL) AND (kind = 'recovery') = (user_id IS NOT NULL)"));
            link.Property(l => l.Id).ValueGeneratedNever();
            link.Property(l => l.Kind).HasConversion(new LowercaseEnumConverter<PasskeyLinkKind>()).HasMaxLength(16);
            link.HasIndex(l => l.TokenHash).IsUnique();
            link.HasIndex(l => l.MemberId).IsUnique().HasFilter(PendingLinkFilter);
            link.HasIndex(l => l.UserId).IsUnique().HasFilter(PendingLinkFilter);
            link.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<User>().WithMany().HasForeignKey(l => l.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private sealed class LowercaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
        value => value.ToString().ToLowerInvariant(),
        value => Enum.Parse<TEnum>(value, true))
        where TEnum : struct, Enum;
}
