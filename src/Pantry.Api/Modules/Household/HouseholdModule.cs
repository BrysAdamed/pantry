using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Household.Features.Language;
using Pantry.Api.Modules.Household.Features.MealSlots;
using Pantry.Api.Modules.Household.Features.Members;
using Pantry.Api.Modules.Household.Features.PlanningRules;
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

        var mealSlots = api.MapGroup("/household/meal-slots");
        mealSlots.MapGetMealSlots();
        mealSlots.MapAddMealSlot();
        mealSlots.MapReorderMealSlots();
        mealSlots.MapRenameMealSlot();
        mealSlots.MapSetMealSlotEnabled();

        var planningRules = api.MapGroup("/household/planning-rules");
        planningRules.MapGetPlanningRules();
        planningRules.MapAddPlanningRule();
        planningRules.MapReorderPlanningRules();
        planningRules.MapEditPlanningRule();
        planningRules.MapSetPlanningRuleEnabled();
        planningRules.MapDeletePlanningRule();

        var contentLanguage = api.MapGroup("/household/content-language");
        contentLanguage.MapGetContentLanguage();
        contentLanguage.MapSetContentLanguage();
        return api;
    }
}
