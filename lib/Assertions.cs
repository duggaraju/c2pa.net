// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Dynamic;

namespace ContentAuthenticity;

public abstract record Assertion(string Label, object Data)
{
    public static T FromJson<T>(string json) where T : Assertion
    {
        return JsonExtensions.Deserialize<T>(json);
    }
}

public abstract record Assertion<T> : Assertion where T : notnull
{
    public new T Data { get; init; }

    public Assertion(string label, T data)
        : base(label, data)
    {
        Data = data;
    }
}

public record ThumbnailAssertionData(string Thumbnail, string InstanceID);

public record ThumbnailAssertion(ThumbnailAssertionData Data) : Assertion<ThumbnailAssertionData>("c2pa.thumbnail", Data);

public record ClaimThumbnailAssertion(ThumbnailAssertionData Data) : Assertion<ThumbnailAssertionData>("c2pa.thumbnail.claim", Data);

public record IngredientThumbnailAssertion(ThumbnailAssertionData Data) : Assertion<ThumbnailAssertionData>("c2pa.thumbnail.ingredient", Data);

public record ActionV1(
    string Action,
    [property: JsonPropertyName("softwareAgent")] string? SoftwareAgent = null,
    [property: JsonPropertyName("digitalSourceType")] string? DigitalSourceType = null,
    string? Changed = null,
    string? InstanceID = null,
    Dictionary<string, object>? Parameters = null);

public record ActionAssertionData(List<ActionV1> Actions);

public record ActionsAssertion(ActionAssertionData Data) : Assertion<ActionAssertionData>("c2pa.actions", Data);

public record ActionV2(
    string Action,
    [property: JsonPropertyName("softwareAgent")] ClaimGeneratorInfo? SoftwareAgent = null,
    string? Description = null,
    [property: JsonPropertyName("digitalSourceType")] string? DigitalSourceType = null,
    DateTimeOffset? When = null,
    Dictionary<string, object>? Changes = null,
    List<dynamic>? Actors = null,
    List<ActionV2>? Related = null,
    string? Reason = null,
    Dictionary<string, object>? Parameters = null);

public record Template(string DigitalSourceType, string Action);

public record ActionsAssertionV2Data(List<ActionV2> Actions, bool AllActionsIncluded = false, Template[]? Templates = null);

public record ActionsAssertionV2(ActionsAssertionV2Data Data) : Assertion<ActionsAssertionV2Data>("c2pa.actions.v2", Data);

public record CustomAssertion(string Label, dynamic Data) : Assertion<dynamic>(Label, (object)Data)
{
    public ExpandoObject GetDataAsExpandoObject()
    {
        return ConvertElementToExpandoObject(Data);
    }

    private static ExpandoObject ConvertElementToExpandoObject(JsonElement element)
    {
        dynamic dataResult = new ExpandoObject();

        foreach (JsonProperty property in element.EnumerateObject())
        {
            string propertyName = property.Name.Replace("@", "");
            ((IDictionary<string, object>)dataResult)[propertyName] = property.Value.ValueKind switch
            {
                JsonValueKind.Array => property.Value.EnumerateArray().Select(x => ConvertElementToExpandoObject(x)).ToArray(),
                JsonValueKind.Object => ConvertElementToExpandoObject(property.Value),
                JsonValueKind.Number => property.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => property.Value.ToString(),
            };
        }

        return dataResult;
    }
}


public record SchemaDotOrg(
    [property: JsonPropertyName("@context")] object? Context = null,
    [property: JsonPropertyName("@type")] string ObjectType = "default_type")
{
    [JsonExtensionData]
    public IDictionary<string, object>? Value { get; set; } = null;
}

public record CreativeWorkAssertionData(
    [property: JsonPropertyName("@context")] object? Context = null,
    [property: JsonPropertyName("@type")] string? Type = "")
{
    [JsonExtensionData]
    public IDictionary<string, object>? Value { get; set; } = null;
}

public record CreativeWorkAssertion(CreativeWorkAssertionData Data) : Assertion<CreativeWorkAssertionData>("stds.schema-org.CreativeWork", Data);

public enum Training
{
    Allowed,
    [JsonPropertyName("notAllowed")]
    NotAllowed,
    Constrained
}

public record TrainingAssertionData(Dictionary<string, Training> Entries);

public record TrainingAssertion(TrainingAssertionData Data) : Assertion<TrainingAssertionData>("c2pa.training-mining", Data);

public record EmbeddedDataAssertionData(string ContentType, byte[] Data);

public record EmbeddedDataAssertion(EmbeddedDataAssertionData Data) : Assertion<EmbeddedDataAssertionData>("c2pa.embedded-data", Data);

public record MetadataAssertionData(
    [property: JsonPropertyName("@context")] Dictionary<string, string> Context,
    [property: JsonExtensionData] Dictionary<string, object>? Value,
    string? CustomMetadataLabel);

public record MetadataAssertion(MetadataAssertionData Data) : Assertion<MetadataAssertionData>("c2pa.metadata", Data);

public record SoftBindingTimespan(nuint Start, nuint End);

public record SoftBindingScope(SoftBindingTimespan? Timespan = null, RegionOfInterestSetting? Region = null, string? Extent = null);

public record SoftBindingBlock(SoftBindingScope Scope, string Value);

public record SoftBindingAssertionData(
    IList<SoftBindingBlock> Blocks,
    IList<byte> Pad,
    string? Alg = null,
    string? AlgParams = null,
    IList<byte>? Pad2 = null,
    string? Url = null);

public record SoftBindingAssertion(SoftBindingAssertionData Data) : Assertion<SoftBindingAssertionData>("c2pa.soft-binding", Data);

public record CertificateStatusAssertionData(
    [property: JsonPropertyName("ocspVals")]
    IList<IList<byte>> OcspVals);

public record CertificateStatusAssertion(CertificateStatusAssertionData Data) : Assertion<CertificateStatusAssertionData>("c2pa.certificate-status", Data);

public record TimeStampAssertionData(Dictionary<string, byte[]> Timestamps);

public record TimeStampAssertion(TimeStampAssertionData Data) : Assertion<TimeStampAssertionData>("c2pa.time-stamp", Data);

public record ReferenceUri(string Uri);

public record ReferenceSetting(ReferenceUri Reference, string? Description = null);

public record AssetReferenceAssertionData(List<ReferenceSetting> References);

public record AssetReferenceAssertion(AssetReferenceAssertionData Data) : Assertion<AssetReferenceAssertionData>("c2pa.asset-ref", Data);

public record AssetTypeAssertionData(IList<AssetType> Types, AssertionMetadata? Metadata = null);

public record AssetTypeAssertion(AssetTypeAssertionData Data) : Assertion<AssetTypeAssertionData>("c2pa.asset-type", Data);