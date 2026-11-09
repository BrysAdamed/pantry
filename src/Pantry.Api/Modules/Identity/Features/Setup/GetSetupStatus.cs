using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Setup;

public static class GetSetupStatus
{
    public static RouteHandlerBuilder MapGetSetupStatus(this IEndpointRouteBuilder setup) => setup.MapGet("", HandleAsync);

    private static async Task<SetupStatusResponse> HandleAsync(IdentityModuleDbContext db, CancellationToken cancellationToken) =>
        new(FirstRunNeeded: !await db.Users.AnyAsync(cancellationToken));
}

public sealed record SetupStatusResponse(bool FirstRunNeeded);
