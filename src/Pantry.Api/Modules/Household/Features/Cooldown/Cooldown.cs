using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Cooldown;

/// <summary>
/// The household cooldown: days before a recipe should be planned again, for every recipe without its own cooldown.
/// 0 turns it off. A soft hint: nothing is blocked.
/// </summary>
public static class Cooldown
{
    public static IEndpointRouteBuilder MapCooldown(this IEndpointRouteBuilder cooldown)
    {
        cooldown.MapGet("/", GetAsync);
        cooldown.MapPut("/", SetAsync);
        return cooldown;
    }

    private static async Task<CooldownSetting> GetAsync(HouseholdDbContext db, CancellationToken cancellationToken)
    {
        var household = await db.Households.AsNoTracking().SingleAsync(cancellationToken);
        return new CooldownSetting(household.CooldownDays);
    }

    private static async Task<Results<Ok<CooldownSetting>, ValidationProblem>> SetAsync(
        CooldownRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        if (request.CooldownDays is not { } days || !Domain.Household.IsValidCooldown(days))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cooldownDays"] = [$"Cooldown is 0 to {Domain.Household.MaxCooldownDays} days."],
            });
        }

        var household = await db.Households.SingleAsync(cancellationToken);
        household.CooldownDays = days;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new CooldownSetting(days));
    }
}

public sealed record CooldownSetting(int CooldownDays);

public sealed record CooldownRequest(int? CooldownDays);
