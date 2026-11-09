using System.Diagnostics.CodeAnalysis;

namespace Pantry.Api.Modules.Household;

/// <summary>
/// The language household content (recipes, list items, meal slot names) is written in, as a BCP 47 code
/// from a short supported list (ADR 0010). Agents write and translate into it. Independent of the UI language.
/// </summary>
public sealed record ContentLanguage
{
    public const int CodeMaxLength = 10;

    public static readonly ContentLanguage Pl = new("pl");
    public static readonly ContentLanguage En = new("en");
    public static readonly ContentLanguage De = new("de");
    public static readonly ContentLanguage Fr = new("fr");
    public static readonly ContentLanguage Es = new("es");
    public static readonly ContentLanguage It = new("it");
    public static readonly ContentLanguage Cs = new("cs");
    public static readonly ContentLanguage Uk = new("uk");

    public static IReadOnlyList<ContentLanguage> Supported { get; } = [Pl, En, De, Fr, Es, It, Cs, Uk];

    /// <summary>Used until the first run sets the household's language.</summary>
    public static ContentLanguage Default => En;

    private ContentLanguage(string code) => Code = code;

    public string Code { get; }

    public static bool TryParse(string? code, [NotNullWhen(true)] out ContentLanguage? language)
    {
        language = Supported.FirstOrDefault(l => string.Equals(l.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
        return language is not null;
    }

    public static ContentLanguage Parse(string code) =>
        TryParse(code, out var language) ? language : throw new FormatException($"'{code}' is not a supported content language.");

    public override string ToString() => Code;
}
