using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Tests.Household;

public sealed class DislikeDomainTests
{
    [Fact]
    public void An_avoid_dislike_has_no_reason()
    {
        var dislike = Dislike.Create(Guid.NewGuid(), DislikeKind.Never, "allergy");

        dislike.Change(DislikeKind.Avoid, "allergy");

        Assert.Equal((DislikeKind.Avoid, (string?)null), (dislike.Kind, dislike.Reason));
    }

    [Theory]
    [InlineData(DislikeKind.Avoid, null, DislikeKind.Avoid, null, DislikeKind.Avoid, null)]
    [InlineData(DislikeKind.Avoid, null, DislikeKind.Never, "allergy", DislikeKind.Never, "allergy")]
    [InlineData(DislikeKind.Never, "allergy", DislikeKind.Avoid, null, DislikeKind.Never, "allergy")]
    [InlineData(DislikeKind.Never, null, DislikeKind.Never, "allergy", DislikeKind.Never, "allergy")]
    [InlineData(DislikeKind.Never, "intolerance", DislikeKind.Never, "allergy", DislikeKind.Never, "intolerance")]
    public void Absorbing_another_dislike_keeps_the_stricter_kind(
        DislikeKind kind, string? reason, DislikeKind otherKind, string? otherReason, DislikeKind expectedKind, string? expectedReason)
    {
        var dislike = Dislike.Create(Guid.NewGuid(), kind, reason);

        dislike.Absorb(Dislike.Create(Guid.NewGuid(), otherKind, otherReason));

        Assert.Equal((expectedKind, expectedReason), (dislike.Kind, dislike.Reason));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("allergy", true)]
    [InlineData("", false)]
    public void Reasons_are_optional_but_not_empty(string? reason, bool valid)
    {
        Assert.Equal(valid, Dislike.IsValidReason(reason));
        Assert.False(Dislike.IsValidReason(new string('a', Dislike.ReasonMaxLength + 1)));
    }
}
