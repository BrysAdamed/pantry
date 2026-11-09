using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pantry.Api.Tests.Infrastructure;

/// <summary>
/// A minimal WebAuthn platform authenticator in software: answers <c>navigator.credentials.create()</c> options with
/// an ES256 credential and a <c>none</c> attestation, serialized like the browser's <c>PublicKeyCredential.toJSON()</c>.
/// .NET 10 has no virtual authenticator for tests, so this exercises the real attestation verification of
/// <c>SignInManager.PerformPasskeyAttestationAsync</c> end to end.
/// </summary>
public sealed class SoftwareAuthenticator(string origin) : IDisposable
{
    private const byte UserPresent = 0x01;
    private const byte UserVerified = 0x04;
    private const byte AttestedCredentialData = 0x40;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);

    public JsonObject Create(JsonElement creationOptions, string? overrideRelyingPartyId = null)
    {
        var relyingPartyId = overrideRelyingPartyId ?? creationOptions.GetProperty("rp").GetProperty("id").GetString()!;
        var challenge = creationOptions.GetProperty("challenge").GetString()!;

        var clientDataJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "webauthn.create",
            challenge,
            origin,
            crossOrigin = false,
        });

        return new JsonObject
        {
            ["id"] = Base64Url(CredentialId),
            ["rawId"] = Base64Url(CredentialId),
            ["type"] = "public-key",
            ["authenticatorAttachment"] = "platform",
            ["clientExtensionResults"] = new JsonObject(),
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = Base64Url(clientDataJson),
                ["attestationObject"] = Base64Url(AttestationObject(relyingPartyId)),
                ["transports"] = new JsonArray("internal"),
            },
        };
    }

    public void Dispose() => _key.Dispose();

    private byte[] AttestationObject(string relyingPartyId)
    {
        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString("none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(AuthenticatorData(relyingPartyId));
        writer.WriteEndMap();
        return writer.Encode();
    }

    private byte[] AuthenticatorData(string relyingPartyId)
    {
        using var data = new MemoryStream();
        data.Write(SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)));
        data.WriteByte(UserPresent | UserVerified | AttestedCredentialData);
        data.Write(new byte[4]);
        data.Write(new byte[16]);
        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)CredentialId.Length);
        data.Write(length);
        data.Write(CredentialId);
        data.Write(CosePublicKey());
        return data.ToArray();
    }

    private byte[] CosePublicKey()
    {
        var point = _key.ExportParameters(includePrivateParameters: false).Q;
        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        writer.WriteInt32(-7);
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(point.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(point.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
