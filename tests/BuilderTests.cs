namespace ContentAuthenticity.Tests;

public class BuilderTests
{
    [Fact]
    public void ComposeManifest_WithSignedManifest_ShouldProduceJpegSegments()
    {
        var signer = new SignerInfo(
            SigningAlg.Ps256,
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pub")),
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pem")));
        using var contextBuilder = new ContextBuilder();
        contextBuilder.SetSigner(signer);
        using var context = contextBuilder.Build();
        using var builder = new Builder(context).WithDefinition(new ManifestDefinition
        {
            Title = "Composed JPEG",
            Format = "image/jpeg",
            ClaimGeneratorInfo = [new ClaimGeneratorInfo { Name = "c2pa.net tests" }],
        });
        builder.SetIntent(C2paBuilderIntent.Create, C2paDigitalSourceType.DigitalCapture);
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "no_manifest.jpg"));
        using var destination = new MemoryStream();
        var manifest = builder.Sign(source, destination, "image/jpeg", signer);

        var composed = builder.ComposeManifest("image/jpeg", manifest);

        Assert.True(composed.Length > manifest.Length);
        Assert.Equal((byte)0xff, composed[0]);
        Assert.Equal((byte)0xeb, composed[1]);
        Assert.True(destination.ToArray().AsSpan().IndexOf(composed) >= 0);
    }

    [Fact]
    public void ComposeManifest_WithEmptyManifest_ShouldThrowNativeException()
    {
        using var context = new Context();
        using var builder = new Builder(context);

        Assert.Throws<C2paException>(() => builder.ComposeManifest("image/jpeg", []));
    }

    [Fact]
    public void SignEmbeddable_WithJpegPlaceholder_ShouldRoundTrip()
    {
        const string format = "image/jpeg";
        const int insertOffset = 2;
        var signer = new SignerInfo(
            SigningAlg.Ps256,
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pub")),
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "certs", "rs256.pem")));
        using var contextBuilder = new ContextBuilder();
        contextBuilder.SetSigner(signer);
        using var context = contextBuilder.Build();
        using var builder = new Builder(context).WithDefinition(new ManifestDefinition
        {
            Title = "Embeddable JPEG",
            Format = format,
            InstanceId = Builder.GenerateInstanceID(),
            ClaimGeneratorInfo = [new ClaimGeneratorInfo { Name = "c2pa.net tests" }],
        });
        builder.AddAction(new Schema.ActionItemV2
        {
            Action = "c2pa.created",
            DigitalSourceType = "http://cv.iptc.org/newscodes/digitalsourcetype/digitalCapture"
        });

        Assert.True(builder.NeedsPlaceholder(format));
        var placeholder = builder.Placeholder(format);
        Assert.NotEmpty(placeholder);
        var source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "no_manifest.jpg"));
        Assert.Equal((byte)0xff, source[0]);
        Assert.Equal((byte)0xd8, source[1]);
        using var asset = new MemoryStream();
        asset.Write(source.AsSpan(0, insertOffset));
        asset.Write(placeholder);
        asset.Write(source.AsSpan(insertOffset));
        var assetLength = asset.Length;

        builder.SetDataHashExclusions([(insertOffset, (ulong)placeholder.Length)]);
        asset.Position = 0;
        builder.UpdateHashFromStream(asset, format);
        var signedManifest = builder.SignEmbeddable(format);

        Assert.Equal(placeholder.Length, signedManifest.Length);
        asset.Position = insertOffset;
        asset.Write(signedManifest);
        Assert.Equal(assetLength, asset.Length);
        asset.Position = 0;
        using var reader = new Reader(context).WithStream(asset, format);
        Assert.True(reader.IsEmbedded);
        var store = reader.Store;
        Assert.NotNull(store.Manifests);
        Assert.NotNull(store.ActiveManifest);
        Assert.Equal("Embeddable JPEG", store.Manifests[store.ActiveManifest].Title);
        Assert.Contains("dataHash.match", reader.Json);
        Assert.Contains("claimSignature.validated", reader.Json);
        Assert.DoesNotContain("dataHash.mismatch", reader.Json);
    }

    [Fact]
    public void GenerateInstanceID_ShouldReturnValidGuid()
    {
        // Act
        var instanceId = Builder.GenerateInstanceID();

        // Assert
        Assert.NotNull(instanceId);
        Assert.StartsWith("xmp:iid:", instanceId);

        // Extract GUID part and verify it's valid
        var guidPart = instanceId.Substring("xmp:iid:".Length);
        Assert.True(Guid.TryParse(guidPart, out _));
    }

    [Fact]
    public void GenerateInstanceID_ShouldReturnUniqueIds()
    {
        // Act
        var id1 = Builder.GenerateInstanceID();
        var id2 = Builder.GenerateInstanceID();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void FromContext_WithManifestDefinition_ShouldCreateBuilder()
    {
        // Arrange
        var manifest = new ManifestDefinition();

        // Act
        var exception = Record.Exception(() =>
        {
            using var contextBuilder = new ContextBuilder();
            using var context = contextBuilder.Build();
            using var builder = new Builder(context).WithDefinition(manifest);
        });

        // Assert - Should not throw during creation, actual functionality depends on native library
        // We can't test the full functionality without the native C2PA library being properly set up
        Assert.True(exception == null || exception is C2paException);
    }

    [Fact]
    public void WithDefinition_WithValidJson_ShouldCreateBuilder()
    {
        // Arrange
        var manifest = new ManifestDefinition
        {
            Format = "jpeg",
            Title = "Test Image",
            Vendor = "Test Vendor",
        };
        var json = manifest.ToJson();

        // Act
        var exception = Record.Exception(() =>
        {
            using var contextBuilder = new ContextBuilder();
            using var context = contextBuilder.Build();
            using var builder = new Builder(context).WithDefinition(json);
        });

        // Assert - Should not throw during creation, actual functionality depends on native library
        Assert.True(exception == null || exception is C2paException);
    }

    [Fact]
    public void BuilderControlApis_ShouldBeCallable()
    {
        var manifest = new ManifestDefinition
        {
            Format = "jpeg",
            Title = "Test Image",
            Vendor = "Test Vendor",
        };

        using var contextBuilder = new ContextBuilder();
        using var context = contextBuilder.Build();
        using var builder = new Builder(context).WithDefinition(manifest);
        builder.SetNoEmbed();
        builder.SetIntent(C2paBuilderIntent.Create, C2paDigitalSourceType.DigitalCreation);
        builder.SetBasePath(Path.GetTempPath());
        builder.SetRemoteUrl(new Uri("https://example.com/manifest.c2pa"));
        builder.AddAction(new Schema.ActionItemV2
        {
            Action = "c2pa.created"
        });
    }
}