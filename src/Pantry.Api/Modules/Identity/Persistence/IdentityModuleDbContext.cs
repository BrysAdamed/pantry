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
        builder.Entity<IdentityUserPasskey<Guid>>().ToTable("user_passkeys");
    }

    private sealed class LowercaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
        value => value.ToString().ToLowerInvariant(),
        value => Enum.Parse<TEnum>(value, true))
        where TEnum : struct, Enum;
}
