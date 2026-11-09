using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Persistence;
using Pantry.Api.Modules.Identity;

namespace Pantry.Api.Modules.Household.Features.Members;

/// <summary>Renames a member or changes their portion factor.</summary>
public static class UpdateMember
{
    public static RouteHandlerBuilder MapUpdateMember(this IEndpointRouteBuilder members) => members.MapPut("/{id:guid}", HandleAsync);

    private static async Task<Results<Ok<MemberResponse>, NotFound, ValidationProblem>> HandleAsync(
        Guid id, MemberRequest request, HouseholdDbContext db, IIdentityModule identity, CancellationToken cancellationToken)
    {
        if (MemberValidation.Validate(request, out var name) is { } problem)
        {
            return problem;
        }

        var member = await db.Members.FindAsync([id], cancellationToken);
        if (member is null)
        {
            return TypedResults.NotFound();
        }

        member.Name = name;
        member.PortionFactor = request.PortionFactor;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MemberResponse.From(member, await identity.GetUsersAsync(cancellationToken)));
    }
}
