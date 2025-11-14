// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

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
public record ManifestAssertion(
    string Label,
    dynamic Data,
    AssertionKind? Kind = null,
    int? Instance = null,
    bool? created = null);


public record Thumbnail(string Format, string Identifier) : ResourceRef(Format, Identifier);

public record ResourceRef(string Format, string Identifier)
{
    public List<AssetType>? DataTypes { get; set; } = null;

    public string? Alg { get; set; } = null;

    public string? Hash { get; set; } = null;
}


public record AssetType(
    [property: JsonPropertyName("asset_type")] string Type,
    string? Version = null);

// Ingredient
public record HashedUri(string Url, SigningAlg Alg, byte[] Hash, byte[] Salt);

public record ValidationStatus(string Code, string? Url, string? Explanation, bool? Success = null, string? IngredientUri = null);

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

public record TextSetting(List<TextSelectorRange>? Selectors = null);

public record TextSelectorRange(TextSelector Selector, TextSelector? End = null);

public record TextSelector(string Fragment, int? Start = null, int? End = null);

public record ItemSettings(string Identifier, string Value);

public enum Relationship
{
    [JsonStringEnumMemberName("parentOf")]
    ParentOf,

    [JsonStringEnumMemberName("componentOf")]
    ComponentOf,

    [JsonStringEnumMemberName("inputTo")]
    InputTo,
}

// Manifest
public record ClaimGeneratorInfo(string Name, string? Version = null, string? OperatingSystem = null)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; set; }
}

public record Ingredient(
    string? Title = null,
    string? Format = null,
    string? DocumentId = null,
    string? InstanceId = null,
    Thumbnail? Thumbnail = null,
    Relationship Relationship = Relationship.ComponentOf)
{
    public string? ActiveManifest { get; set; }

    public HashedUri? C2paManifest { get; set; }

    public HashedUri? HashedManifestUri { get; set; }

    public List<ValidationStatus>? ValidationStatus { get; set; }

    public object? Metadata { get; set; }

    public Dictionary<string, object>? ManifestData { get; set; }

    public string? Label { get; set; }

    public HashedUri? Data { get; set; }

    public string? Description { get; set; }

    public string? InformationalUri { get; set; }
}

public record SignatureInfo(
    SigningAlg? Alg,
    string? Issuer,
    string? CommonName,
    string? CertSerialNumber,
    DateTimeOffset? Time,
    bool? RevocationStatus);

public record Manifest(
    string? ClaimGenerator = null,
    IList<ClaimGeneratorInfo>? ClaimGeneratorInfo = null,
    string? Title = null,
    string Format = "application/octet-stream",
    string? InstanceId = null,
    Thumbnail? Thumbnail = null,
    IList<Ingredient>? Ingredients = null,
    IList<dynamic>? Credentials = null,
    IList<ManifestAssertion>? Assertions = null,
    SignatureInfo? SignatureInfo = null,
    string? Label = null);

public record ManifestStore(
    string ActiveManifest,
    Dictionary<string, Manifest> Manifests,
    IList<ValidationStatus> ValidationStatus,
    ValidationResults? ValidationResults,
    ValidationState ValidationState)
{
    public static ManifestStore FromJson(string json)
    {
        return json.Deserialize<ManifestStore>();
    }
}