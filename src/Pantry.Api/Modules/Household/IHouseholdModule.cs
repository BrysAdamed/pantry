using System.Data.Common;

namespace Pantry.Api.Modules.Household;

/// <summary>
/// In-process contract of the Household module. Other modules (planner, shopping, MCP) read the household
/// configuration through it.
/// </summary>
public interface IHouseholdModule
{
    /// <summary>Members in household order.</summary>
    Task<IReadOnlyList<HouseholdMember>> GetMembersAsync(CancellationToken cancellationToken = default);

    /// <summary>Sum of the portion factors of all members.</summary>
    Task<decimal> GetHouseholdServingAsync(CancellationToken cancellationToken = default);

    /// <summary>Enabled meal slots in planner order: the rows of the planner. Disabled slots are left out.</summary>
    Task<IReadOnlyList<HouseholdMealSlot>> GetEnabledMealSlotsAsync(CancellationToken cancellationToken = default);

    /// <summary>Enabled planning rules in household order: what agents follow when planning. Disabled rules are left out.</summary>
    Task<IReadOnlyList<HouseholdPlanningRule>> GetEnabledPlanningRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>The language agents write and translate household content into (ADR 0010).</summary>
    Task<ContentLanguage> GetContentLanguageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets up the household for the admin created by the first run, inside the caller's transaction (ADR 0015):
    /// the admin's member with the default portion factor, the content language from the admin's UI language and
    /// the default meal slots named in it. Safe to call again: what already exists is kept.
    /// </summary>
    Task CompleteFirstRunAsync(FirstRunAdmin admin, DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a member without a user to a new user, inside the caller's transaction (ADR 0015).
    /// Returns false when the member no longer exists or already has a user.
    /// </summary>
    Task<bool> LinkMemberToUserAsync(Guid memberId, Guid userId, DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Unlinks the member of a user who is being removed, inside the caller's transaction (ADR 0015). The member stays.</summary>
    Task UnlinkUserAsync(Guid userId, DbTransaction transaction, CancellationToken cancellationToken = default);
}

public sealed record HouseholdMember(Guid Id, string Name, decimal PortionFactor, int Position, Guid? UserId);

public sealed record HouseholdMealSlot(Guid Id, string Name);

public sealed record HouseholdPlanningRule(Guid Id, string Text);

/// <summary>The admin of the first run. <c>Language</c> becomes the content language and names the default meal slots.</summary>
public sealed record FirstRunAdmin(Guid UserId, string Name, ContentLanguage Language);
