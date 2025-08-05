
namespace Microsoft.ContentAuthenticity.Bindings
{
    public record TrustSettings
    {

    }

    public record VerifySettings
    {

    }

    public record C2paSettings(TrustSettings Trust, VerifySettings Verify)
    {
        public string ToJson()
        {
            return Utils.Serialize(this);
        }

        public static C2paSettings FromJson(string json)
        {
            return Utils.Deserialize<C2paSettings>(json);
        }
    }
}
