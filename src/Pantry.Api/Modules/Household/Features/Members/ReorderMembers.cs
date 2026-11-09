using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Members;

/// <summary>Sets the household order. The request lists every member id exactly once.</summary>
public static class ReorderMembers
{
    public static RouteHandlerBuilder MapReorderMembers(this IEndpointRouteBuilder members) => members.MapPut("/order", HandleAsync);

    private static async Task<Results<NoContent, ValidationProblem>> HandleAsync(
        ReorderMembersRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        var members = await db.Members.ToDictionaryAsync(m => m.Id, cancellationToken);
        var ids = request.MemberIds ?? [];
        if (ids.Count != members.Count || ids.Distinct().Count() != ids.Count || !ids.All(members.ContainsKey))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.MemberIds)] = ["List every member exactly once."],
            });
        }

        for (var position = 0; position < ids.Count; position++)
        {
            members[ids[position]].Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

public sealed record ReorderMembersRequest(IReadOnlyList<Guid>? MemberIds);
