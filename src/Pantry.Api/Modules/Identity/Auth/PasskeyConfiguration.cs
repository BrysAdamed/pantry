using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Pantry.Api.Modules.Identity.Auth;

internal static class PasskeyConfiguration
{
    public static void Apply(IdentityPasskeyOptions options, IOptions<PasskeySettings> settings)
    {
        var origins = settings.Value.Origins.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        options.ServerDomain = settings.Value.RelyingPartyId;
        options.ResidentKeyRequirement = "required";
        options.UserVerificationRequirement = "required";
        options.ValidateOrigin = context => ValueTask.FromResult(!context.CrossOrigin && origins.Contains(Normalize(context.Origin)));
    }

    private static string Normalize(string origin) => origin.Trim().TrimEnd('/');
}
