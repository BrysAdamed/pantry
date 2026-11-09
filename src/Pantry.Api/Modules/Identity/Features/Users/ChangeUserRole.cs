using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Users;

/// <summary>Promotes a user to admin or demotes an admin. The new role applies to their sessions at the next session check.</summary>
public static class ChangeUserRole
{
    public static RouteHandlerBuilder MapChangeUserRole(this IEndpointRouteBuilder users) => users.MapPut("/{id:guid}/role", HandleAsync);

    private static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid id,
        ChangeUserRoleRequest request,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        CancellationToken cancellationToken)
    {
        if (request.Role is not { } requested)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Role)] = ["Role must be admin or user."],
            });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserAdministrationAsync(cancellationToken);
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var role = IdentityModuleContract.ToUserRole(requested);
        if (role != UserRole.Admin && await UserAdministration.IsLastAdminAsync(db, user, cancellationToken))
        {
            return UserAdministration.LastAdmin();
        }

        user.Role = role;
        UserAdministration.EnsureSucceeded(await userManager.UpdateAsync(user));
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

public sealed record ChangeUserRoleRequest(AccountRole? Role);
