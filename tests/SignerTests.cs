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
        settings.Builder.Thumbnail.Format = C2pa.ThumbnailFormat.Jpeg;
        C2pa.LoadSettings(settings.ToJson(indented: false));

        using var builder = Builder.FromJson("{}");

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

    private sealed class CountingRsaSigner : ISigner
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
    }
}