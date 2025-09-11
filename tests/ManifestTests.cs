// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace ContentAuthenticity.Tests;

public class ManifestTests
{
    [Fact]
    public void Thumbnail_ShouldInheritFromResourceRef()
    {
        // Arrange & Act
        var thumbnail = new Thumbnail("image/jpeg", "thumb-123");

        // Assert
        Assert.Equal("image/jpeg", thumbnail.Format);
        Assert.Equal("thumb-123", thumbnail.Identifier);
        Assert.IsType<ResourceRef>(thumbnail, exactMatch: false);
    }

    [Fact]
    public void ResourceRef_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var resourceRef = new ResourceRef("image/png", "resource-456");

        // Assert
        Assert.Equal("image/png", resourceRef.Format);
        Assert.Equal("resource-456", resourceRef.Identifier);
        Assert.Null(resourceRef.DataTypes);
        Assert.Null(resourceRef.Alg);
        Assert.Null(resourceRef.Hash);
    }

    [Fact]
    public void AssetType_ShouldCreateWithTypeAndVersion()
    {
        // Arrange & Act
        var assetType = new AssetType("image", "1.0");

        // Assert
        Assert.Equal("image", assetType.Type);
        Assert.Equal("1.0", assetType.Version);
    }

    [Fact]
    public void HashedUri_ShouldCreateWithAllProperties()
    {
        // Arrange
        var url = "https://example.com/resource";
        var alg = SigningAlg.Es256;
        var hash = new byte[] { 1, 2, 3, 4 };
        var salt = new byte[] { 5, 6, 7, 8 };

        // Act
        var hashedUri = new HashedUri(url, alg, hash, salt);

        // Assert
        Assert.Equal(url, hashedUri.Url);
        Assert.Equal(alg, hashedUri.Alg);
        Assert.Equal(hash, hashedUri.Hash);
        Assert.Equal(salt, hashedUri.Salt);
    }

    [Fact]
    public void ValidationStatus_ShouldCreateWithAllProperties()
    {
        // Arrange
        var code = "validation.success";
        var url = "https://example.com/validation";
        var explanation = "Validation passed";

        // Act
        var status = new ValidationStatus(code, url, explanation);

        // Assert
        Assert.Equal(code, status.Code);
        Assert.Equal(url, status.Url);
        Assert.Equal(explanation, status.Explanation);
    }

    [Fact]
    public void ClaimGeneratorInfo_ShouldCreateWithDefaults()
    {
        // Arrange & Act
        var info = new ClaimGeneratorInfo("dummy");

        // Assert
        Assert.Equal("dummy", info.Name);
        Assert.Null(info.Version);
        Assert.Null(info.OperatingSystem);
    }

    [Fact]
    public void ClaimGeneratorInfo_ShouldCreateWithNameAndVersion()
    {
        // Arrange & Act
        var info = new ClaimGeneratorInfo("TestApp", "2.0.1");

        // Assert
        Assert.Equal("TestApp", info.Name);
        Assert.Equal("2.0.1", info.Version);
        Assert.Null(info.OperatingSystem);
    }

    [Fact]
    public void Ingredient_ShouldCreateWithDefaults()
    {
        // Arrange & Act
        var ingredient = new Ingredient();

        // Assert
        Assert.Null(ingredient.Title);
        Assert.Null(ingredient.Format);
        Assert.Equal(Relationship.ComponentOf, ingredient.Relationship);
        Assert.Null(ingredient.DocumentId);
        Assert.Null(ingredient.InstanceId);
        Assert.Null(ingredient.C2paManifest);
        Assert.Null(ingredient.HashedManifestUri);
        Assert.Null(ingredient.ValidationStatus);
        Assert.Null(ingredient.Thumbnail);
        Assert.Null(ingredient.Data);
    }

    [Theory]
    [InlineData(Relationship.ParentOf)]
    [InlineData(Relationship.ComponentOf)]
    [InlineData(Relationship.InputTo)]
    public void Relationship_ShouldHaveValidValues(Relationship relationship)
    {
        // Act & Assert
        Assert.True(Enum.IsDefined(typeof(Relationship), relationship));
    }

    [Fact(Skip = "Not exact match")]
    public async Task ValidateSchemaMatches()
    {

        var schemUri = "https://raw.githubusercontent.com/contentauth/json-manifest-reference/refs/heads/main/_data/ManifestStore_schema.json";
        // download the schema
        using var client = new HttpClient();
        var response = await client.GetAsync(schemUri);
        var schemaJson = await response.Content.ReadAsStreamAsync();
        var expected = JsonNode.Parse(schemaJson);

        var options = JsonExtensions.JsonSerializerOptions();
        JsonNode actual = options.GetJsonSchemaAsNode(typeof(ManifestStore));
        Console.WriteLine(actual.ToString());
        Assert.Equal(expected, actual);
    }
}