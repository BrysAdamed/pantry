using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Persistence;
using Pantry.Api.Modules.Identity;

namespace Pantry.Api.Modules.Household.Features.Members;

/// <summary>Deletes a member. A member linked to an existing user stays until that user is removed.</summary>
public static class DeleteMember
{
    public static RouteHandlerBuilder MapDeleteMember(this IEndpointRouteBuilder members) => members.MapDelete("/{id:guid}", HandleAsync);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid id, HouseholdDbContext db, IIdentityModule identity, CancellationToken cancellationToken)
    {
        var member = await db.Members.FindAsync([id], cancellationToken);
        if (member is null)
        {
            return TypedResults.NotFound();
        }

        if (member.UserId is { } userId && (await identity.GetUsersAsync(cancellationToken)).Any(u => u.Id == userId))
        {
            return TypedResults.Problem(
                title: "The member has an account.",
                detail: "Remove the user first.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.Members.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
