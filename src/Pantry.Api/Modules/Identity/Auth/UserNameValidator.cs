using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Auth;

/// <summary>
/// Enforces <see cref="UserNames"/> on every user Identity saves, in place of Identity's ASCII-only character list.
/// The internal ID that users without a username have is allowed.
/// </summary>
internal sealed class UserNameValidator : IUserValidator<User>
{
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user) =>
        Task.FromResult(user.ChosenUserName is { } userName && !UserNames.IsValid(userName)
            ? IdentityResult.Failed(new IdentityError { Code = "InvalidUserName", Description = UserNames.Rule })
            : IdentityResult.Success);
}
