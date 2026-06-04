// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

public static class JsonExtensions
{
    public class AssertionTypeConverter : JsonConverter<Assertion>
    {
        public override Assertion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;
            string? label = root.GetProperty("label").GetString() ?? throw new JsonException("Missing label property");
            Type assertionType = GetAssertionTypeFromLabel(label);
            return doc.Deserialize(assertionType, options) as Assertion;
        }

        public override void Write(Utf8JsonWriter writer, Assertion value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }

        private static Type GetAssertionTypeFromLabel(string label)
        {
            return JsonExtensions.GetAssertionTypeFromLabel(label) ?? throw new JsonException();
        }
    }

    public static JsonSerializerOptions JsonSerializerOptions(bool indented = true) => new()
    {
        Converters =
        {
            new AssertionTypeConverter(),
            new JsonStringEnumConverter()
        },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = indented,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = "\n"
    };

    public static T FromJson<T>(this string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions()) ?? throw new JsonException("Failed to deserialize JSON.");
    }

    public static Type GetAssertionTypeFromLabel(string label)
    {
        return label switch
        {
            "c2pa.actions" => typeof(ActionsAssertion),
            "c2pa.actions.v2" => typeof(ActionsAssertionV2),
            "c2pa.thumbnail" => typeof(ThumbnailAssertion),
            "c2pa.training-mining" => typeof(TrainingAssertion),
            "c2pa.soft-binding" => typeof(SoftBindingAssertion),
            "c2pa.asset-type" => typeof(AssetTypeAssertion),
            "c2pa.asset-ref" => typeof(AssetReferenceAssertion),
            "c2pa.time-stamp" => typeof(TimeStampAssertion),
            "c2pa.certificate-status" => typeof(CertificateStatusAssertion),
            "c2pa.embedded-data" => typeof(EmbeddedDataAssertion),
            "c2pa.metadata" => typeof(MetadataAssertion),
            "stds.schema-org.CreativeWork" => typeof(CreativeWorkAssertion),
            string s when s.StartsWith("c2pa.thumbnail.claim") => typeof(ClaimThumbnailAssertion),
            string s when s.StartsWith("c2pa.thumbnail.ingredient") => typeof(IngredientThumbnailAssertion),
            _ => typeof(CustomAssertion),
        };
    }

    public static string ToJson<T>(this T obj, bool indented = true) => JsonSerializer.Serialize(obj, JsonSerializerOptions(indented));
}