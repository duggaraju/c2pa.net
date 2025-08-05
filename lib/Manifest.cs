using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.ContentAuthenticity.Bindings
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

    public class Thumbnail(string format = "", string identifier = "") : ResourceRef(format, identifier);

    public class ResourceRef(string format = "", string identifier = "")
    {
        public string Format { get; set; } = format;
        public string Identifier { get; set; } = identifier;
        public List<AssetType>? DataTypes { get; set; } = [];
        public string? Alg { get; set; } = null;
        public string? Hash { get; set; } = null;
    }

    public class AssetType(string assetType = "", string? version = null)
    {
        [JsonPropertyName("asset_type")]
        public string Type { get; set; } = assetType;
        public string? Version { get; set; } = version;
    }

    public class ResourceStore
    {
        public Dictionary<string, string> Resources { get; set; } = [];
        public string? Label { get; set; } = null;
    }

    // Ingredient
    public class HashedUri(string url, string alg, byte[] hash, byte[] salt)
    {
        public string Url { get; set; } = url;
        public string Alg { get; set; } = alg;
        public byte[] Hash { get; set; } = hash;
        public byte[] Salt { get; set; } = salt;
    }

    public record ValidationStatus(string Code = "", string Url = "", string Explanation = "");

    public enum Relationship {
        [JsonPropertyName("parentOf")]
        parentOf,
        [JsonPropertyName("componentOf")]
        componentOf,
        [JsonPropertyName("inputTo")]
        inputTo,
    }

    // Manifest
    public record ClaimGeneratorInfo(string Name = "", string Version = "");

    public class Ingredient(string title = "", string format = "", Relationship relationship = Relationship.parentOf) {
        public string Title { get; set; } = title;
        public string Format { get; set; } = format;
        public Relationship Relationship { get; set; } = relationship;
        public string? DocumentID { get; set; } = null;
        public string? InstanceID { get; set; } = null;
        public HashedUri? C2paManifest { get; set; } = null;
        public HashedUri? HashedManifestUri { get; set; } = null;
        public List<ValidationStatus>? ValidationStatus { get; set; } = null;
        public Thumbnail? Thumbnail { get; set; } = null;
        public HashedUri? Data { get; set; } = null;
        public string? Description { get; set; } = null;
        public string? InformationalUri { get; set; } = null;
    }


    public record Manifest
    {
        public string ClaimGenerator { get; set; } = string.Empty;

        public List<ClaimGeneratorInfo> ClaimGeneratorInfo { get; set; } = [];
        
        public string Format { get; set; } = "application/octet-stream";
        
        public string? Title { get; set; } = null;

        public Thumbnail? Thumbnail { get; set; } = null;

        public List<Ingredient> Ingredients { get; set; } = [];

        public List<Assertion> Assertions { get; set; } = [];

        public string GetManifestJson()
        {
            return JsonSerializer.Serialize(this, Utils.JsonOptions);
        }
    }

    public record ManifestStore(string ActiveManifest, Dictionary<string, Manifest> Manifests)
    {
        public static ManifestStore FromJson(string json)
        {
            return JsonSerializer.Deserialize<ManifestStore>(json, Utils.JsonOptions) ?? throw new JsonException("Manifest JSON is Invalid");
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, Utils.JsonOptions);
        }
    }
}
