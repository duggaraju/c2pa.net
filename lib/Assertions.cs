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

public class ClaimThumbnailAssertion : Assertion<Schema.ThumbnailAssertion>
{
    [SetsRequiredMembers]
    public ClaimThumbnailAssertion(Schema.ThumbnailAssertion data) : base("c2pa.thumbnail.claim", data)
    {
    }
}

public class IngredientThumbnailAssertion : Assertion<Schema.ThumbnailAssertion>
{
    [SetsRequiredMembers]
    public IngredientThumbnailAssertion(Schema.ThumbnailAssertion data) : base("c2pa.thumbnail.ingredient", data)
    {
    }
}

public class ActionsAssertion : Assertion<Schema.ActionsAssertionV1>
{
    [SetsRequiredMembers]
    public ActionsAssertion(Schema.ActionsAssertionV1 data) : base("c2pa.actions", data)
    {
    }
}

public class ActionsAssertionV2 : Assertion<Schema.ActionsAssertionV2>
{
    [SetsRequiredMembers]
    public ActionsAssertionV2(Schema.ActionsAssertionV2 data) : base("c2pa.actions.v2", data)
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

public record EmbeddedDataAssertionData([property: JsonExtensionData] IDictionary<string, object>? Parameters = null);

public class EmbeddedDataAssertion : Assertion<EmbeddedDataAssertionData>
{
    [SetsRequiredMembers]
    public EmbeddedDataAssertion(EmbeddedDataAssertionData data) : base("c2pa.embedded-data", data)
    {
    }
}

public record MetadataAssertionData(
    [property: JsonPropertyName("@context")] Dictionary<string, string> Context,
    [property: JsonExtensionData] Dictionary<string, object>? Properties = null
);

public class MetadataAssertion : Assertion<MetadataAssertionData>
{
    [SetsRequiredMembers]
    public MetadataAssertion(MetadataAssertionData data) : base("c2pa.metadata", data)
    {
    }
}

public class SoftBindingAssertion : Assertion<Schema.SoftBindingAssertion>
{
    [SetsRequiredMembers]
    public SoftBindingAssertion(Schema.SoftBindingAssertion data) : base("c2pa.soft-binding", data)
    {
    }
}

public class CertificateStatusAssertion : Assertion<Schema.CertificateStatusAssertion>
{
    [SetsRequiredMembers]
    public CertificateStatusAssertion(Schema.CertificateStatusAssertion data) : base("c2pa.certificate-status", data)
    {
    }
}

public class TimeStampAssertion : Assertion<Dictionary<string, string>>
{
    [SetsRequiredMembers]
    public TimeStampAssertion(Dictionary<string, string> data) : base("c2pa.time-stamp", data)
    {
    }

    [SetsRequiredMembers]
    public TimeStampAssertion(Dictionary<string, byte[]> data) : base("c2pa.time-stamp", data.ToDictionary(kvp => kvp.Key, kvp => Convert.ToBase64String(kvp.Value)))
    {
    }
}

public class AssetReferenceAssertion : Assertion<Schema.AssetReferenceAssertion>
{
    [SetsRequiredMembers]
    public AssetReferenceAssertion(Schema.AssetReferenceAssertion data) : base("c2pa.asset-ref", data)
    {
    }
}

public class AssetTypeAssertion : Assertion<Schema.AssetTypeAssertion>
{
    [SetsRequiredMembers]
    public AssetTypeAssertion(Schema.AssetTypeAssertion data) : base("c2pa.asset-type", data)
    {
    }
}