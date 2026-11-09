using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Identity.Auth;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Features.PasskeyLinks;
using Pantry.Api.Modules.Identity.Features.Passkeys;
using Pantry.Api.Modules.Identity.Features.Redeem;
using Pantry.Api.Modules.Identity.Features.Setup;
using Pantry.Api.Modules.Identity.Features.SignIn;
using Pantry.Api.Modules.Identity.Features.Users;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityModuleDbContext>(IdentityModuleDbContext.Schema);
        services.AddScoped<IIdentityModule, IdentityModuleContract>();

        services.AddOptions<PasskeySettings>()
            .Bind(configuration.GetSection(PasskeySettings.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<SessionSettings>()
            .Bind(configuration.GetSection(SessionSettings.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.TwoFactorUserIdScheme)
            .AddCookie(IdentityConstants.TwoFactorRememberMeScheme);
        services.AddAuthorization(IdentityPolicies.Add);
        services.TryAddSingleton(TimeProvider.System);
        services.AddIdentityCore<User>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<UserClaimsPrincipalFactory>();

        services.AddOptions<IdentityPasskeyOptions>().Configure<IOptions<PasskeySettings>>(PasskeyConfiguration.Apply);
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<SessionSettings>>(SessionCookieConfiguration.ApplySession);
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme)
            .Configure(SessionCookieConfiguration.ApplyPasskeyCeremony);
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme)
            .Configure(SessionCookieConfiguration.ApplyRememberMe);
        services.AddOptions<SecurityStampValidatorOptions>()
            .Configure<IOptions<SessionSettings>>((options, settings) => options.ValidationInterval = settings.Value.ValidationInterval);

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder api)
    {
        var setup = api.MapGroup("/setup").AllowAnonymous();
        setup.MapGetSetupStatus();
        setup.MapCreateFirstRunPasskeyOptions();
        setup.MapCompleteFirstRun();

        var auth = api.MapGroup("/auth").AllowAnonymous();
        auth.MapCreateSignInPasskeyOptions();
        auth.MapSignInWithPasskey();
        auth.MapSignOut();

        var me = api.MapGroup("/me");
        me.MapGetMe();
        me.MapChangeUiLanguage();
        me.MapChangeTheme();
        me.MapSignOutOtherSessions();

        var passkeys = me.MapGroup("/passkeys");
        passkeys.MapGetMyPasskeys();
        passkeys.MapCreatePasskeyOptions();
        passkeys.MapAddPasskey();
        passkeys.MapRenamePasskey();
        passkeys.MapRemovePasskey();

        var links = api.MapGroup("/passkey-links").RequireAuthorization(IdentityPolicies.Admin);
        links.MapGetOpenPasskeyLinks();
        links.MapIssueInvite();
        links.MapIssueRecoveryLink();
        links.MapRevokePasskeyLink();

        var redeem = api.MapGroup("/passkey-links/redeem/{token}").AllowAnonymous();
        redeem.MapGetRedeemableLink();
        redeem.MapCreateRedeemPasskeyOptions();
        redeem.MapCompleteRedeem();

        var users = api.MapGroup("/users").RequireAuthorization(IdentityPolicies.Admin);
        users.MapRemoveUser();
        users.MapChangeUserRole();
        return api;
    }
}
