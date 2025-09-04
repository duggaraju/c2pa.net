// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

public record TrustSettings(
    string? UserAnchors = null,
    string? TrustAnchors = null,
    string? TrustConfig = null,
    string? AllowedList = null);

public record VerifySettings(
    bool VerifyAfterReading = true,
    bool VerifyAfterSign = true,
    bool VerifyTrust = false,
    bool VerifyTimestampTrust = false,
    bool OcspFetch = false,
    bool CheckIngredientTrust = true,
    bool SkipIngredientConflictResolution = false,
    bool RemoteManifestFetch = true,
    bool StrictV1Validation = false);

public record ThumbnailSettings(
    bool Enabled = true,
    bool IgnoreErrors = true,
    int LongEdge = 1024,
    bool PreferSmallestFormat = true,
    string Quality = "Medium",
    string? Format = null);


[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record ActionTemplateSettings(
    string Action,
    ClaimGeneratorInfo? SoftwareAgent,
    int? SoftwareAgentIndex,
    string? SourceType,
    ResourceRef? Icon,
    string? Description,
    Dictionary<string, object>? TemplateParameters);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record ActionsSettings(
    IList<ActionTemplateSettings>? Templates,
    IList<ActionSettings>? Actions);


[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record ActionSettings(
    string Action,
    DateTimeOffset? When,
    ClaimGeneratorInfo? SoftwareAgent,
    int? SoftwareAgentIndex,
    IList<RegionOfInterestSetting>? changes,
    Dictionary<string, object>? Parameters);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record ClaimGeneratorInfoSettings(
    string Name,
    string? Version,
    string? OperatingSystem,
    ResourceRef? Icon,
    Dictionary<string, object>? Other);

public record BuilderSettings(
    ClaimGeneratorInfoSettings? ClaimGeneratorInfo,
    ThumbnailSettings? Thumbnail,
    ActionsSettings? Actions);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record CoreSettings(
    bool Debug = false,
    string HashAlg = "sha256",
    bool SaltJumbfBoxes = true,
    bool PreferBoxHash = false,
    bool CompressManifest = true,
    long? MaxMemoryUsage = null);

public record Settings(
    TrustSettings? Trust = null,
    VerifySettings? Verify = null,
    CoreSettings? Core = null,
    BuilderSettings? Builder = null)
{
    [JsonPropertyName("version_major")]
    public int MajorVersion { get; init; } = 1;

    [JsonPropertyName("version_minor")]
    public int MinorVersion { get; init; } = 0;

    public void Load()
    {
        Load(this.ToJson());
    }

    public static Settings? Load(string settings, string format = "json")
    {
        unsafe
        {
            fixed (byte* s = Encoding.UTF8.GetBytes(settings))
            fixed (byte* f = Encoding.UTF8.GetBytes(format))
            {
                var ret = C2paBindings.load_settings((sbyte*)s, (sbyte*)f);
                if (ret != 0)
                {
                    C2pa.CheckError();
                }
            }
        }
        if (format == "json")
            return settings.Deserialize<Settings>();
        return null;
    }

    public static Settings FromJson(string json) => json.Deserialize<Settings>();
}