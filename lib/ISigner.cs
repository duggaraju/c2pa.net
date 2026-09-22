// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Buffers;

namespace ContentAuthenticity;

public interface ISigner
{
    public SigningAlg Alg { get; }

    public string Certs { get; }

    public Uri? TimeAuthorityUrl { get; }
}

public interface ICallbackSigner : ISigner
{
    int Sign(ReadOnlySpan<byte> data, Span<byte> hash);
}

/// <summary>
/// Produces a CAWG identity credential from a CBOR-encoded signer payload.
/// Implementations must remain usable from the signing thread while the context lives.
/// </summary>
public interface ICredentialHolder
{
    /// <summary>The CAWG signature type, such as cawg.identity_claims_aggregation.</summary>
    string SignatureType { get; }

    /// <summary>The positive maximum byte count of the returned credential signature.</summary>
    int ReserveSize { get; }

    /// <summary>
    /// Writes the credential signature and returns its byte count, or a negative
    /// value on failure. The count must not exceed the supplied buffer length.
    /// </summary>
    int Sign(ReadOnlySpan<byte> signerPayload, Span<byte> signature);
}

/// <summary>
/// Inline signer material used to create a native signer without a managed
/// callback.
/// </summary>
public readonly record struct SignerInfo(
    SigningAlg Alg,
    string Certs,
    string PrivateKey,
    Uri? TimeAuthorityUrl = null) : ISigner;

/// <summary>Configures C2PA claim signing and optional CAWG identity assertions.</summary>
public readonly record struct SigningOptions
{
    /// <summary>The signer for the C2PA claim.</summary>
    public required ISigner C2paSigner { get; init; }

    /// <summary>An optional signer for a CAWG X.509 identity assertion.</summary>
    public ISigner? IdentitySigner { get; init; }

    /// <summary>Assertion labels referenced by the identity assertions.</summary>
    public IReadOnlyList<string>? ReferencedAssertions { get; init; }

    /// <summary>Roles attributed to the identity assertion's named actor.</summary>
    public IReadOnlyList<string>? Roles { get; init; }

    /// <summary>
    /// An optional callback-backed identity credential, emitted in addition to
    /// the X.509 identity assertion when IdentitySigner is also supplied.
    /// </summary>
    public ICredentialHolder? CredentialHolder { get; init; }
}

public interface IAsyncSigner : ICallbackSigner
{
    Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> data);

    int ICallbackSigner.Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        var pool = ArrayPool<byte>.Shared;
        var rented = pool.Rent(data.Length);
        try
        {
            data.CopyTo(rented);
            var output = SignAsync(rented.AsMemory(0, data.Length)).GetAwaiter().GetResult();
            output.Span.CopyTo(hash);
            return output.Length;
        }
        finally
        {
            pool.Return(rented);
        }
    }
}