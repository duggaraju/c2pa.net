// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity.Tests;

public class ManifestDefinitionTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateValidInstance()
    {
        // Act
        var manifest = new ManifestDefinition();

        // Assert
        Assert.Equal("application/octet-stream", manifest.Format);
        Assert.Equal(SigningAlg.Es256, manifest.Alg);
        Assert.NotNull(manifest.ClaimGeneratorInfo);
        Assert.Empty(manifest.ClaimGeneratorInfo);
        Assert.NotNull(manifest.Ingredients);
        Assert.Empty(manifest.Ingredients);
        Assert.NotNull(manifest.Assertions);
        Assert.Empty(manifest.Assertions);
        Assert.StartsWith("xmp:iid:", manifest.InstanceID);
    }

    [Fact]
    public void Constructor_WithFormat_ShouldSetFormat()
    {
        // Arrange
        var format = "image/jpeg";

        // Act
        var manifest = new ManifestDefinition(format);

        // Assert
        Assert.Equal(format, manifest.Format);
    }

    [Fact]
    public void ToJson_ShouldReturnValidJson()
    {
        // Arrange
        var manifest = new ManifestDefinition("image/jpeg")
        {
            Title = "Test Manifest",
            Vendor = "Test Vendor"
        };

        // Act
        var json = manifest.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("image/jpeg", json);
        Assert.Contains("Test Manifest", json);
        Assert.Contains("Test Vendor", json);
    }

    [Fact]
    public void FromJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var original = new ManifestDefinition("image/png")
        {
            Title = "Test Title",
            Vendor = "Test Vendor"
        };
        var json = original.ToJson();

        // Act
        var deserialized = ManifestDefinition.FromJson(json);

        // Assert
        Assert.Equivalent(original, deserialized);
    }

    [Fact]
    public void AddClaimGeneratorInfo_ShouldAddToList()
    {
        // Arrange
        var manifest = new ManifestDefinition();
        var claimGenerator = new ClaimGeneratorInfo("TestApp", "1.0.0");

        // Act
        manifest.ClaimGeneratorInfo.Add(claimGenerator);

        // Assert
        var claim = Assert.Single(manifest.ClaimGeneratorInfo);
        Assert.Equal("TestApp", claim.Name);
        Assert.Equal("1.0.0", claim.Version);
    }

    [Fact]
    public void AddAssertion_ShouldAddToList()
    {
        // Arrange
        var manifest = new ManifestDefinition();
        var assertion = new CreativeWorkAssertion(new CreativeWorkAssertionData());

        // Act
        manifest.Assertions.Add(assertion);

        // Assert
        Assert.Single(manifest.Assertions);
        Assert.IsType<CreativeWorkAssertion>(manifest.Assertions[0]);
    }
}