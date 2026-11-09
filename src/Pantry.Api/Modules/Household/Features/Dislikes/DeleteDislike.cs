using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Dislikes;

public static class DeleteDislike
{
    public static RouteHandlerBuilder MapDeleteDislike(this IEndpointRouteBuilder dislikes) => dislikes.MapDelete("/{id:guid}", HandleAsync);

    private static async Task<Results<NoContent, NotFound>> HandleAsync(Guid id, HouseholdDbContext db, CancellationToken cancellationToken) =>
        await db.Dislikes.Where(d => d.Id == id).ExecuteDeleteAsync(cancellationToken) == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
}
