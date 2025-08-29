using System.Runtime.Serialization;

namespace Microsoft.ContentAuthenticity;

// See https://opensource.contentauthenticity.org/docs/manifest/json-ref/reader for the schema.

public enum ValidationState
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

public enum RangeType
{
    Spatial,
    Temporal,
    Frame,
    Textual,
    Identified,
}

public enum ShapeType
{
    Rectangle,
    Circle,
    Polygon,
}

public enum UnitType
{
    Pixel,
    Percent,
}

public enum TimeType
{
    Ntp
}

public enum Role
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
public record HashedUri(string Url, SigningAlg Alg, byte[] Hash, byte[] Salt);

public record ValidationStatus(string Code, string? Url, string? Explanation, bool? Success = false, string? IngredientUri = null);

public record ValidationResults(
    [property: JsonPropertyName("activeManifest")] StatusCodes? ActiveManifest,
    [property: JsonPropertyName("ingredientDeltas")] IList<IngredientDeltaValidationResult>? IngredientDeltas);

public record StatusCodes(
    IList<ValidationStatus> Success,
    IList<ValidationStatus> Informational,
    IList<ValidationStatus> Failure);

public record IngredientDeltaValidationResult(
    [property: JsonPropertyName("ingredientAssertionURI")] string IngredientAssertionURI,
    [property: JsonPropertyName("validationDeltas")] StatusCodes ValidationDeltas);

public record AssertionMetadata(
    IList<ReviewRating>? Reviews = null,
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
    Role? Role = null,
    string? Description = null,
    AssertionMetadata? Metadata = null);

public record Range(
    RangeType RangeType = RangeType.Temporal,
    ShapeSetting? Shape = null,
    TimeSetting? Time = null,
    FrameSetting? Frame = null,
    TextSetting? Text = null,
    ItemSettings? Item = null);

public record ShapeSetting(
    ShapeType ShapeType,
    UnitType UnitType,
    Coordinate Origin,
    double? Width = null,
    double? Height = null,
    bool? Inside = null,
    List<Coordinate>? Vertices = null);

public record Coordinate(double X, double Y);

public record TimeSetting(TimeType TimeType = TimeType.Ntp, int? Start = null, int? End = null);

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
public record ClaimGeneratorInfo(string Name, string? Version = null, string? OperatingSystem = null);

public record Ingredient(Relationship Relationship = Relationship.ComponentOf)
{
    public string? Title { get; set; }

    public string? Format { get; set; }

    public string? ActiveManifest { get; set; }

    public string? Label { get; set; }

    public string? DocumentId { get; set; }

    public string? InstanceId { get; set; }

    public HashedUri? C2paManifest { get; set; }

    public HashedUri? HashedManifestUri { get; set; }

    public List<ValidationStatus>? ValidationStatus { get; set; }

    public Thumbnail? Thumbnail { get; set; }

    public HashedUri? Data { get; set; }

    public string? Description { get; set; }

    public string? InformationalUri { get; set; }
}

public record SignatureInfo(
    SigningAlg? Alg,
    string? Issuer,
    string? CommonName,
    DateTimeOffset? Time,
    string? CertSerialNumber,
    bool? RevocationStatus);

public record Manifest(
    string ClaimGenerator,
    string Format,
    string? Title,
    string? InstanceId,
    string? Label)
{
    public List<ClaimGeneratorInfo> ClaimGeneratorInfo { get; set; } = [];

    public Thumbnail? Thumbnail { get; set; }

    public List<Ingredient> Ingredients { get; set; } = [];

    public List<ManifestAssertion> Assertions { get; set; } = [];

    public SignatureInfo? SignatureInfo { get; set; } = null;
}

public record ManifestStore(
    string ActiveManifest,
    Dictionary<string, Manifest> Manifests,
    IList<ValidationStatus> ValidationStatus,
    ValidationResults? ValidationResults,
    ValidationState ValidationState)
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