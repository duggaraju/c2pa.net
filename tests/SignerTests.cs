// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ContentAuthenticity.Tests;

public sealed class SignerTests
{
    [Fact]
    public void From_WithValidSigner_CanSign_AndInvokesCallback()
    {
        // Arrange
        var settings = new C2paSettings();
        settings.SetValue("build.thumbnail.format", "\"jpeg\"");

        var manifest = """
                        {
                            "assertions": [
                                {
                                    "label": "c2pa.actions",
                                    "data": {
                                        "actions": [
                                            { "action": "c2pa.created" }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;
        using var contextBuilder = new ContextBuilder();
        contextBuilder.SetSettings(settings);
        using var context = contextBuilder.Build();
        using var builder = new Builder(context).WithDefinition(manifest);

        var inputPath = Path.Combine(AppContext.BaseDirectory, "no_manifest.jpg");
        Assert.True(File.Exists(inputPath), $"Missing test fixture: {inputPath}");

        var inputBytes = File.ReadAllBytes(inputPath);
        using var source = new MemoryStream(inputBytes);
        using var dest = new MemoryStream();

        var signer = new CountingRsaSigner();

        // Act
        var manifestBytes = builder.Sign(source, dest, "image/jpeg", signer);

        // Assert
        Assert.NotNull(manifestBytes);
        Assert.NotEmpty(manifestBytes);
        Assert.True(signer.CallCount > 0);
    }

    [Fact]
    public void From_AllocatesHandle_ForCallbackLifetime()
    {
        // Arrange
        var signerImpl = new CountingRsaSigner();

        // Act
        using var signer = new Signer(signerImpl);

        // Assert
        var handles = GetHandles(signer);
        Assert.Single(handles);
        Assert.True(handles[0].IsAllocated);
        Assert.Same(signerImpl, handles[0].Target);
    }

    [Fact]
    public void From_WithSignerInfo_CreatesNativeSignerWithoutCallbackHandle()
    {
        var keyPath = Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pem");
        var certPath = Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pub");

        var signerInfo = new SignerInfo(
            SigningAlg.Ps256,
            File.ReadAllText(certPath),
            File.ReadAllText(keyPath));

        using var signer = new Signer(signerInfo);

        var handles = GetHandles(signer);
        Assert.Empty(handles);
        Assert.True(signer.ReserveSize >= 0);
    }

    [Fact]
    public void FromIdentity_WithManagedSigners_CreatesCombinedSignerOrThrowsNativeException()
    {
        using var c2paSigner = new CountingRsaSigner();
        using var identitySigner = new CountingRsaSigner();
        var options = new SigningOptions(
            C2paSigner: c2paSigner,
            IdentitySigner: identitySigner,
            ReferencedAssertions: ["c2pa.actions"],
            Roles: ["creator"]);

        var exception = Record.Exception(() =>
        {
            using var signer = new Signer(options);

            Assert.True(signer.ReserveSize >= 0);
        });

        Assert.True(exception == null || exception is C2paException);
    }

    [Fact]
    public void FromIdentity_WithManagedSigners_KeepsChildSignerOwned()
    {
        using var c2paSigner = new CountingRsaSigner();
        using var identitySigner = new CountingRsaSigner();
        var options = new SigningOptions(
            C2paSigner: c2paSigner,
            IdentitySigner: identitySigner,
            ReferencedAssertions: ["c2pa.actions"],
            Roles: ["creator"]);

        using var signer = new Signer(options);

        var handles = GetHandles(signer);
        Assert.Equal(2, handles.Count);
        Assert.Contains(handles, h => h.IsAllocated && ReferenceEquals(h.Target, c2paSigner));
        Assert.Contains(handles, h => h.IsAllocated && ReferenceEquals(h.Target, identitySigner));
    }

    private static GCHandleCollection GetHandles(Signer signer)
    {
        var field = typeof(Signer).GetField("handles", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<GCHandleCollection>(field!.GetValue(signer));
    }

    private sealed class CountingRsaSigner : ICallbackSigner, IDisposable
    {
        private readonly RSA _key;

        public CountingRsaSigner()
        {
            var keyPath = Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pem");
            var certPath = Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pub");

            Certs = File.ReadAllText(certPath);

            _key = RSA.Create();
            _key.ImportFromPem(File.ReadAllText(keyPath));
        }

        public int CallCount { get; private set; }

        public SigningAlg Alg { get; } = SigningAlg.Ps256;

        public string Certs { get; }

        public Uri? TimeAuthorityUrl => null;

        public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
        {
            CallCount++;
            var sig = _key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            sig.CopyTo(hash);
            return sig.Length;
        }

        public void Dispose()
        {
            _key.Dispose();
        }
    }
}