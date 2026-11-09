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

    /// <summary>
    /// Issues a request token for the current user on safe <c>/api</c> requests. Runs before authorization,
    /// so a 401 answer still carries a token for the anonymous user the browser now is.
    /// </summary>
    public static IApplicationBuilder UseApiAntiforgeryTokens(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (IsApiRequest(context) && IsSafe(context.Request.Method))
        {
            IssueRequestToken(context);
        }
        await next(context);
    });

    /// <summary>
    /// Rejects unsafe <c>/api</c> requests without a valid token. Runs after authorization, so a request whose
    /// session expired gets 401 (sign in again) rather than 400.
    /// </summary>
    public static IApplicationBuilder UseApiAntiforgeryValidation(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (IsApiRequest(context)
            && !IsSafe(context.Request.Method)
            && !await context.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(context))
        {
            await Results.Problem(
                    title: "Missing or invalid antiforgery token.",
                    statusCode: StatusCodes.Status400BadRequest)
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    });

    /// <summary><c>GET /api/antiforgery</c>: an anonymous, side-effect-free way to get a token for the current user.</summary>
    public static RouteHandlerBuilder MapAntiforgeryTokenEndpoint(this IEndpointRouteBuilder api) =>
        api.MapGet("/antiforgery", () => TypedResults.NoContent()).AllowAnonymous();

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

    private static bool IsApiRequest(HttpContext context) => context.Request.Path.StartsWithSegments("/api");

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
