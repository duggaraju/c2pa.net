using SigningAlg = Microsoft.ContentAuthenticity.Bindings.C2paSigningAlg;

namespace Microsoft.ContentAuthenticity
{
    // See https://opensource.contentauthenticity.org/docs/manifest/json-ref/manifest-def for the schema.
    public record ManifestDefinition(string Format = "application/octet-stream")
    {
        public SigningAlg Alg { get; set; } = SigningAlg.Es256;

        public string? Vendor { get; set; }

        public List<ClaimGeneratorInfo> ClaimGeneratorInfo { get; set; } = [];

        public string? Title { get; set; }

        public string InstanceID { get; set; } = "xmp:iid:" + Guid.NewGuid().ToString();

        public Thumbnail? Thumbnail { get; set; }

        public List<Ingredient> Ingredients { get; set; } = [];

        public List<Assertion> Assertions { get; set; } = [];

        public List<string>? Redactions { get; set; }

        public string? Label { get; set; }

        public string ToJson()
        {
            return Utils.Serialize(this);
        }

        public static ManifestDefinition FromJson(string json)
        {
            return Utils.Deserialize<ManifestDefinition>(json);
        }
    }
}
