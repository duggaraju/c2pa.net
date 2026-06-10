// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ContentAuthenticity;

internal sealed class Signer : IDisposable
{
    private unsafe C2paSigner* signer;
    private GCHandleCollection handles = new();

    public Signer(ISigner signer)
        : this(new SigningOptions(signer))
    {
    }

    public Signer(SigningOptions options)
    {
        if (options.IdentitySigner == null)
        {
            unsafe
            {
                signer = CreateNativeSigner(options.C2paSigner, out var handle);
                if (handle.IsAllocated)
                    handles.Add(handle);
            }
            return;
        }

        unsafe
        {
            signer = CreateIdentitySigner(options, out var combinedHandles);
            handles.Transfer(combinedHandles);
        }
    }

    public static unsafe implicit operator C2paSigner*(Signer signer)
    {
        return signer.signer;
    }

    public void Dispose()
    {
        unsafe
        {
            if (signer != null)
            {
                C2paBindings.free(signer);
                signer = null;
            }
        }
        handles.Dispose();
        handles = new GCHandleCollection();
    }

    internal unsafe GCHandleCollection DetachHandles()
    {
        var detachedHandles = handles;
        handles = new GCHandleCollection();

        signer = null;
        return detachedHandles;
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

    /// <summary>
    /// Creates a combined signer that signs the C2PA claim with
    /// <paramref name="c2paSigner"/> and emits a CAWG X.509 identity
    /// assertion signed by <paramref name="identitySigner"/>.
    /// </summary>
    private static unsafe C2paSigner* CreateIdentitySigner(
        SigningOptions options,
        out GCHandleCollection combinedHandles)
    {
        ArgumentNullException.ThrowIfNull(options.IdentitySigner, nameof(options.IdentitySigner));

        combinedHandles = new GCHandleCollection();

        var c2paSigner = CreateNativeSigner(options.C2paSigner, out var c2paHandle);
        if (c2paHandle.IsAllocated)
            combinedHandles.Add(c2paHandle);

        var identitySigner = CreateNativeSigner(options.IdentitySigner!, out var identityHandle);
        if (identityHandle.IsAllocated)
            combinedHandles.Add(identityHandle);

        try
        {
            return CreateIdentitySigner(
                c2paSigner,
                identitySigner,
                options.ReferencedAssertions,
                options.Roles);
        }
        catch
        {
            combinedHandles.Dispose();
            combinedHandles = new GCHandleCollection();
            throw;
        }
    }

    private static unsafe C2paSigner* CreateNativeSigner(ISigner signer, out GCHandle handle)
    {
        handle = default;
        return signer switch
        {
            ICallbackSigner callbackSigner => CreateSigner(callbackSigner, out handle),
            SignerInfo signerInfo => CreateSignerFromInfo(signerInfo),
            _ => throw new ArgumentException($"Unsupported signer type: {signer.GetType().FullName}", nameof(signer)),
        };
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
        if (handle.Target is ICallbackSigner signer)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            return signer.Sign(span, hash);
        }
        return -1;
    }

    public Signer(X509Certificate2Collection certificates, RSA algorithm, Uri? timeAuthorityUrl = null)
    {
        StringBuilder builder = new();
        foreach (var cert in certificates)
        {
            // Console.WriteLine("Subject = {0} Issuer = {1} Expiry = {2}", cert.Subject, cert.Issuer, cert.GetExpirationDateString());
            builder.AppendLine(cert.ExportCertificatePem());
        }

        var certs = builder.ToString();
        var alg = algorithm.GetAlgorithm();
        var handle = GCHandle.Alloc(algorithm);
        var created = false;
        try
        {
            unsafe
            {
                fixed (byte* taUrlBytes = timeAuthorityUrl == null ? null : Encoding.UTF8.GetBytes(timeAuthorityUrl.OriginalString))
                fixed (byte* certsBytes = Encoding.UTF8.GetBytes(certs))
                {
                    var c2paSigner = C2paBindings.signer_create((void*)GCHandle.ToIntPtr(handle), &RSASign, alg, (sbyte*)certsBytes, (sbyte*)taUrlBytes);
                    if (c2paSigner == null)
                        C2pa.CheckError();
                    created = true;
                    signer = c2paSigner;
                    handles = new GCHandleCollection { handle };
                }
            }
        }
        finally
        {
            if (!created)
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

    private unsafe C2paSigner* Detach()
    {
        var detached = signer;
        signer = null;
        return detached;
    }

    private static nint CreateNullTerminatedUtf8Array(IReadOnlyList<string>? values, out nint[] stringPointers)
    {
        if (values == null || values.Count == 0)
        {
            stringPointers = [];
            return 0;
        }

        stringPointers = new nint[values.Count];
        var arrayPointer = Marshal.AllocHGlobal((values.Count + 1) * IntPtr.Size);

        for (int i = 0; i < values.Count; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(values[i]);
            var stringPointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, stringPointer, bytes.Length);
            Marshal.WriteByte(stringPointer, bytes.Length, 0);
            stringPointers[i] = stringPointer;
            Marshal.WriteIntPtr(arrayPointer, i * IntPtr.Size, stringPointer);
        }

        Marshal.WriteIntPtr(arrayPointer, values.Count * IntPtr.Size, IntPtr.Zero);
        return arrayPointer;
    }

    private static void FreeUtf8Array(nint arrayPointer, IReadOnlyList<nint> stringPointers)
    {
        foreach (var stringPointer in stringPointers)
        {
            if (stringPointer != 0)
                Marshal.FreeHGlobal(stringPointer);
        }

        if (arrayPointer != 0)
            Marshal.FreeHGlobal(arrayPointer);
    }

    private static unsafe C2paSigner* CreateSigner(ICallbackSigner signer, out GCHandle handle)
    {
        fixed (byte* certs = Encoding.UTF8.GetBytes(signer.Certs))
        fixed (byte* taUrl = signer.TimeAuthorityUrl == null ? null : Encoding.UTF8.GetBytes(signer.TimeAuthorityUrl.OriginalString))
        {
            handle = GCHandle.Alloc(signer);
            var c2paSigner = C2paBindings.signer_create((void*)(nint)handle, &Sign, signer.Alg, (sbyte*)certs, (sbyte*)taUrl);
            if (c2paSigner == null)
            {
                handle.Free();
                C2pa.CheckError();
            }
            return c2paSigner;
        }
    }

    private static unsafe C2paSigner* CreateSignerFromInfo(SignerInfo signerInfo)
    {
        var algName = signerInfo.Alg.ToString().ToLowerInvariant();
        fixed (byte* algBytes = Encoding.UTF8.GetBytes(algName))
        fixed (byte* certBytes = Encoding.UTF8.GetBytes(signerInfo.Certs))
        fixed (byte* keyBytes = Encoding.UTF8.GetBytes(signerInfo.PrivateKey))
        fixed (byte* taBytes = signerInfo.TimeAuthorityUrl == null ? null : Encoding.UTF8.GetBytes(signerInfo.TimeAuthorityUrl.OriginalString))
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
            return c2paSigner;
        }
    }

    private static unsafe C2paSigner* CreateIdentitySigner(
        C2paSigner* c2paSigner,
        C2paSigner* identitySigner,
        IReadOnlyList<string>? referencedAssertions = null,
        IReadOnlyList<string>? roles = null)
    {
        var referencedAssertionPointers = Array.Empty<nint>();
        var rolePointers = Array.Empty<nint>();
        nint referencedAssertionsArray = 0;
        nint rolesArray = 0;

        try
        {
            referencedAssertionsArray = CreateNullTerminatedUtf8Array(referencedAssertions, out referencedAssertionPointers);
            rolesArray = CreateNullTerminatedUtf8Array(roles, out rolePointers);

            var combinedSigner = C2paBindings.identity_signer_create(
                c2paSigner,
                identitySigner,
                (sbyte**)referencedAssertionsArray,
                (sbyte**)rolesArray);

            if (combinedSigner == null)
                C2pa.CheckError();

            return combinedSigner;
        }
        finally
        {
            FreeUtf8Array(referencedAssertionsArray, referencedAssertionPointers);
            FreeUtf8Array(rolesArray, rolePointers);
        }
    }
}