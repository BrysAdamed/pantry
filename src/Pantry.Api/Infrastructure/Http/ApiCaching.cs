using Microsoft.Net.Http.Headers;

namespace Pantry.Api.Infrastructure.Http;

public static class ApiCaching
{
    /// <summary>Marks every <c>/api</c> response <c>no-store</c>, so household data never outlives a sign-out in a browser cache.</summary>
    public static IApplicationBuilder UseApiNoStore(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        }
        return next(context);
    });
}
