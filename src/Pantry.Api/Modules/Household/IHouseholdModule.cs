using System.Data.Common;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Household;

/// <summary>
/// In-process contract of the Household module. Other modules (planner, shopping, MCP) read the household
/// configuration through it. Grows with dislikes, planning rules and content language.
/// </summary>
public interface IHouseholdModule
{
    /// <summary>Members in household order.</summary>
    Task<IReadOnlyList<HouseholdMember>> GetMembersAsync(CancellationToken cancellationToken = default);

    /// <summary>Sum of the portion factors of all members.</summary>
    Task<decimal> GetHouseholdServingAsync(CancellationToken cancellationToken = default);

    /// <summary>Enabled meal slots in planner order: the rows of the planner. Disabled slots are left out.</summary>
    Task<IReadOnlyList<HouseholdMealSlot>> GetEnabledMealSlotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets up the household for the admin created by the first run, inside the caller's transaction (ADR 0015):
    /// the admin's member with the default portion factor and the default meal slots named in the admin's UI language.
    /// Safe to call again: what already exists is kept.
    /// </summary>
    Task CompleteFirstRunAsync(FirstRunAdmin admin, DbTransaction transaction, CancellationToken cancellationToken = default);
}

public sealed record HouseholdMember(Guid Id, string Name, decimal PortionFactor, int Position, Guid? UserId);

public sealed record HouseholdMealSlot(Guid Id, string Name);

public sealed record FirstRunAdmin(Guid UserId, string Name, UiLanguage UiLanguage);
