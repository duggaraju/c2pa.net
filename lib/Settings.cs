
using System.Security.Cryptography.X509Certificates;

namespace ContentAuthenticity;

public enum OcspFetch
{
    All,
    Active
}

public enum ThumbprintQuality
{
    Low,
    Medium,
    High
}

// [trust]
public record TrustSettings(string? UserAnchors = null, string? TrustAnchors = null, string? TrustConfig = null, string? AllowedList = null)
{
    private static TrustSettings? __cachedSettings = null;

    public static TrustSettings Default
    {
        get
        {
            if (__cachedSettings == null)
            {
                __cachedSettings = new TrustSettings();
            }
            return __cachedSettings;
        }
    }
}

// [verify]
public record VerifySettings(
    bool VerifyAfterReading = true,
    bool VerifyAfterSign = true,
    bool VerifyTrust = true,
    bool VerifyTimestampTrust = true,
    bool OcspFetch = false,
    bool RemoteManifestFetch = true,
    bool CheckIngredientTrust = true,
    bool SkipIngredientConflictResolution = false,
    bool StringV1Validation = false,
    bool Detailed = false)
{
    private static VerifySettings? __cachedSettings = null;

    public static VerifySettings Default
    {
        get
        {
            if (__cachedSettings == null)
            {
                __cachedSettings = new VerifySettings();
            }
            return __cachedSettings;
        }
    }
}

// [signer]
public record SignerSettings(LocalSignerSettings? Local = null, RemoteSignerSettings? Remote = null);

// [signer.local]
public record LocalSignerSettings(
    string? Alg = null,
    string? SignCert = null,
    string? PrivateKey = null,
    string? TsaUrl = null);

// [signer.remote]
public record RemoteSignerSettings(
    string? Url = null,
    string? Alg = null,
    string? SignCert = null,
    string? TsaUrl = null );

// [builder]
public record BuilderSettings(ThumbnailSettings Thumbnail, BuilderActionsSettings Actions, ClaimGeneratorInfoSettings? ClaimGeneratorInfo = null, OcspFetch? certificateStatusFetch = null, bool? certificateStatusShouldOverride = null)
{
    private static BuilderSettings? __cachedSettings = null;

    public static BuilderSettings Default
    {
        get
        {
            if (__cachedSettings == null)
            {
                __cachedSettings = new BuilderSettings(ThumbnailSettings.Default, BuilderActionsSettings.Default);
            }
            return __cachedSettings;
        }
    }
}

// [builder.claim_generator_info]
public record ClaimGeneratorInfoSettings(
    string Name = "My Service",
    string Version = "1.0.0",
    OperatingSystemSettings? OperatingSystem = null,
    string? SomeOtherField = null);

// [builder.claim_generator_info.operating_system]
public record OperatingSystemSettings(
    string? Name = null,
    bool Infer = true);

// [builder.actions]
public record BuilderActionsSettings(
    AutoActionSetting AutoCreatedAction,
    AutoActionSetting AutoOpenedAction,
    AutoActionSetting AutoPlacedAction,
    bool AllActionsIncluded = true,
    TemplatesSettings[]? Templates = null,
    BuilderActionsActionsSettings[]? Actions = null)
{
    private static BuilderActionsSettings? __cachedSettings = null;

    public static BuilderActionsSettings Default
    {
        get
        {
            if (__cachedSettings == null)
            {
                __cachedSettings = new BuilderActionsSettings(AutoActionSetting.DefaultCreated, AutoActionSetting.DefaultOpenedAndPlaced, AutoActionSetting.DefaultOpenedAndPlaced);
            }
            return __cachedSettings;
        }
    }
}

// [builder.actions.templates]
public record TemplatesSettings(
    string? Action = null,
    string? SourceType = null,
    string? Description = null,
    Dictionary<string, string>? TemplateParameters = null,
    ClaimGeneratorInfo? SoftwareAgent = null);

// [builder.actions.actions]
public record BuilderActionsActionsSettings(string action = "");

// [builder.actions.auto_*_action]
public record AutoActionSetting(bool Enabled = true, DigitalSourceType? SourceType = null)
{
    private static AutoActionSetting? __cachedDefaultCreatedAction = null;
    private static AutoActionSetting? __cachedDefaultOpenedAndPlacedAction = null;

    private static AutoActionSetting CreateDefaultAction(DigitalSourceType? sourceType = null)
    {
        return new AutoActionSetting(SourceType: sourceType);
    }

    public static AutoActionSetting DefaultCreated
    {
        get
        {
            if (__cachedDefaultCreatedAction == null)
            {
                __cachedDefaultCreatedAction = CreateDefaultAction(DigitalSourceType.Empty);
            }
            return __cachedDefaultCreatedAction;
        }
    }

    public static AutoActionSetting DefaultOpenedAndPlaced
    {
        get
        {
            if (__cachedDefaultOpenedAndPlacedAction == null)
            {
                __cachedDefaultOpenedAndPlacedAction = CreateDefaultAction();
            }
            return __cachedDefaultOpenedAndPlacedAction;
        }
    }
}

// builder.thumbnail
public record ThumbnailSettings(
    bool Enabled = true,
    bool IgnoreErrors = true,
    int LongEdge = 1024, // size of the longest edge of the thumbnail
    string? Format = null,
    bool PreferSmallestFormat = true,
    ThumbprintQuality Quality = ThumbprintQuality.Medium)
{
    private static ThumbnailSettings? __cachedSettings = null;

    public static ThumbnailSettings Default
    {
        get
        {
            if (__cachedSettings == null)
            {
                __cachedSettings = new ThumbnailSettings();
            }
            return __cachedSettings;
        }
    }
}

// [core]
public record CoreSettings(bool Debug = false, string? HashAlg = null, string? SoftHashAlg = null, bool SaltJumbfBoxes = true, bool PreferBoxHash = false, UIntPtr? MerkleTreeMaxProofs = null, bool CompressManifest = true, UIntPtr? MaxMemoryUsage = null)
{
    private static CoreSettings? __cachedSettings = null;

    public static CoreSettings Default
    {
        get
        {
            if (__cachedSettings == null)
            {
                __cachedSettings = new CoreSettings();
            }
            return __cachedSettings;
        }
    }
}

// [fragment]
public record FragmentSettings(string? FragmentsGlob = null);

public record Settings(TrustSettings Trust, TrustSettings CawgTrust, VerifySettings Verify, BuilderSettings Builder, CoreSettings Core, FragmentSettings? Fragment = null, SignerSettings ? signerSettings = null)
{
    [JsonPropertyName("version_major")]
    public int MajorVersion { get; init; } = 1;

    [JsonPropertyName("version_minor")]
    public int MinorVersion { get; init; } = 0;

    private static Settings? __cachedSettings = null;

    public static Settings Default
    {
        get
        {
            if (__cachedSettings == null)
            {
                __cachedSettings = new Settings(TrustSettings.Default, TrustSettings.Default, VerifySettings.Default, BuilderSettings.Default, CoreSettings.Default);
            }
            return __cachedSettings;
        }
    }
    
    public void Load()
    {
        Load(ToJson());
    }

    public string ToJson()
    {
        return Utils.Serialize(this);
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
            return Utils.Deserialize<Settings>(settings);
        return null;
    }
    
    public static Settings FromJson(string json) => Utils.Deserialize<Settings>(json);
}
