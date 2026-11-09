using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.Passkeys;

/// <summary>Finishes adding a passkey to the signed-in user, for example for a second device without a shared password manager.</summary>
public static class AddPasskey
{
    public static RouteHandlerBuilder MapAddPasskey(this IEndpointRouteBuilder passkeys) => passkeys.MapPost("", HandleAsync);

    private static async Task<Results<Created<PasskeyResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(
        AddPasskeyRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILoggerFactory loggerFactory)
    {
        var name = MyPasskeys.NormalizeName(request.Name);
        if (!MyPasskeys.IsValidName(name))
        {
            return MyPasskeys.InvalidName();
        }

        if (request.Credential.ValueKind != JsonValueKind.Object)
        {
            return AttestationFailed("The passkey credential is missing.");
        }

        PasskeyAttestationResult attestation;
        try
        {
            attestation = await signInManager.PerformPasskeyAttestationAsync(request.Credential.GetRawText());
        }
        catch (InvalidOperationException)
        {
            return AttestationFailed("No passkey creation is in progress. Start again.");
        }

        if (!attestation.Succeeded)
        {
            loggerFactory.CreateLogger(typeof(AddPasskey)).LogWarning("Passkey attestation failed: {Reason}", attestation.Failure.Message);
            return AttestationFailed("The passkey could not be verified.");
        }

        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (attestation.UserEntity.Id != user.Id.ToString())
        {
            return AttestationFailed("The passkey was created for someone else.");
        }

        attestation.Passkey.Name = name;
        var result = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Adding the passkey failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        var id = MyPasskeys.ToId(attestation.Passkey.CredentialId);
        return TypedResults.Created($"/api/me/passkeys/{id}", new PasskeyResponse(id, name, attestation.Passkey.CreatedAt, null));
    }

    private static ProblemHttpResult AttestationFailed(string detail) =>
        TypedResults.Problem(title: "Passkey creation failed.", detail: detail, statusCode: StatusCodes.Status400BadRequest);
}

public sealed record AddPasskeyRequest(JsonElement Credential, string? Name = null);
