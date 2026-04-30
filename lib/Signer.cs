// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ContentAuthenticity;

public sealed class Signer : IDisposable
{
    private readonly unsafe C2paSigner* signer;
    private readonly GCHandle handle;

    internal unsafe Signer(C2paSigner* c2paSigner, GCHandle handle = default)
    {
        this.signer = c2paSigner;
        this.handle = handle;
    }

    public static unsafe implicit operator C2paSigner*(Signer signer)
    {
        return signer.signer;
    }

    public void Dispose()
    {
        unsafe
        {
            C2paBindings.free(signer);
        }
        if (handle.IsAllocated)
            handle.Free();
    }

    public long ReserveSize
    {
        get
        {
            unsafe
            {
                return (long)C2paBindings.signer_reserve_size(signer);
            }
        }
    }

    public static Signer From(ISigner signer)
    {
        unsafe
        {
            fixed (byte* certs = Encoding.UTF8.GetBytes(signer.Certs))
            fixed (byte* taUrl = signer.TimeAuthorityUrl == null ? null : Encoding.UTF8.GetBytes(signer.TimeAuthorityUrl.OriginalString))
            {
                var handle = GCHandle.Alloc(signer);
                var c2paSigner = C2paBindings.signer_create((void*)(nint)handle, &Sign, signer.Alg, (sbyte*)certs, (sbyte*)taUrl);
                if (c2paSigner == null)
                {
                    handle.Free();
                    C2pa.CheckError();
                }
                return new Signer(c2paSigner, handle);
            }
        }
    }

    /// <summary>
    /// Creates a <see cref="Signer"/> from inline signing material (algorithm,
    /// certificate chain in PEM, private key in PEM and an optional timestamp
    /// authority URL). The private key is sent to the native library which
    /// performs signing internally.
    /// </summary>
    public static Signer FromInfo(SigningAlg alg, string signCertPem, string privateKeyPem, Uri? timeAuthorityUrl = null)
    {
        unsafe
        {
            var algName = alg.ToString().ToLowerInvariant();
            fixed (byte* algBytes = Encoding.UTF8.GetBytes(algName))
            fixed (byte* certBytes = Encoding.UTF8.GetBytes(signCertPem))
            fixed (byte* keyBytes = Encoding.UTF8.GetBytes(privateKeyPem))
            fixed (byte* taBytes = timeAuthorityUrl == null ? null : Encoding.UTF8.GetBytes(timeAuthorityUrl.OriginalString))
            {
                var info = new C2paSignerInfo
                {
                    alg = (sbyte*)algBytes,
                    sign_cert = (sbyte*)certBytes,
                    private_key = (sbyte*)keyBytes,
                    ta_url = (sbyte*)taBytes,
                };
                var c2paSigner = C2paBindings.signer_from_info(&info);
                if (c2paSigner == null)
                    C2pa.CheckError();
                return new Signer(c2paSigner);
            }
        }
    }

    /// <summary>
    /// Signs <paramref name="data"/> with the Ed25519 algorithm using the
    /// supplied PEM-encoded private key. Returns the raw signature bytes.
    /// </summary>
    public static byte[] Ed25519Sign(ReadOnlySpan<byte> data, string privateKeyPem)
    {
        // Ed25519 signatures are always 64 bytes.
        const int Ed25519SignatureSize = 64;
        unsafe
        {
            fixed (byte* dataPtr = data)
            fixed (byte* keyPtr = Encoding.UTF8.GetBytes(privateKeyPem))
            {
                var sigPtr = C2paBindings.ed25519_sign(dataPtr, (nuint)data.Length, (sbyte*)keyPtr);
                if (sigPtr == null)
                    C2pa.CheckError();
                var bytes = new byte[Ed25519SignatureSize];
                Marshal.Copy((nint)sigPtr, bytes, 0, bytes.Length);
                C2paBindings.free(sigPtr);
                return bytes;
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe nint Sign(void* context, byte* data, nuint len, byte* signature, nuint sig_max_size)
    {
        GCHandle handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is ISigner signer)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            return signer.Sign(span, hash);
        }
        return -1;
    }

    public Signer CreateRSASigner(X509Certificate2Collection certifciates, RSA algorithm, Uri? TimeAuthorityUrl = null)
    {
        StringBuilder builder = new();
        foreach (var cert in certifciates)
        {
            // Console.WriteLine("Subject = {0} Issuer = {1} Expiry = {2}", cert.Subject, cert.Issuer, cert.GetExpirationDateString());
            builder.AppendLine(cert.ExportCertificatePem());
        }

        var certs = builder.ToString();
        var alg = algorithm.GetAlgorithm();
        var handle = GCHandle.Alloc(algorithm);
        try
        {
            unsafe
            {
                fixed (byte* taUrlBytes = TimeAuthorityUrl == null ? null : Encoding.UTF8.GetBytes(TimeAuthorityUrl.OriginalString))
                fixed (byte* certsBytes = Encoding.UTF8.GetBytes(certs))
                {
                    var c2paSigner = C2paBindings.signer_create((void*)GCHandle.ToIntPtr(handle), &RSASign, alg, (sbyte*)certsBytes, (sbyte*)taUrlBytes);
                    if (c2paSigner == null)
                        C2pa.CheckError();
                    return new Signer(c2paSigner);
                }
            }
        }
        finally
        {
            handle.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe nint RSASign(void* context, byte* data, nuint len, byte* signature, nuint sig_max_size)
    {
        GCHandle handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is RSA rsa)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            if (rsa.TrySignData(span, hash, rsa.GetHash(), RSASignaturePadding.Pss, out _))
            {
                return hash.Length;
            }
        }
        return -1;
    }

}