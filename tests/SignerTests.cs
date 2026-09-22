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
        using var settings = new C2paSettings();
        settings.SetValue("builder.thumbnail.format", "\"jpeg\"");

        var manifest = """
                        {
                            "assertions": [
                                {
                                    "label": "c2pa.actions",
                                    "data": {
                                        "actions": [
                                            {
                                                "action": "c2pa.created",
                                                "digitalSourceType": "http://cv.iptc.org/newscodes/digitalsourcetype/digitalCapture"
                                            }
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

        using var signer = new CountingRsaSigner();

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
        var options = new SigningOptions
        {
            C2paSigner = c2paSigner,
            IdentitySigner = identitySigner,
            ReferencedAssertions = ["c2pa.actions"],
            Roles = ["creator"]
        };

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
        var options = new SigningOptions
        {
            C2paSigner = c2paSigner,
            IdentitySigner = identitySigner,
            ReferencedAssertions = ["c2pa.actions"],
            Roles = ["creator"]
        };

        using var signer = new Signer(options);

        var handles = GetHandles(signer);
        Assert.Equal(2, handles.Count);
        Assert.Contains(handles, h => h.IsAllocated && ReferenceEquals(h.Target, c2paSigner));
        Assert.Contains(handles, h => h.IsAllocated && ReferenceEquals(h.Target, identitySigner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CredentialHolder_KeepsCallbacksOwned_AndReleasesThemOnDispose(bool withIdentitySigner)
    {
        using var c2paSigner = new CountingRsaSigner();
        using var identitySigner = new CountingRsaSigner();
        var holder = new TestCredentialHolder();
        var options = new SigningOptions
        {
            C2paSigner = c2paSigner,
            IdentitySigner = withIdentitySigner ? identitySigner : null,
            ReferencedAssertions = ["c2pa.actions"],
            Roles = ["creator"],
            CredentialHolder = holder
        };
        var signer = new Signer(options);
        var handles = GetHandles(signer);
        try
        {
            Assert.True(signer.ReserveSize >= holder.ReserveSize);
            Assert.Equal(withIdentitySigner ? 3 : 2, handles.Count);
            Assert.Contains(handles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, holder));
            Assert.Contains(handles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, c2paSigner));
            if (withIdentitySigner)
                Assert.Contains(handles, handle => handle.IsAllocated && ReferenceEquals(handle.Target, identitySigner));
        }
        finally
        {
            signer.Dispose();
        }
        Assert.Empty(handles);
        signer.Dispose();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid\0type")]
    public void CredentialHolder_WithInvalidSignatureType_ShouldThrow(string signatureType)
    {
        using var c2paSigner = new CountingRsaSigner();
        var options = new SigningOptions
        {
            C2paSigner = c2paSigner,
            CredentialHolder = new TestCredentialHolder { SignatureType = signatureType }
        };

        Assert.Throws<ArgumentException>(() => new Signer(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CredentialHolder_WithInvalidReserveSize_ShouldThrow(int reserveSize)
    {
        using var c2paSigner = new CountingRsaSigner();
        var options = new SigningOptions
        {
            C2paSigner = c2paSigner,
            CredentialHolder = new TestCredentialHolder { ReserveSize = reserveSize }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new Signer(options));
    }

    [Fact]
    public void CredentialHolder_WithContext_ShouldSignAndEmbedIdentityAssertion()
    {
        using var c2paSigner = new CountingRsaSigner();
        var holder = new TestCredentialHolder();
        var options = new SigningOptions
        {
            C2paSigner = c2paSigner,
            ReferencedAssertions = ["c2pa.actions"],
            Roles = ["creator"],
            CredentialHolder = holder
        };
        Context context;
        using (var contextBuilder = new ContextBuilder())
        {
            contextBuilder.SetSigner(options);
            context = contextBuilder.Build();
        }
        using var ownedContext = context;
        using var builder = new Builder(context).WithDefinition(new ManifestDefinition
        {
            Title = "Credential holder JPEG",
            ClaimGeneratorInfo = [new ClaimGeneratorInfo { Name = "c2pa.net tests" }],
        });
        builder.SetIntent(C2paBuilderIntent.Create, C2paDigitalSourceType.DigitalCapture);
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "no_manifest.jpg"));
        using var destination = new MemoryStream();

        builder.Sign(source, destination, "image/jpeg");

        Assert.True(holder.CallCount > 0);
        Assert.NotEmpty(holder.Payload);
        Assert.Equal(holder.ReserveSize, holder.BufferLength);
        Assert.True(c2paSigner.CallCount > 0);
        destination.Position = 0;
        using var reader = new Reader(context).WithStream(destination, "image/jpeg");
        Assert.Contains("cawg.identity", reader.Json);
        Assert.Contains(holder.SignatureType, reader.Json);
        Assert.Contains("creator", reader.Json);
        Assert.Contains("claimSignature.validated", reader.Json);
        Assert.Contains("dataHash.match", reader.Json);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(2049, false)]
    [InlineData(1, true)]
    public void CredentialHolder_WhenCallbackFails_ShouldThrowNativeException(int result, bool throws)
    {
        using var c2paSigner = new CountingRsaSigner();
        var holder = new TestCredentialHolder { Result = result, Throws = throws };
        using var contextBuilder = new ContextBuilder();
        contextBuilder.SetSigner(new SigningOptions { C2paSigner = c2paSigner, CredentialHolder = holder });
        using var context = contextBuilder.Build();
        using var builder = new Builder(context).WithDefinition(new ManifestDefinition
        {
            ClaimGeneratorInfo = [new ClaimGeneratorInfo { Name = "c2pa.net tests" }],
        });
        builder.SetIntent(C2paBuilderIntent.Create, C2paDigitalSourceType.DigitalCapture);
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "no_manifest.jpg"));
        using var destination = new MemoryStream();

        Assert.Throws<C2paException>(() => builder.Sign(source, destination, "image/jpeg"));
        Assert.True(holder.CallCount > 0);
    }

    private sealed class TestCredentialHolder : ICredentialHolder
    {
        public string SignatureType { get; init; } = "cawg.test_credential";

        public int ReserveSize { get; init; } = 2048;

        public int Result { get; init; } = 1;

        public bool Throws { get; init; }

        public int CallCount { get; private set; }

        public byte[] Payload { get; private set; } = [];

        public int BufferLength { get; private set; }

        public int Sign(ReadOnlySpan<byte> signerPayload, Span<byte> signature)
        {
            CallCount++;
            Payload = signerPayload.ToArray();
            BufferLength = signature.Length;
            if (Throws)
                throw new InvalidOperationException("Credential service failed.");
            signature[0] = 0x01;
            return Result;
        }
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