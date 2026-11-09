using System.Net;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Pantry.Api.Infrastructure.Antiforgery;

namespace Pantry.Api.Tests.Infrastructure;

/// <summary>Echoes the <c>XSRF-TOKEN</c> cookie as the antiforgery header on unsafe requests, like Angular's HttpClient.</summary>
public sealed class AntiforgeryHandler(CookieContainerHandler cookies) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var safe = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head || request.Method == HttpMethod.Options;
        var token = cookies.Container.GetCookies(request.RequestUri!)[ApiAntiforgery.RequestTokenCookieName]?.Value;
        if (!safe && token is not null)
        {
            request.Headers.Add(ApiAntiforgery.HeaderName, WebUtility.UrlDecode(token));
        }
        return base.SendAsync(request, cancellationToken);
    }
}
