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
        var settings = C2pa.Settings.Default;
        Assert.NotNull(settings.Builder);
        Assert.NotNull(settings.Builder.Thumbnail);
        settings.Builder.Thumbnail.Format = C2pa.ThumbnailFormat.Jpeg;

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

        var signerImpl = new CountingRsaSigner();
        using var signer = Signer.From(signerImpl);

        // Act
        var manifestBytes = builder.Sign(signer, source, dest, "image/jpeg");

        // Assert
        Assert.NotNull(manifestBytes);
        Assert.NotEmpty(manifestBytes);
        Assert.True(signerImpl.CallCount > 0);
    }

    [Fact]
    public void From_AllocatesHandle_ForCallbackLifetime()
    {
        // Arrange
        var signerImpl = new CountingRsaSigner();

        // Act
        using var signer = Signer.From(signerImpl);

        // Assert
        var handleField = typeof(Signer).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handleField);

        var handleValue = (GCHandle)handleField!.GetValue(signer)!;
        Assert.True(handleValue.IsAllocated);
        Assert.Same(signerImpl, handleValue.Target);
    }

    [Fact]
    public void FromIdentity_WithManagedSigners_CreatesCombinedSignerOrThrowsNativeException()
    {
        var c2paSigner = new CountingRsaSigner();
        var identitySigner = new CountingRsaSigner();

        var exception = Record.Exception(() =>
        {
            using var signer = Signer.FromIdentity(
                c2paSigner,
                identitySigner,
                ["c2pa.actions"],
                ["creator"]);

            Assert.True(signer.ReserveSize >= 0);
        });

        Assert.True(exception == null || exception is C2paException);
    }

    [Fact]
    public void FromIdentity_WithManagedSigners_KeepsChildSignerOwned()
    {
        using var c2paSigner = new CountingRsaSigner();
        using var identitySigner = new CountingRsaSigner();

        using var signer = Signer.FromIdentity(
            c2paSigner,
            identitySigner,
            ["c2pa.actions"],
            ["creator"]);

        var identitySignerField = typeof(Signer).GetField("identitySigner", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(identitySignerField);

        var childSigner = Assert.IsType<Signer>(identitySignerField!.GetValue(signer));

        var handleField = typeof(Signer).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handleField);

        var childHandle = (GCHandle)handleField!.GetValue(childSigner)!;
        Assert.True(childHandle.IsAllocated);

        var nestedChildSigner = Assert.IsType<Signer>(identitySignerField.GetValue(childSigner));
        var nestedHandle = (GCHandle)handleField.GetValue(nestedChildSigner)!;
        Assert.True(nestedHandle.IsAllocated);
    }

    private sealed class CountingRsaSigner : ISigner, IDisposable
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