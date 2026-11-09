using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Language;

public static class GetContentLanguage
{
    public static RouteHandlerBuilder MapGetContentLanguage(this IEndpointRouteBuilder language) => language.MapGet("/", HandleAsync);

    private static async Task<ContentLanguageResponse> HandleAsync(HouseholdDbContext db, CancellationToken cancellationToken)
    {
        var household = await db.Households.AsNoTracking().SingleAsync(cancellationToken);
        return ContentLanguageResponse.From(household.ContentLanguage ?? ContentLanguage.Default);
    }
}

public sealed record ContentLanguageResponse(string ContentLanguage)
{
    public static ContentLanguageResponse From(ContentLanguage language) => new(language.Code);
}
