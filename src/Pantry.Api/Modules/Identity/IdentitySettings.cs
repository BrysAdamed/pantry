using System.ComponentModel.DataAnnotations;

namespace Pantry.Api.Modules.Identity;

/// <summary>Passkey relying party, bound from <c>Identity:Passkeys</c>.</summary>
public sealed class PasskeySettings
{
    public const string Section = "Identity:Passkeys";

    /// <summary>The relying party ID: the hostname passkeys are bound to, for example <c>localhost</c>.</summary>
    [Required]
    public string RelyingPartyId { get; set; } = string.Empty;

    /// <summary>Origins the browser may run the passkey ceremony from, for example <c>https://pantry.example.com</c>.</summary>
    [MinLength(1)]
    public string[] Origins { get; set; } = [];
}

/// <summary>Sign-in session, bound from <c>Identity:Session</c>.</summary>
public sealed class SessionSettings
{
    public const string Section = "Identity:Session";

    /// <summary>How long a session lasts without use. Every request within this time extends it.</summary>
    [Range(typeof(TimeSpan), "00:05:00", "365.00:00:00")]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(30);
}
