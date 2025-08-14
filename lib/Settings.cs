
using System.Text.Json.Serialization;

namespace Microsoft.ContentAuthenticity.Bindings
{
    public record TrustSettings(string? UserAnchors = null, string? TrustAnchors = null, string? TrustConfig = null, string AllowedList = null);

    public record VerifySettings(
        bool VerifyAfterReading = true,
        bool VerifyAfterSigning = true,
        bool VerifyTrust = false,
        bool VerifyTimeStampTrust = false,
        bool OCSPFetch = false);

    public record BuilderSettings(bool AutoThumbnail = true);

    public record CoreSettings(bool Debug = false, string HashAlg = "sha256", bool SaltJumbfBoxes = true, bool CompressManifest = true);

    public record Settings(TrustSettings? Trust = null, VerifySettings? Verify = null, CoreSettings? Core = null, BuilderSettings? Builder = null)
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

        public static Settings Load(string settings, string format = "json")
        {
            var ret = c2pa.C2paLoadSettings(settings, format);
            if (ret != 0)
            {
                C2pa.CheckError();
            }
            return Utils.Deserialize<Settings>(settings);
        }
    }
}
