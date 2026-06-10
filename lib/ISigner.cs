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
/// Inline signer material used to create a native signer without a managed
/// callback.
/// </summary>
public readonly record struct SignerInfo(
    SigningAlg Alg,
    string Certs,
    string PrivateKey,
    Uri? TimeAuthorityUrl = null) : ISigner;

public readonly record struct SigningOptions(
    ISigner C2paSigner,
    ISigner? IdentitySigner = null,
    IReadOnlyList<string>? ReferencedAssertions = null,
    IReadOnlyList<string>? Roles = null);

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