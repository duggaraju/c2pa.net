// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity.Tests;

public class SettingsTests
{
    [Fact]
    public void Settings_CheckDefaults()
    {
        // Act
        var settings = C2pa.Settings.Default;
        // Assert
        Assert.NotNull(settings.Trust);
        Assert.NotNull(settings.Verify);
        Assert.NotNull(settings.Core);
        Assert.NotNull(settings.Builder);
    }

    [Fact]
    public void ToJson_ShouldReturnValidJson()
    {
        // Arrange
        var settings = C2pa.Settings.Default;

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
        var original = C2pa.Settings.Default;

        var json = original.ToJson();

        // Act
        var act = C2pa.LoadSettings(json);
        Assert.True(JsonDocument.Equals(json, act.ToJson()));
    }

    [Fact]
    public void VerifySettings_ShouldLoad()
    {
        const string json = """
            {
                "trust": {
                    "user_anchors": null,
                    "trust_anchors": null,
                    "trust_config": null,
                    "allowed_list": null
                },
                "cawg_trust": {
                },
                "core": {
                },
                "verify": {
                    "verify_after_reading": true,
                    "verify_after_sign": true,
                    "verify_trust": true,
                    "ocsp_fetch": false,
                    "remote_manifest_fetch": true,
                    "skip_ingredient_conflict_resolution": false,
                    "strict_v1_validation": false
                },
                "builder": {
                    "thumbnail": {
                      "enabled": true
                    },
                    "actions": {
                        "auto_created_action": {},
                        "auto_opened_action": {},
                        "auto_placed_action": {},
                        "templates": []
                    }
                }
            }
            """;

        var settings = C2pa.LoadSettings(json);
        Assert.NotNull(settings);
    }
}