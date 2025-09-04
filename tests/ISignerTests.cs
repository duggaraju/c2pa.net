// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity.Tests;

public class ISignerTests
{
    [Fact]
    public void ISigner_ShouldHaveRequiredProperties()
    {
        // Arrange
        var mockSigner = new Mock<ISigner>();
        mockSigner.Setup(s => s.Alg).Returns(SigningAlg.Es256);
        mockSigner.Setup(s => s.Certs).Returns("certificate-data");
        mockSigner.Setup(s => s.TimeAuthorityUrl).Returns("https://timestamp.example.com");
        mockSigner.Setup(s => s.UseOcsp).Returns(false);

        // Act
        var signer = mockSigner.Object;

        // Assert
        Assert.Equal(SigningAlg.Es256, signer.Alg);
        Assert.Equal("certificate-data", signer.Certs);
        Assert.Equal("https://timestamp.example.com", signer.TimeAuthorityUrl);
        Assert.False(signer.UseOcsp);
    }

    [Fact]
    public void ISigner_Sign_ShouldBeCallable()
    {
        // Use TestSigner instead of Mock since Moq doesn't work well with ref structs
        var testSigner = new TestSigner();
        var testData = new ReadOnlySpan<byte>([1, 2, 3, 4]);
        var hashBuffer = new Span<byte>(new byte[64]);

        // Act
        var result = testSigner.Sign(testData, hashBuffer);

        // Assert
        Assert.Equal(8, result); // TestSigner returns 8 bytes
        Assert.Equal(1, hashBuffer[0]);
        Assert.Equal(2, hashBuffer[1]);
    }

    [Fact]
    public void ISigner_UseOcsp_ShouldDefaultToFalse()
    {
        // Arrange
        var mockSigner = new Mock<ISigner>();

        // Act
        var useOcsp = mockSigner.Object.UseOcsp;

        // Assert
        Assert.False(useOcsp);
    }

    [Fact]
    public void ISigner_Sign_WithMockSigner_CanBeSetupWithCallback()
    {
        // Since Moq doesn't work well with ref structs, we'll test the Sign method
        // using a custom mock implementation or use TestSigner for comprehensive testing
        var testSigner = new TestSigner();
        var testData = new ReadOnlySpan<byte>([1, 2, 3, 4, 5]);
        var hashBuffer = new Span<byte>(new byte[32]);

        // Act
        var result = testSigner.Sign(testData, hashBuffer);

        // Assert
        Assert.Equal(8, result);
        Assert.Equal(1, hashBuffer[0]);
        Assert.Equal(2, hashBuffer[1]);
        Assert.Equal(3, hashBuffer[2]);
        Assert.Equal(4, hashBuffer[3]);
    }

    [Fact]
    public void TestSigner_ShouldImplementAllRequiredProperties()
    {
        // Arrange & Act
        var testSigner = new TestSigner
        {
            Alg = SigningAlg.Es384,
            Certs = "custom-cert",
            TimeAuthorityUrl = "https://custom.timestamp.com",
            UseOcsp = true
        };

        // Assert
        Assert.Equal(SigningAlg.Es384, testSigner.Alg);
        Assert.Equal("custom-cert", testSigner.Certs);
        Assert.Equal("https://custom.timestamp.com", testSigner.TimeAuthorityUrl);
        Assert.True(testSigner.UseOcsp);
    }
}

// Test implementation of ISigner for integration tests
public class TestSigner : ISigner
{
    public SigningAlg Alg { get; init; } = SigningAlg.Es256;
    public string Certs { get; init; } = "test-certificate";
    public string? TimeAuthorityUrl { get; init; } = "https://timestamp.test.com";
    public bool UseOcsp { get; init; } = false;

    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        // Mock signature - in real implementation this would use cryptographic signing
        var mockSignature = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        mockSignature.CopyTo(hash);
        return mockSignature.Length;
    }
}