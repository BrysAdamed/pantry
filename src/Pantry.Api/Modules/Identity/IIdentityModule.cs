namespace Pantry.Api.Modules.Identity;

/// <summary>In-process contract of the Identity module for other modules.</summary>
public interface IIdentityModule
{
    /// <summary>All users who can sign in, with their role.</summary>
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default);
}

public sealed record UserAccount(Guid Id, AccountRole Role);

public enum AccountRole
{
    Admin,
    User,
}
