using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Domain;
using Pantry.Api.Modules.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Household.Features.Members;

/// <summary>A member as the settings screen shows it. <c>Account</c> is the linked user's role, or null without an account.</summary>
public sealed record MemberResponse(Guid Id, string Name, decimal PortionFactor, int Position, UserRole? Account)
{
    public static MemberResponse From(Member member, IReadOnlyList<UserAccount> users) => new(
        member.Id,
        member.Name,
        member.PortionFactor,
        member.Position,
        users.FirstOrDefault(u => u.Id == member.UserId)?.Role);
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
