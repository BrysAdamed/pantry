using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Pantry.Api.Modules.Identity.Domain;

/// <summary>
/// An invite or a recovery link: a single-use link that lets someone register a passkey. Only the SHA-256 hash of
/// the token is stored; the token itself is shown once, to the admin who issued the link.
/// </summary>
public sealed class PasskeyLink
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    private const int TokenBytes = 32;

    private PasskeyLink()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public PasskeyLinkKind Kind { get; private init; }

    public byte[] TokenHash { get; private init; } = [];

    /// <summary>The member an invite is for. Null for a recovery link.</summary>
    public Guid? MemberId { get; private init; }

    /// <summary>The user a recovery link is for. Null for an invite.</summary>
    public Guid? UserId { get; private init; }

    public Guid? CreatedByUserId { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static (PasskeyLink Link, string Token) CreateInvite(Guid memberId, Guid createdByUserId, DateTimeOffset now) =>
        Create(PasskeyLinkKind.Invite, memberId, userId: null, createdByUserId, now);

    public static (PasskeyLink Link, string Token) CreateRecovery(Guid userId, Guid createdByUserId, DateTimeOffset now) =>
        Create(PasskeyLinkKind.Recovery, memberId: null, userId, createdByUserId, now);

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public PasskeyLinkState StateAt(DateTimeOffset now) =>
        UsedAt is not null ? PasskeyLinkState.Used
        : RevokedAt is not null ? PasskeyLinkState.Revoked
        : now >= ExpiresAt ? PasskeyLinkState.Expired
        : PasskeyLinkState.Open;

    /// <summary>Revokes the link unless it was used. Expired links are revoked too, so at most one link per member or user is pending.</summary>
    public void Revoke(DateTimeOffset now)
    {
        if (UsedAt is null && RevokedAt is null)
        {
            RevokedAt = now;
        }
    }

    public void MarkUsed(DateTimeOffset now) => UsedAt = now;

    private static (PasskeyLink, string) Create(PasskeyLinkKind kind, Guid? memberId, Guid? userId, Guid createdByUserId, DateTimeOffset now)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var link = new PasskeyLink
        {
            Kind = kind,
            TokenHash = Hash(token),
            MemberId = memberId,
            UserId = userId,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
        return (link, token);
    }
}

public enum PasskeyLinkKind
{
    Invite,
    Recovery,
}

public enum PasskeyLinkState
{
    Open,
    Expired,
    Used,
    Revoked,
}
