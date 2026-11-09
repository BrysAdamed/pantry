namespace Pantry.Api.Modules.Household.Domain;

public sealed class Member
{
    public const int NameMaxLength = 50;
    public const decimal MinPortionFactor = 0.05m;
    public const decimal MaxPortionFactor = 2.00m;
    public const decimal PortionFactorStep = 0.05m;
    public const decimal DefaultPortionFactor = 1.00m;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public decimal PortionFactor { get; set; } = DefaultPortionFactor;

    public int Position { get; set; }

    public Guid? UserId { get; init; }

    public static bool IsValidName(string name) => name.Length is > 0 and <= NameMaxLength;

    public static bool IsValidPortionFactor(decimal factor) =>
        factor is >= MinPortionFactor and <= MaxPortionFactor && factor % PortionFactorStep == 0;
}
