using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Language;

/// <summary>Picks the content language from the supported list. Existing content is not translated.</summary>
public static class SetContentLanguage
{
    public static RouteHandlerBuilder MapSetContentLanguage(this IEndpointRouteBuilder language) => language.MapPut("/", HandleAsync);

    private static async Task<Results<Ok<ContentLanguageResponse>, ValidationProblem>> HandleAsync(
        ContentLanguageRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        if (!ContentLanguage.TryParse(request.ContentLanguage, out var language))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ContentLanguage)] = [$"Pick one of: {string.Join(", ", ContentLanguage.Supported)}."],
            });
        }

        var household = await db.Households.SingleAsync(cancellationToken);
        household.ContentLanguage = language;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ContentLanguageResponse.From(language));
    }
}

public sealed record ContentLanguageRequest(string? ContentLanguage);
