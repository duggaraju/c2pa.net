// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Dynamic;

namespace ContentAuthenticity;

public abstract class Assertion : Schema.Builder.AssertionDefinition
{
    [SetsRequiredMembers]
    protected Assertion(string label, object data)
    {
        Label = label;
        base.Data = JsonSerializer.SerializeToElement(data);
    }

    public static T FromJson<T>(string json) where T : Assertion
    {
        return JsonExtensions.FromJson<T>(json);
    }
}

public abstract class Assertion<T> : Assertion where T : notnull
{
    public new T Data { get; init; }

    [SetsRequiredMembers]
    public Assertion(string label, T data)
        : base(label, data)
    {
        Data = data;
    }
}

public record ThumbnailAssertionData(string Thumbnail, string InstanceID);

public class ThumbnailAssertion : Assertion<ThumbnailAssertionData>
{
    [SetsRequiredMembers]
    public ThumbnailAssertion(ThumbnailAssertionData data) : base("c2pa.thumbnail", data)
    {
    }
}

public class ClaimThumbnailAssertion : Assertion<ThumbnailAssertionData>
{
    [SetsRequiredMembers]
    public ClaimThumbnailAssertion(ThumbnailAssertionData data) : base("c2pa.thumbnail.claim", data)
    {
    }
}

public class IngredientThumbnailAssertion : Assertion<ThumbnailAssertionData>
{
    [SetsRequiredMembers]
    public IngredientThumbnailAssertion(ThumbnailAssertionData data) : base("c2pa.thumbnail.ingredient", data)
    {
    }
}

public record ActionV1(
    string Action,
    [property: JsonPropertyName("softwareAgent")] string? SoftwareAgent = null,
    [property: JsonPropertyName("digitalSourceType")] string? DigitalSourceType = null,
    string? Changed = null,
    string? InstanceID = null,
    Dictionary<string, object>? Parameters = null);

public record ActionAssertionData(List<ActionV1> Actions);

public class ActionsAssertion : Assertion<ActionAssertionData>
{
    [SetsRequiredMembers]
    public ActionsAssertion(ActionAssertionData data) : base("c2pa.actions", data)
    {
    }
}

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

public class ActionsAssertionV2 : Assertion<ActionsAssertionV2Data>
{
    [SetsRequiredMembers]
    public ActionsAssertionV2(ActionsAssertionV2Data data) : base("c2pa.actions.v2", data)
    {
    }
}

public class CustomAssertion : Assertion<dynamic>
{
    [SetsRequiredMembers]
    public CustomAssertion(string label, dynamic data) : base(label, (object)data)
    {
    }

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

public class CreativeWorkAssertion : Assertion<CreativeWorkAssertionData>
{
    [SetsRequiredMembers]
    public CreativeWorkAssertion(CreativeWorkAssertionData data) : base("stds.schema-org.CreativeWork", data)
    {
    }
}

public enum Training
{
    Allowed,
    [JsonPropertyName("notAllowed")]
    NotAllowed,
    Constrained
}

public record TrainingAssertionData(Dictionary<string, Training> Entries);

public class TrainingAssertion : Assertion<TrainingAssertionData>
{
    [SetsRequiredMembers]
    public TrainingAssertion(TrainingAssertionData data) : base("c2pa.training-mining", data)
    {
    }
}

public record EmbeddedDataAssertionData(string ContentType, byte[] Data);

public class EmbeddedDataAssertion : Assertion<EmbeddedDataAssertionData>
{
    [SetsRequiredMembers]
    public EmbeddedDataAssertion(EmbeddedDataAssertionData data) : base("c2pa.embedded-data", data)
    {
    }
}

public record MetadataAssertionData(
    [property: JsonPropertyName("@context")] Dictionary<string, string> Context,
    [property: JsonExtensionData] Dictionary<string, object>? Value,
    string? CustomMetadataLabel);

public class MetadataAssertion : Assertion<MetadataAssertionData>
{
    [SetsRequiredMembers]
    public MetadataAssertion(MetadataAssertionData data) : base("c2pa.metadata", data)
    {
    }
}

public record SoftBindingTimespan(nuint Start, nuint End);

public record SoftBindingScope(SoftBindingTimespan? Timespan = null, Schema.Builder.RegionOfInterest? Region = null, string? Extent = null);

public record SoftBindingBlock(SoftBindingScope Scope, string Value);

public record SoftBindingAssertionData(
    IList<SoftBindingBlock> Blocks,
    IList<byte> Pad,
    string? Alg = null,
    string? AlgParams = null,
    IList<byte>? Pad2 = null,
    string? Url = null);

public class SoftBindingAssertion : Assertion<SoftBindingAssertionData>
{
    [SetsRequiredMembers]
    public SoftBindingAssertion(SoftBindingAssertionData data) : base("c2pa.soft-binding", data)
    {
    }
}

public record CertificateStatusAssertionData(
    [property: JsonPropertyName("ocspVals")]
    IList<IList<byte>> OcspVals);

public class CertificateStatusAssertion : Assertion<CertificateStatusAssertionData>
{
    [SetsRequiredMembers]
    public CertificateStatusAssertion(CertificateStatusAssertionData data) : base("c2pa.certificate-status", data)
    {
    }
}

public record TimeStampAssertionData(Dictionary<string, byte[]> Timestamps);

public class TimeStampAssertion : Assertion<TimeStampAssertionData>
{
    [SetsRequiredMembers]
    public TimeStampAssertion(TimeStampAssertionData data) : base("c2pa.time-stamp", data)
    {
    }
}

public record ReferenceUri(string Uri);

public record ReferenceSetting(ReferenceUri Reference, string? Description = null);

public record AssetReferenceAssertionData(List<ReferenceSetting> References);

public class AssetReferenceAssertion : Assertion<AssetReferenceAssertionData>
{
    [SetsRequiredMembers]
    public AssetReferenceAssertion(AssetReferenceAssertionData data) : base("c2pa.asset-ref", data)
    {
    }
}

public record AssetTypeAssertionData(IList<Schema.Builder.AssetType> Types, Schema.Builder.AssertionMetadata? Metadata = null);

public class AssetTypeAssertion : Assertion<AssetTypeAssertionData>
{
    [SetsRequiredMembers]
    public AssetTypeAssertion(AssetTypeAssertionData data) : base("c2pa.asset-type", data)
    {
    }
}