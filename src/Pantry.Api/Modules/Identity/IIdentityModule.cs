namespace Pantry.Api.Modules.Identity;

/// <summary>In-process contract of the Identity module for other modules.</summary>
public interface IIdentityModule
{
    /// <summary>All users who can sign in, with their role and sign-in methods.</summary>
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default);
}

public sealed record UserAccount(Guid Id, AccountRole Role, IReadOnlyList<SignInMethod> SignInMethods);

public enum AccountRole
{
    Admin,
    User,
}

/// <summary>A way a user signs in (ADR 0018). A user has at least one.</summary>
public enum SignInMethod
{
    Passkey,
    Password,
}
