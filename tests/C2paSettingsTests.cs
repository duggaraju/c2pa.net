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
        var settings = new Settings(trust, verify);

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
        var settings = new Settings(trust, verify);

        // Act
        var json = settings.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("trust", json);
        Assert.Contains("verify", json);
    }

    [Fact]
    public void Load_ShouldLoadCorrectly()
    {
        // Arrange
        var original = new Settings(new TrustSettings(), new VerifySettings());
        var json = original.ToJson();

        // Act
        Settings.Load(json);
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

    [Fact]
    public void VerifySettings_ShouldLoad()
    {
        const string json = """
            {   
                "version_major": 1,
                "version_minor": 0,
                "trust": {
                    "private_anchors": null,
                    "trust_anchors": null,
                    "trust_config": null,
                    "allowed_list": null
                },
                "core": {
                    "debug": false,
                    "hash_alg": "sha256",
                    "salt_jumbf_boxes": true,
                    "prefer_box_hash": false,
                    "prefer_bmff_merkle_tree": false,
                    "compress_manifests": true,
                    "max_memory_usage": null
                },
                "verify": {
                    "verify_after_reading": true,
                    "verify_after_sign": true,
                    "verify_trust": true,
                    "ocsp_fetch": false,
                    "remote_manifest_fetch": true,
                    "check_ingredient_trust": true,
                    "skip_ingredient_conflict_resolution": false,
                    "strict_v1_validation": false
                },
                "builder": {
                    "auto_thumbnail": true
                }
            }
            """;

        var settings = Settings.Load(json);
        Assert.NotNull(settings);
        Assert.Equal(1, settings.MajorVersion);
        Assert.Equal(0, settings.MinorVersion);
    }
}