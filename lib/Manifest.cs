using System.Text.Json.Serialization;

namespace ContentAuthenticity.Bindings
{

    // Example manifest JSON
    // {
    //     "claim_generator_info": [
    //         {
    //             "name": "{{claimName}}",
    //             "version": "0.0.1"
    //         }
    //     ],
    //     "format": "{{ext}}",
    //     "title": "{{manifestTitle}}",
    //     "ingredients": [],
    //     "assertions": [
    //         {   "label": "stds.schema-org.CreativeWork",
    //             "data": {
    //                 "@context": "http://schema.org/",
    //                 "@type": "CreativeWork",
    //                 "author": [
    //                     {   "@type": "Person",
    //                         "name": "{{authorName}}"
    //                     }
    //                 ]
    //             },
    //             "kind": "Json"
    //         }
    //     ]
    // }

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

        public List<Assertion> Assertions { get; set; } = [];
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
