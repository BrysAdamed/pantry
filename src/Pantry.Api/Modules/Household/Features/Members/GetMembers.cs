using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;
using Pantry.Api.Modules.Identity;

namespace Pantry.Api.Modules.Household.Features.Members;

public static class GetMembers
{
    public static RouteHandlerBuilder MapGetMembers(this IEndpointRouteBuilder members) => members.MapGet("/", HandleAsync);

    private static async Task<MembersResponse> HandleAsync(HouseholdDbContext db, IIdentityModule identity, CancellationToken cancellationToken)
    {
        var members = await db.Members.AsNoTracking().OrderBy(m => m.Position).ToListAsync(cancellationToken);
        var users = await identity.GetUsersAsync(cancellationToken);
        return new MembersResponse(
            members.Select(m => MemberResponse.From(m, users)).ToList(),
            members.Sum(m => m.PortionFactor));
    }
}

public sealed record MembersResponse(IReadOnlyList<MemberResponse> Members, decimal HouseholdServing);
