using Microsoft.Net.Http.Headers;

namespace Pantry.Api.Infrastructure.Http;

public static class ApiCaching
{
    public const string ImmutableCacheControl = "private, max-age=31536000, immutable";

    /// <summary>
    /// Marks every <c>/api</c> response <c>no-store</c>, so household data never outlives a sign-out in a browser cache.
    /// A successful response of an endpoint marked <see cref="CacheImmutably{TBuilder}"/> may instead be kept privately
    /// for a year. Call it before authentication: the cache headers are set last, after a renewed session cookie or a new
    /// antiforgery token asked for <c>no-cache</c>.
    /// </summary>
    public static IApplicationBuilder UseApiCaching(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers[HeaderNames.CacheControl] = "no-store";
            if (context.GetEndpoint()?.Metadata.GetMetadata<ImmutableResponse>() is not null)
            {
                context.Response.OnStarting(() =>
                {
                    if (context.Response.StatusCode == StatusCodes.Status200OK)
                    {
                        context.Response.Headers[HeaderNames.CacheControl] = ImmutableCacheControl;
                        context.Response.Headers.Remove(HeaderNames.Pragma);
                        context.Response.Headers.Remove(HeaderNames.Expires);
                    }

                    return Task.CompletedTask;
                });
            }
        }

        return next(context);
    });

    /// <summary>
    /// Marks an endpoint whose URL always answers the same bytes (for example one with a content id in it), so the
    /// browser may keep its response for a year without asking again. The user still needs a session to fetch it.
    /// </summary>
    public static TBuilder CacheImmutably<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new ImmutableResponse());

    private sealed class ImmutableResponse;
}
