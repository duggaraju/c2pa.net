
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace ContentAuthenticity;
    
// See https://opensource.contentauthenticity.org/docs/manifest/json-ref/reader for the schema.

public enum ValidationStateEnum
{
    Valid,
    Invalid,
    Trusted
}

public enum AssertionKind
{
    Cbor,
    Json,
    Binary,
    Uri
}

public enum RangeTypeEnum
{
    Spatial,
    Temporal,
    Frame,
    Textual,
    Identified,
}

public enum ShapeTypeEnum
{
    Rectangle,
    Circle,
    Polygon,
}

public enum UnitTypeEnum
{
    Pixel,
    Percent,
}

public enum TimeTypeEnum
{
    Npt
}

public enum RoleEnum
{ 
    AreaOfInterest,
    Cropped,
    Edited,
    Placed,
    Redacted,
    SubjectArea,
    Deleted,
    Styled,
    Watermarked,
}


public record ManifestAssertion(string Label, object Data, AssertionKind Kind, int? Instance = null): Assertion(Label, Data);


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

public record ValidationResult()
{
    [JsonPropertyName("activeManifest")]
    public StatusCodes? ActiveManifest { get; set; }

    [JsonPropertyName("ingredientDeltas")]
    public List<IngredientDeltasValidationResult>? IngredientDeltas { get; set; }
}


public record StatusCodes(List<ValidationStatus> Success, List<ValidationStatus> Informational, List<ValidationStatus> Failure);

public record IngredientDeltasValidationResult(string IngredientAssertionURI, StatusCodes ValidationDeltas);

public record AssertionMetadata(
    List<ReviewRating>? Reviews = null,
    DateTimeOffset? DateTime = null,
    HashedUri? Reference = null,
    DataSourceSetting? DataSource = null,
    RegionOfInterestSetting? RegionOfInterest = null);

public record ReviewRating(string Explanation, byte Value, string? Code = null);

public record DataSourceSetting(string SourceType, string? Details, List<Actor> Actors);

public record Actor(string? Identifier = null, List<HashedUri>? Credentials = null);

public record RegionOfInterestSetting(
    List<Range> Region,
    string? Name = null,
    string? Identifier = null,
    string? RegionType = null,
    RoleEnum? Role = null,
    string? Description = null,
    AssertionMetadata? Metadata = null);

public record Range(
    RangeTypeEnum RangeType = RangeTypeEnum.Temporal,
    ShapeSetting? Shape = null,
    TimeSetting? Time = null,
    FrameSetting? Frame = null,
    TextSetting? Text = null,
    ItemSettings? Item = null);

public record ShapeSetting(
    ShapeTypeEnum ShapeType,
    UnitTypeEnum UnitType,
    Coordinate Origin,
    double? Width = null,
    double? Height = null,
    bool? Inside = null,
    List<Coordinate>? Vertices = null);

public record Coordinate(double X, double Y);

public record TimeSetting(TimeTypeEnum TimeType = TimeTypeEnum.Npt, int? Start = null, int? End = null);

public record FrameSetting(int? Start = null, int? End = null);

public record TextSetting(List<TextSelectorRange>? selectors = null);

public record TextSelectorRange(TextSelector selector, TextSelector? End = null);

public record TextSelector(string Fragment, int? Start = null, int? End = null);

public record ItemSettings(string Identifier, string Value);

public enum Relationship
{
    [EnumMember(Value = "parentOf")]
    ParentOf,

    [EnumMember(Value = "componentOf")]
    ComponentOf,

    [EnumMember(Value = "inputTo")]
    InputTo,
}

// Manifest
public record ClaimGeneratorInfo(string Name = "", string Version = "", string? OperatingSystem = null);

public record Ingredient(string? Title = null, string? Format = null, Relationship Relationship = Relationship.ParentOf)
{
    public string? DocumentID { get; set; }

    public string? InstanceID { get; set; }

    public string? Provenance { get; set; }
    
    public Thumbnail? Thumbnail { get; set; }

    public string? Hash { get; set; }

    public string? ActiveManifest { get; set; } // Just the Label

    public List<ValidationStatus>? ValidationStatus { get; set; }

    public ValidationResult? ValidationResults { get; set; }

    public ResourceRef? Data { get; set; }

    public string? Description { get; set; }

    public string? InformationalURI { get; set; }

    public AssertionMetadata? Metadata { get; set; }

    public List<AssetType>? DataTypes { get; set; }

    public ResourceRef? ManifestData { get; set; }
    
    public string? Label { get; set; }
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

public record ManifestStore(Dictionary<string, Manifest> Manifests)
{
    public string? ActiveManifest { get; set; }

    public List<ValidationStatus>? ValidationStatus { get; set; }

    public ValidationResult? ValidationResults { get; set; }

    public ValidationStateEnum? ValidationState { get; set; }

    public static ManifestStore FromJson(string json)
    {
        return Utils.Deserialize<ManifestStore>(json);
    }

    public string ToJson()
    {
        return Utils.Serialize(this);
    }
}
