using Microsoft.ContentAuthenticity.Bindings;
using System.Text.Json.Serialization;

namespace Microsoft.ContentAuthenticity
{
    // See https://opensource.contentauthenticity.org/docs/manifest/json-ref/reader for the schema.

    public enum AssertionKind
    {
        Cbor,
        Json,
        Binary,
        Uri
    }

    public record ManifestAssertion(string Label, object Data, AssertionKind Kind, int? Instance = null) : Assertion(Label, Data);


    public record Thumbnail(string Format, string Identifier) : ResourceRef(Format, Identifier);

    public record ResourceRef(string Format, string Identifier)
    {
        public List<AssetType>? DataTypes { get; set; } = [];

        public string? Alg { get; set; } = null;

        public string? Hash { get; set; } = null;
    }

    public record AssetType(
        [property: JsonPropertyName("asset_type")] string Type,
        string? Version = null);

    // Ingredient
    public record HashedUri(string Url, C2paSigningAlg Alg, byte[] Hash, byte[] Salt);

    public record ValidationStatus(string Code, string Url, string Explanation);

    public enum Relationship
    {
        [JsonPropertyName("parentOf")]
        ParentOf,

        [JsonPropertyName("componentOf")]
        ComponentOf,

        [JsonPropertyName("inputTo")]
        InputTo,
    }

    // Manifest
    public record ClaimGeneratorInfo(string Name = "", string Version = "", string? OperatingSystem = null);

    public record Ingredient(string Title = "", string Format = "", Relationship Relationship = Relationship.ParentOf)
    {
        public string? DocumentID { get; set; }

        public string? InstanceID { get; set; }

        public HashedUri? C2paManifest { get; set; }

        public HashedUri? HashedManifestUri { get; set; }

        public List<ValidationStatus>? ValidationStatus { get; set; }

        public Thumbnail? Thumbnail { get; set; }

        public HashedUri? Data { get; set; }

        public string? Description { get; set; }

        public string? InformationalUri { get; set; }
    }


    public record Manifest
    {
        public string ClaimGenerator { get; set; } = string.Empty;

        public List<ClaimGeneratorInfo> ClaimGeneratorInfo { get; set; } = [];

        public string Format { get; set; } = "application/octet-stream";

        public string? Title { get; set; }

        public Thumbnail? Thumbnail { get; set; }

        public List<Ingredient> Ingredients { get; set; } = [];

        public List<ManifestAssertion> Assertions { get; set; } = [];
    }

    public record ManifestStore(string ActiveManifest, Dictionary<string, Manifest> Manifests)
    {
        public static ManifestStore FromJson(string json)
        {
            return Utils.Deserialize<ManifestStore>(json);
        }

        public string ToJson()
        {
            return Utils.Serialize(this);
        }
    }
}
