namespace Microsoft.ContentAuthenticity.BindingTests;

public class C2paSettingsTests
{
    [Fact]
    public void Constructor_ShouldCreateWithTrustAndVerifySettings()
    {
        // Arrange
        var trust = new TrustSettings();
        var verify = new VerifySettings();
        
        // Act
        var settings = new C2paSettings(trust, verify);
        
        // Assert
        Assert.Equal(trust, settings.Trust);
        Assert.Equal(verify, settings.Verify);
    }

    [Fact]
    public void ToJson_ShouldReturnValidJson()
    {
        // Arrange
        var trust = new TrustSettings();
        var verify = new VerifySettings();
        var settings = new C2paSettings(trust, verify);
        
        // Act
        var json = settings.ToJson();
        
        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("trust", json);
        Assert.Contains("verify", json);
    }

    [Fact]
    public void FromJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var original = new C2paSettings(new TrustSettings(), new VerifySettings());
        var json = original.ToJson();
        
        // Act
        var deserialized = C2paSettings.FromJson(json);
        
        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Trust);
        Assert.NotNull(deserialized.Verify);
    }

    [Fact]
    public void TrustSettings_ShouldBeRecord()
    {
        // Arrange & Act
        var trust1 = new TrustSettings();
        var trust2 = new TrustSettings();
        
        // Assert
        Assert.Equal(trust1, trust2);
        Assert.True(trust1.Equals(trust2));
    }

    [Fact]
    public void VerifySettings_ShouldBeRecord()
    {
        // Arrange & Act
        var verify1 = new VerifySettings();
        var verify2 = new VerifySettings();
        
        // Assert
        Assert.Equal(verify1, verify2);
        Assert.True(verify1.Equals(verify2));
    }
}