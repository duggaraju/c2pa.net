namespace Microsoft.ContentAuthenticity;

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
public record ActionSettings(
    );

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record ActionsSettings(IList<ActionSettings>? Actions);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record ClaimGeneratorInfoSettings(
    string? Name,
    string? Version,
    string? OperatingSystem);

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

    public string ToJson()
    {
        return Utils.Serialize(this);
    }

    public void Load()
    {
        Load(ToJson());
    }

    private readonly static byte[] Format = Encoding.UTF8.GetBytes("json");

    public static Settings Load(string settings, string format = "json")
    {
        unsafe
        {
            var bytes = Encoding.UTF8.GetBytes(settings);
            fixed (byte* p = bytes)
            fixed (byte* f = Format)
            {
                var ret = C2paBindings.load_settings((sbyte*)p, (sbyte*)f);
                if (ret != 0)
                {
                    C2pa.CheckError();
                }
            }
        }
        return Utils.Deserialize<Settings>(settings);
    }

    public static Settings FromJson(string json) => Utils.Deserialize<Settings>(json);
}
