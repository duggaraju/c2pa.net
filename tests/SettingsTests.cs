namespace ContentAuthenticity.Tests;

public class SettingsTests
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
    public void Settings_ShouldHaveCorrectDefaults()
    {
        // Act
        var settings = new Settings();

        // Assert
        Assert.Null(settings.Trust);
        Assert.Null(settings.Verify);
        Assert.Null(settings.Core);
        Assert.Null(settings.Builder);
        Assert.Equal(1, settings.MajorVersion);
        Assert.Equal(0, settings.MinorVersion);
    }

    [Fact]
    public void TrustSettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var trust = new TrustSettings();

        // Assert
        Assert.Null(trust.UserAnchors);
        Assert.Null(trust.TrustAnchors);
        Assert.Null(trust.TrustConfig);
        Assert.Null(trust.AllowedList);
    }

    [Fact]
    public void VerifySettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var verify = new VerifySettings();

        // Assert
        Assert.True(verify.VerifyAfterReading);
        Assert.True(verify.VerifyAfterSign);
        Assert.False(verify.VerifyTrust);
        Assert.False(verify.VerifyTimestampTrust);
        Assert.False(verify.OcspFetch);
        Assert.True(verify.CheckIngredientTrust);
        Assert.False(verify.SkipIngredientConflictResolution);
        Assert.True(verify.RemoteManifestFetch);
        Assert.False(verify.StrictV1Validation);
    }

    [Fact]
    public void CoreSettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var core = new CoreSettings();

        // Assert
        Assert.False(core.Debug);
        Assert.Equal("sha256", core.HashAlg);
        Assert.True(core.SaltJumbfBoxes);
        Assert.False(core.PreferBoxHash);
        Assert.True(core.CompressManifest);
        Assert.Null(core.MaxMemoryUsage);
    }

    [Fact]
    public void ThumbnailSettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var thumbnail = new ThumbnailSettings();

        // Assert
        Assert.True(thumbnail.Enabled);
        Assert.True(thumbnail.IgnoreErrors);
        Assert.Equal(1024, thumbnail.LongEdge);
        Assert.True(thumbnail.PreferSmallestFormat);
        Assert.Equal("Medium", thumbnail.Quality);
        Assert.Null(thumbnail.Format);
    }

    [Fact]
    public void BuilderSettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var builder = new BuilderSettings(null, null, null);

        // Assert
        Assert.Null(builder.ClaimGeneratorInfo);
        Assert.Null(builder.Thumbnail);
        Assert.Null(builder.Actions);
    }

    [Fact]
    public void ActionSettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var action = new ActionSettings();

        // Assert - ActionSettings has no properties with defaults, just ensuring it can be constructed
        Assert.NotNull(action);
    }

    [Fact]
    public void ActionsSettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var actions = new ActionsSettings(null);

        // Assert
        Assert.Null(actions.Actions);
    }

    [Fact]
    public void ClaimGeneratorInfoSettings_ShouldHaveCorrectDefaults()
    {
        // Act
        var info = new ClaimGeneratorInfoSettings(null, null, null);

        // Assert
        Assert.Null(info.Name);
        Assert.Null(info.Version);
        Assert.Null(info.OperatingSystem);
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
                    "user_anchors": null,
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
                    "thumbnail": {
                      "enabled": true
                    }
                }
            }
            """;

        var settings = Settings.Load(json);
        Assert.NotNull(settings);
        Assert.Equal(1, settings.MajorVersion);
        Assert.Equal(0, settings.MinorVersion);
    }
}