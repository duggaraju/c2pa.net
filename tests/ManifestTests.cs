namespace Microsoft.ContentAuthenticity.Tests;

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
        Assert.IsAssignableFrom<ResourceRef>(thumbnail);
    }

    [Fact]
    public void ResourceRef_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var resourceRef = new ResourceRef("image/png", "resource-456");

        // Assert
        Assert.Equal("image/png", resourceRef.Format);
        Assert.Equal("resource-456", resourceRef.Identifier);
        Assert.NotNull(resourceRef.DataTypes);
        Assert.Empty(resourceRef.DataTypes);
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
        var alg = C2paSigningAlg.Es256;
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
        var info = new ClaimGeneratorInfo();

        // Assert
        Assert.Equal("", info.Name);
        Assert.Equal("", info.Version);
    }

    [Fact]
    public void ClaimGeneratorInfo_ShouldCreateWithNameAndVersion()
    {
        // Arrange & Act
        var info = new ClaimGeneratorInfo("TestApp", "2.0.1");

        // Assert
        Assert.Equal("TestApp", info.Name);
        Assert.Equal("2.0.1", info.Version);
    }

    [Fact]
    public void Ingredient_ShouldCreateWithDefaults()
    {
        // Arrange & Act
        var ingredient = new Ingredient();

        // Assert
        Assert.Equal("", ingredient.Title);
        Assert.Equal("", ingredient.Format);
        Assert.Equal(Relationship.ParentOf, ingredient.Relationship);
        Assert.Null(ingredient.DocumentID);
        Assert.Null(ingredient.InstanceID);
        Assert.Null(ingredient.C2paManifest);
        Assert.Null(ingredient.HashedManifestUri);
        Assert.Null(ingredient.ValidationStatus);
        Assert.Null(ingredient.Thumbnail);
        Assert.Null(ingredient.Data);
    }

    [Fact]
    public void Ingredient_ShouldCreateWithTitleFormatAndRelationship()
    {
        // Arrange & Act
        var ingredient = new Ingredient("Test Image", "image/jpeg", Relationship.ComponentOf);

        // Assert
        Assert.Equal("Test Image", ingredient.Title);
        Assert.Equal("image/jpeg", ingredient.Format);
        Assert.Equal(Relationship.ComponentOf, ingredient.Relationship);
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
}