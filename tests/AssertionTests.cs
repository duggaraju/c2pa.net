// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity.Tests;

public class AssertionTests
{
    [Fact]
    public void CreativeWorkAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        var data = new CreativeWorkAssertionData("http://schema.org/", "CreativeWork");

        // Act
        var assertion = new CreativeWorkAssertion(data);

        // Assert
        Assert.Equal("stds.schema-org.CreativeWork", assertion.Label);
        Assert.Equal(data, assertion.Data);
    }

    [Fact]
    public void ActionAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        // Act
        var assertion = new ActionsAssertion(new()
        {
            Actions = [
                new()
                {
                    Action = "c2pa.edited"
                }
            ]
        });

        // Assert
        Assert.Equal("c2pa.actions", assertion.Label);
        Assert.Single(assertion.Data.Actions);
        Assert.Equal("c2pa.edited", assertion.Data.Actions[0].Action);
    }

    [Fact]
    public void ClaimThumbnailAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        // Act
        var assertion = new ClaimThumbnailAssertion(new()
        {
            MimeType = "image/jpeg",
            ThumbnailType = 1,
        });

        // Assert
        Assert.Equal("c2pa.thumbnail.claim", assertion.Label);
        Assert.Equal("image/jpeg", assertion.Data.MimeType);
        Assert.Equal(1, assertion.Data.ThumbnailType);
    }

    [Fact]
    public void IngredientThumbnailAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        // Act
        var assertion = new IngredientThumbnailAssertion(new()
        {
            MimeType = "image/jpeg",
            ThumbnailType = 1,
        });

        // Assert
        Assert.Equal("c2pa.thumbnail.ingredient", assertion.Label);
        Assert.Equal("image/jpeg", assertion.Data.MimeType);
        Assert.Equal(1, assertion.Data.ThumbnailType);
    }

    [Fact]
    public void CustomAssertion_ShouldCreateWithCustomLabel()
    {
        // Arrange
        var label = "custom.assertion";
        var data = new { customField = "customValue" };

        // Act
        var assertion = new CustomAssertion(label, data);

        // Assert
        Assert.Equal(label, assertion.Label);
        Assert.Equal(data, assertion.Data);
    }

    [Fact]
    public void CreativeWorkAssertionData_WithAuthors_ShouldSerializeCorrectly()
    {
        // Arrange
        var value = new Dictionary<string, object>
        {
            { "Person", "John Doe" },
            { "Organization", "ACME Corp" }
        };

        // Act
        var data = new CreativeWorkAssertionData("http://schema.org/", "CreativeWork")
        {
            Value = value
        };

        var assertion = new CreativeWorkAssertion(data);
        var json = assertion.ToJson();
        var newAssertion = Assertion.FromJson<CreativeWorkAssertion>(json);

        // Assert
        Assert.Equal(assertion.Data.ToJson(), newAssertion.Data.ToJson());
    }

    [Fact]
    public void C2paAction_WithAllProperties_ShouldCreateCorrectly()
    {
        // Arrange
        var instanceId = Guid.NewGuid().ToString();

        // Act
        var action = new Schema.ActionItemV1
        {
            Action = "c2pa.edited",
            SoftwareAgent = "TestApp v1.0",
            Changed = "color, brightness",
            InstanceId = instanceId
        };

        // Assert
        Assert.Equal("c2pa.edited", action.Action);
        Assert.Equal("TestApp v1.0", action.SoftwareAgent);
        Assert.Equal("color, brightness", action.Changed);
        Assert.Equal(instanceId, action.InstanceId);
    }

    [Fact]
    public void Assertion_ToJson_ShouldSerializeCorrectly()
    {
        // Arrange
        var data = new CreativeWorkAssertionData("http://schema.org/", "CreativeWork");
        var assertion = new CreativeWorkAssertion(data);

        // Act
        var json = assertion.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.Contains("stds.schema-org.CreativeWork", json);
        Assert.Contains("http://schema.org/", json);
    }

    [Fact]
    public void Assertion_FromJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var original = new CreativeWorkAssertion(
            new CreativeWorkAssertionData("http://schema.org/", "CreativeWork"));
        var json = original.ToJson();

        // Act
        var deserialized = Assertion.FromJson<CreativeWorkAssertion>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Label, deserialized.Label);
    }

    #region CertificateStatusAssertion Tests

    [Fact]
    public void CertificateStatusAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        var ocspValBytes = new List<byte[]>
        {
            new byte[] { 0x30, 0x82, 0x01, 0x23 },
            new byte[] { 0x30, 0x82, 0x02, 0x34 }
        };
        var ocspVals = ocspValBytes.Select(Convert.ToBase64String).ToArray();

        // Act
        var assertion = new CertificateStatusAssertion(new()
        {
            OcspVals = ocspVals
        });

        // Assert
        Assert.Equal("c2pa.certificate-status", assertion.Label);
        Assert.Equal(ocspVals, assertion.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_WithEmptyOcspVals_ShouldCreateSuccessfully()
    {
        // Arrange
        string[] emptyOcspVals = [];
        // Act
        var assertion = new CertificateStatusAssertion(new() { OcspVals = emptyOcspVals });

        // Assert
        Assert.Equal("c2pa.certificate-status", assertion.Label);
        Assert.NotNull(assertion.Data.OcspVals);
        Assert.Empty(assertion.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_WithSingleOcspVal_ShouldCreateCorrectly()
    {
        // Arrange
        var ocspVal = new byte[] { 0x30, 0x82, 0x01, 0x23, 0x45, 0x67 };
        string[] ocspVals = [Convert.ToBase64String(ocspVal)];
        // Act
        var assertion = new CertificateStatusAssertion(new() { OcspVals = ocspVals });

        // Assert
        Assert.Single(assertion.Data.OcspVals);
        Assert.Equal(ocspVals, assertion.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_WithMultipleOcspVals_ShouldMaintainAllValues()
    {
        // Arrange
        var ocspValBytes = new List<byte[]>
        {
            new byte[] { 0x30, 0x82, 0x01, 0x23 },
            new byte[] { 0x30, 0x82, 0x02, 0x34, 0x56 },
            new byte[] { 0x30, 0x82, 0x03, 0x45, 0x67, 0x89 },
            new byte[] { 0x30, 0x82, 0x04, 0x56, 0x78, 0x9A, 0xBC }
        };
        var ocspVals = ocspValBytes.Select(Convert.ToBase64String).ToArray();

        // Act
        var assertion = new CertificateStatusAssertion(new() { OcspVals = ocspVals });

        // Assert
        Assert.Equal(ocspVals, assertion.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_ToJson_ShouldSerializeCorrectly()
    {
        // Arrange
        var ocspValBytes = new List<byte[]>
        {
            new byte[] { 0x30, 0x82, 0x01, 0x23 },
            new byte[] { 0x30, 0x82, 0x02, 0x34 }
        };
        var ocspVals = ocspValBytes.Select(Convert.ToBase64String).ToArray();
        var assertion = new CertificateStatusAssertion(new() { OcspVals = ocspVals });

        // Act
        var json = assertion.ToJson(false);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("c2pa.certificate-status", json);
        Assert.Contains("ocspVals", json);
        Assert.Contains("MIIBIw==", json);
        Assert.Contains("MIICNA==", json);
    }

    [Fact]
    public void CertificateStatusAssertion_FromJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var originalOcspValBytes = new List<byte[]>
        {
            new byte[] { 0x30, 0x82, 0x01, 0x23 },
            new byte[] { 0x30, 0x82, 0x02, 0x34 }
        };
        var originalOcspVals = originalOcspValBytes.Select(Convert.ToBase64String).ToArray();
        var originalAssertion = new CertificateStatusAssertion(new() { OcspVals = originalOcspVals });
        var json = originalAssertion.ToJson();

        // Act
        var deserialized = Assertion.FromJson<CertificateStatusAssertion>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(originalAssertion.Label, deserialized.Label);
        Assert.IsType<CertificateStatusAssertion>(deserialized);

        var deserializedCertStatus = (CertificateStatusAssertion)deserialized;
        Assert.Equal(originalOcspVals, deserializedCertStatus.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_RoundTripSerialization_ShouldMaintainData()
    {
        // Arrange
        var originalOcspValBytes = new List<byte[]>
        {
            new byte[] { 0x30, 0x82, 0x01, 0x23 },
            new byte[] { 0x30, 0x82, 0x02, 0x34, 0x56 },
            new byte[] { 0x30, 0x82, 0x03, 0x45, 0x67, 0x89 }
        };
        var originalOcspVals = originalOcspValBytes.Select(Convert.ToBase64String).ToArray();
        var originalAssertion = new CertificateStatusAssertion(new() { OcspVals = originalOcspVals });

        // Act - Serialize and deserialize
        var json = originalAssertion.ToJson();
        var deserializedAssertion = json.FromJson<CertificateStatusAssertion>();

        // Assert
        Assert.Equal(originalAssertion.Label, deserializedAssertion.Label);
        Assert.Equal(originalOcspVals, deserializedAssertion.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_WithLargeOcspVal_ShouldHandleCorrectly()
    {
        // Arrange
        var largeOcspVal = new byte[10000]; // Large OCSP response
        for (int i = 0; i < largeOcspVal.Length; i++)
        {
            largeOcspVal[i] = (byte)(i % 256);
        }
        string[] ocspVals = [Convert.ToBase64String(largeOcspVal)];

        // Act
        var assertion = new CertificateStatusAssertion(new() { OcspVals = ocspVals });
        var json = assertion.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.Equal(ocspVals, assertion.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_WithEmptyByteArray_ShouldHandleCorrectly()
    {
        // Arrange
        var ocspValBytes = new List<byte[]>
        {
            Array.Empty<byte>(), // Empty byte array
            new byte[] { 0x30, 0x82 }, // Non-empty byte array
            Array.Empty<byte>() // Another empty byte array
        };
        var ocspVals = ocspValBytes.Select(Convert.ToBase64String).ToArray();
        // Act
        var assertion = new CertificateStatusAssertion(new() { OcspVals = ocspVals });
        var json = assertion.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.Equal(ocspVals, assertion.Data.OcspVals);
    }

    [Fact]
    public void CertificateStatusAssertion_WithTypicalOcspResponse_ShouldCreateCorrectly()
    {
        // Arrange - Simulate typical OCSP response structure
        var typicalOcspResponse = new byte[]
        {
            0x30, 0x82, 0x01, 0x91, // SEQUENCE, length 401
            0x0A, 0x01, 0x00,       // ENUMERATED: successful (0)
            0x30, 0x82, 0x01, 0x8A  // ResponseBytes
            // ... (truncated for test purposes)
        };
        var ocspVal = Convert.ToBase64String(typicalOcspResponse);

        // Act
        var assertion = new CertificateStatusAssertion(new() { OcspVals = [ocspVal] });

        // Assert
        Assert.Equal("c2pa.certificate-status", assertion.Label);
        Assert.Single(assertion.Data.OcspVals);
        Assert.Equal(ocspVal, assertion.Data.OcspVals[0]);
    }
    #endregion
}