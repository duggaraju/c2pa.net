// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity.Tests;

public class UtilsTests
{
    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".tiff", "image/tiff")]
    [InlineData(".tif", "image/tiff")]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".mp4", "video/mp4")]
    [InlineData(".mov", "video/quicktime")]
    [InlineData(".avi", "video/x-msvideo")]
    [InlineData(".mp3", "audio/mpeg")]
    [InlineData(".wav", "audio/wav")]
    [InlineData(".unknown", "application/octet-stream")]
    public void GetMimeTypeFromExtension_ShouldReturnCorrectMimeType(string extension, string expectedMimeType)
    {
        // Act
        var mimeType = Utils.GetMimeTypeFromExtension(extension);

        // Assert
        Assert.Equal(expectedMimeType, mimeType);
    }

    [Theory]
    [InlineData(".JPG", "image/jpeg")]
    [InlineData(".PNG", "image/png")]
    [InlineData(".WEBP", "image/webp")]
    public void GetMimeTypeFromExtension_ShouldBeCaseInsensitive(string extension, string expectedMimeType)
    {
        // Act
        var mimeType = Utils.GetMimeTypeFromExtension(extension);

        // Assert
        Assert.Equal(expectedMimeType, mimeType);
    }

    [Theory]
    [InlineData("c2pa.actions", typeof(ActionsAssertion))]
    [InlineData("c2pa.actions.v2", typeof(ActionsAssertionV2))]
    [InlineData("c2pa.thumbnail", typeof(ThumbnailAssertion))]
    [InlineData("c2pa.training-mining", typeof(TrainingAssertion))]
    [InlineData("c2pa.thumbnail.claim.123", typeof(ClaimThumbnailAssertion))]
    [InlineData("c2pa.thumbnail.ingredient.456", typeof(IngredientThumbnailAssertion))]
    [InlineData("stds.schema-org.CreativeWork", typeof(CreativeWorkAssertion))]
    [InlineData("c2pa.metadata", typeof(MetadataAssertion))]
    [InlineData("c2pa.soft-binding", typeof(SoftBindingAssertion))]
    [InlineData("c2pa.asset-type", typeof(AssetTypeAssertion))]
    [InlineData("c2pa.asset-ref", typeof(AssetReferenceAssertion))]
    [InlineData("c2pa.time-stamp", typeof(TimeStampAssertion))]
    [InlineData("c2pa.certificate-status", typeof(CertificateStatusAssertion))]
    [InlineData("c2pa.embedded-data", typeof(EmbeddedDataAssertion))]
    [InlineData("custom.assertion", typeof(CustomAssertion))]
    public void GetAssertionTypeFromLabel_ShouldReturnCorrectType(string label, Type expectedType)
    {
        // Act
        var type = JsonExtensions.GetAssertionTypeFromLabel(label);

        // Assert
        Assert.Equal(expectedType, type);
    }

    [Fact]
    public void Serialize_ShouldProduceValidJson()
    {
        // Arrange
        var obj = new { Name = "Test", Value = 42 };

        // Act
        var json = obj.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.Contains("name", json); // snake_case
        Assert.Contains("value", json);
        Assert.Contains("Test", json);
        Assert.Contains("42", json);
    }

    [Fact]
    public void Deserialize_ShouldDeserializeValidJson()
    {
        // Arrange
        var json = """{"name": "Test", "value": 42}""";

        // Act
        var obj = json.Deserialize<TestObject>();

        // Assert
        Assert.NotNull(obj);
        Assert.Equal("Test", obj.Name);
        Assert.Equal(42, obj.Value);
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ShouldThrowJsonException()
    {
        // Arrange
        var invalidJson = "invalid json";

        // Act & Assert
        Assert.Throws<JsonException>(() => invalidJson.Deserialize<TestObject>());
    }

    [Fact]
    public void Deserialize_WithNullResult_ShouldThrowJsonException()
    {
        // Arrange
        var nullJson = "null";

        // Act & Assert
        Assert.Throws<JsonException>(() => nullJson.Deserialize<TestObject>());
    }

    [Fact]
    public void JsonOptions_ShouldUseSnakeCaseNaming()
    {
        // Arrange
        var obj = new TestObject { Name = "Test", Value = 42 };

        // Act
        var json = obj.ToJson();

        // Assert
        Assert.Contains("name", json);
        Assert.Contains("value", json);
        Assert.DoesNotContain("Name", json);
        Assert.DoesNotContain("Value", json);
    }

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}