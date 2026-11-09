using Microsoft.AspNetCore.Antiforgery;

namespace Pantry.Api.Infrastructure.Antiforgery;

/// <summary>
/// Antiforgery for the JSON API: unsafe <c>/api</c> requests must send the <c>X-XSRF-TOKEN</c> header,
/// the matching token is handed out in the readable <c>XSRF-TOKEN</c> cookie (Angular's HttpClient default).
/// </summary>
public static class ApiAntiforgery
{
    public const string HeaderName = "X-XSRF-TOKEN";
    public const string RequestTokenCookieName = "XSRF-TOKEN";

    public static IServiceCollection AddApiAntiforgery(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = "pantry.antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        return services;
    }

    /// <summary>Validates unsafe <c>/api</c> requests and issues a fresh request token on <c>/api</c> GETs.</summary>
    public static IApplicationBuilder UseApiAntiforgery(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        if (HttpMethods.IsGet(context.Request.Method))
        {
            IssueRequestToken(context);
        }
        else if (!await antiforgery.IsRequestValidAsync(context))
        {
            await Results.Problem(
                    title: "Missing or invalid antiforgery token.",
                    statusCode: StatusCodes.Status400BadRequest)
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    });

    /// <summary>Issues a request token bound to <see cref="HttpContext.User"/>. Call again after the user changes (sign-in, sign-out).</summary>
    public static void IssueRequestToken(HttpContext context)
    {
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Response.Cookies.Append(RequestTokenCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }
}
