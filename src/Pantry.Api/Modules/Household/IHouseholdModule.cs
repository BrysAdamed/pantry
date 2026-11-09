using System.Data.Common;

namespace Pantry.Api.Modules.Household;

/// <summary>
/// In-process contract of the Household module. Other modules (planner, shopping, MCP) read the household
/// configuration through it. Grows with meal slots, dislikes, planning rules and content language.
/// </summary>
public interface IHouseholdModule
{
    /// <summary>Members in household order.</summary>
    Task<IReadOnlyList<HouseholdMember>> GetMembersAsync(CancellationToken cancellationToken = default);

    /// <summary>Sum of the portion factors of all members.</summary>
    Task<decimal> GetHouseholdServingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets up the household for the admin created by the first run, inside the caller's transaction (ADR 0015):
    /// the admin's member with the default portion factor.
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

public sealed record FirstRunAdmin(Guid UserId, string Name);
