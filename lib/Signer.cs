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
            C2paBindings.signer_free(signer);
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

    public static Signer FromSettings()
    {
        unsafe
        {
            var c2paSigner = C2paBindings.signer_from_settings();
            if (c2paSigner == null)
                C2pa.CheckError();
            return new Signer(c2paSigner);
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