using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Pantry.Api.Modules.Identity.Auth;

internal static class SessionCookieConfiguration
{
    public const string SessionCookieName = "pantry.session";
    public const string PasskeyCeremonyCookieName = "pantry.passkey";

    public static void ApplySession(CookieAuthenticationOptions options, IOptions<SessionSettings> settings)
    {
        options.Cookie.Name = SessionCookieName;
        Harden(options);
        options.ExpireTimeSpan = settings.Value.Lifetime;
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    public static void ApplyPasskeyCeremony(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = PasskeyCeremonyCookieName;
        Harden(options);
    }

    private static void Harden(CookieAuthenticationOptions options)
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    }
}
