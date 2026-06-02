// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ContentAuthenticity.Tests;

public class ContextBuilderTests
{
    [Fact]
    public void Build_TransfersSavedHandlesToContext()
    {
        using var builder = new ContextBuilder();
        Func<C2paProgressPhase, uint, uint, int> callback = (_, _, _) => 1;
        using var signerImpl = new TestSigner();
        using var signer = Signer.From(signerImpl);
        using var resolver = new HttpResolver(new HttpClient());

        builder.SetSigner(signer);
        builder.SetProgressCallback(callback);
        builder.SetHttpResolver(resolver);

        var builderHandlesBeforeBuild = GetHandles(builder);
        Assert.Equal(3, builderHandlesBeforeBuild.Count);
        Assert.Contains(builderHandlesBeforeBuild, handle => handle.IsAllocated && ReferenceEquals(handle.Target, callback));
        Assert.Contains(builderHandlesBeforeBuild, handle => handle.IsAllocated && ReferenceEquals(handle.Target, signerImpl));
        Assert.Contains(builderHandlesBeforeBuild, handle => handle.IsAllocated && ReferenceEquals(handle.Target, resolver));

        using var context = builder.Build();

        var builderHandlesAfterBuild = GetHandles(builder);
        Assert.Empty(builderHandlesAfterBuild);

        var contextHandles = GetHandles(context);
        Assert.Equal(3, contextHandles.Count);
        Assert.Contains(contextHandles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, callback));
        Assert.Contains(contextHandles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, signerImpl));
        Assert.Contains(contextHandles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, resolver));
    }

    [Fact]
    public void SetProgressCallback_CallbackRemainsAliveAfterBuilderIsDisposed()
    {
        var callbackCount = 0;
        var inputPath = Path.Combine(AppContext.BaseDirectory, "basic-signed.pdf");
        Assert.True(File.Exists(inputPath), $"Missing test fixture: {inputPath}");

        using var builder = new ContextBuilder();
        builder.SetProgressCallback((_, _, _) =>
        {
            Interlocked.Increment(ref callbackCount);
            return 1;
        });

        using var context = builder.Build();
        builder.Dispose();

        var builderHandlesAfterDispose = GetHandles(builder);
        Assert.Empty(builderHandlesAfterDispose);

        var contextHandlesBeforeRead = GetHandles(context);
        Assert.Contains(contextHandlesBeforeRead, handle => handle.IsAllocated);

        var exception = Record.Exception(() =>
        {
            using var reader = new Reader(context).WithFile(inputPath);
            _ = reader.Json;
        });

        Assert.True(exception == null || exception is C2paException);
        Assert.True(Volatile.Read(ref callbackCount) > 0);
    }

    [Fact]
    public void SetSigner_AndResolver_MoveTheirHandlesIntoBuilder()
    {
        using var builder = new ContextBuilder();
        using var signerImpl = new TestSigner();
        using var signer = Signer.From(signerImpl);
        using var resolver = new HttpResolver(new HttpClient());

        builder.SetSigner(signer);
        builder.SetHttpResolver(resolver);

        var builderHandles = GetHandles(builder);
        Assert.Equal(2, builderHandles.Count);
        Assert.Contains(builderHandles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, signerImpl));
        Assert.Contains(builderHandles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, resolver));

        var signerHandle = GetSignerHandle(signer);
        Assert.False(signerHandle.IsAllocated);

        var resolverHandle = GetResolverHandle(resolver);
        Assert.False(resolverHandle.IsAllocated);
    }

    private static GCHandleCollection GetHandles(object instance)
    {
        var field = instance.GetType().GetField("handles", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<GCHandleCollection>(field!.GetValue(instance));
    }

    private static GCHandle GetSignerHandle(Signer signer)
    {
        var field = typeof(Signer).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (GCHandle)field!.GetValue(signer)!;
    }

    private static GCHandle GetResolverHandle(HttpResolver resolver)
    {
        var field = typeof(HttpResolver).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (GCHandle)field!.GetValue(resolver)!;
    }

    private sealed class TestSigner : ISigner, IDisposable
    {
        private readonly RSA key;

        public TestSigner()
        {
            var keyPath = Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pem");
            var certPath = Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pub");

            Certs = File.ReadAllText(certPath);
            key = RSA.Create();
            key.ImportFromPem(File.ReadAllText(keyPath));
        }

        public SigningAlg Alg => SigningAlg.Ps256;

        public string Certs { get; }

        public Uri? TimeAuthorityUrl => null;

        public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
        {
            var signature = key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            signature.CopyTo(hash);
            return signature.Length;
        }

        public void Dispose()
        {
            key.Dispose();
        }
    }
}