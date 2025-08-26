using Microsoft.ContentAuthenticity.Bindings;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.ContentAuthenticity
{
    public record TrustSettings(string? UserAnchors = null, string? TrustAnchors = null, string? TrustConfig = null, string? AllowedList = null);

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
    }
}
