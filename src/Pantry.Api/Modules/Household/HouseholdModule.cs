using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Household.Features.Members;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household;

public static class HouseholdModule
{
    public static IServiceCollection AddHouseholdModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<HouseholdDbContext>(HouseholdDbContext.Schema);
        services.AddScoped<IHouseholdModule, HouseholdModuleContract>();
        return services;
    }

    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder api)
    {
        var members = api.MapGroup("/household/members");
        members.MapGetMembers();
        members.MapAddMember();
        members.MapReorderMembers();
        members.MapUpdateMember();
        members.MapDeleteMember();
        return api;
    }
}
