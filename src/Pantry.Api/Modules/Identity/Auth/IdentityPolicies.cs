using Microsoft.AspNetCore.Authorization;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Auth;

public static class IdentityPolicies
{
    /// <summary>User administration: invites, recovery links, removing users, changing roles. Others get 403.</summary>
    public const string Admin = "admin";

    public static void Add(AuthorizationOptions options) =>
        options.AddPolicy(Admin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));
}
