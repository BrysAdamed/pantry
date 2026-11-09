using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Domain;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Members;

/// <summary>Adds a member without an account at the end of the household order.</summary>
public static class AddMember
{
    public static RouteHandlerBuilder MapAddMember(this IEndpointRouteBuilder members) => members.MapPost("/", HandleAsync);

    private static async Task<Results<Created<MemberResponse>, ValidationProblem>> HandleAsync(
        MemberRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        if (MemberValidation.Validate(request, out var name) is { } problem)
        {
            return problem;
        }

        var lastPosition = await db.Members.MaxAsync(m => (int?)m.Position, cancellationToken) ?? -1;
        var member = new Member { Name = name, PortionFactor = request.PortionFactor, Position = lastPosition + 1 };
        db.Members.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/household/members/{member.Id}", MemberResponse.From(member, []));
    }
}
