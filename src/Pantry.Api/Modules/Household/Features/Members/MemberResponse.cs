using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Domain;
using Pantry.Api.Modules.Identity;

namespace Pantry.Api.Modules.Household.Features.Members;

/// <summary>
/// A member as the settings screen shows it. <c>Account</c> is the linked user's role and <c>UserId</c> that user,
/// both null without an account.
/// </summary>
public sealed record MemberResponse(Guid Id, string Name, decimal PortionFactor, int Position, MemberAccount? Account, Guid? UserId)
{
    public static MemberResponse From(Member member, IReadOnlyList<UserAccount> users)
    {
        var user = users.FirstOrDefault(u => u.Id == member.UserId);
        return new(member.Id, member.Name, member.PortionFactor, member.Position, ToMemberAccount(user?.Role), user?.Id);
    }

    private static MemberAccount? ToMemberAccount(AccountRole? role) => role switch
    {
        AccountRole.Admin => MemberAccount.Admin,
        AccountRole.User => MemberAccount.User,
        null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}

public enum MemberAccount
{
    Admin,
    User,
}

public sealed record MemberRequest(string? Name, decimal PortionFactor);

internal static class MemberValidation
{
    public static ValidationProblem? Validate(MemberRequest request, out string name)
    {
        name = request.Name?.Trim() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (!Member.IsValidName(name))
        {
            errors[nameof(MemberRequest.Name)] = [$"Name must have 1 to {Member.NameMaxLength} characters."];
        }

        if (!Member.IsValidPortionFactor(request.PortionFactor))
        {
            errors[nameof(MemberRequest.PortionFactor)] =
            [
                string.Create(CultureInfo.InvariantCulture,
                    $"Portion factor must be between {Member.MinPortionFactor} and {Member.MaxPortionFactor} in steps of {Member.PortionFactorStep}."),
            ];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }
}
