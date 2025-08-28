using System.Text.Encodings.Web;

namespace ContentAuthenticity;

public class AssertionTypeConverter : JsonConverter<Assertion>
{
    public override Assertion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;
        string? label = root.GetProperty("label").GetString() ?? throw new JsonException("Missing label property");
        Type assertionType = GetAssertionTypeFromLabel(label);

        return JsonSerializer.Deserialize(root.GetRawText(), assertionType, options) as Assertion;
    }

    public override void Write(Utf8JsonWriter writer, Assertion value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    private static Type GetAssertionTypeFromLabel(string label)
    {
        return Utils.GetAssertionTypeFromLabel(label) ?? throw new JsonException();
    }
}

public class MetadataAssertionConverter : JsonConverter<MetadataAssertionData>
{
    public override MetadataAssertionData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Going from Json to Builder is currently not supported
        throw new NotImplementedException("Deserialization not implemented for this example.");
    }

    public override void Write(Utf8JsonWriter writer, MetadataAssertionData assertionData, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        writer.WritePropertyName("@context");
        JsonSerializer.Serialize(writer, assertionData.Context, options); // Use options for proper serialization of value types

        FlattenDictionary(writer, assertionData.Value, "", options);
        
        writer.WriteEndObject();
    }

    private void FlattenDictionary(Utf8JsonWriter writer, Dictionary<string, object> dictionary, string prefix, JsonSerializerOptions options)
    {
        foreach (var entry in dictionary)
        {
            var key = string.IsNullOrEmpty(prefix) ? entry.Key : $"{prefix}.{entry.Key}";

            if (entry.Value is Dictionary<string, object> nestedDictionary)
            {
                FlattenDictionary(writer, nestedDictionary, key, options);
            }
            else
            {
                // Write simple values directly
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, entry.Value, options); // Use options for proper serialization of value types
            }
        }
    }
}

public class ByteBufConverter : JsonConverter<ByteBuf>
{
    public override ByteBuf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Going from Json to Builder is currently not supported
        throw new NotImplementedException("Deserialization not implemented for this example.");
    }

    public override void Write(Utf8JsonWriter writer, ByteBuf assertionData, JsonSerializerOptions options)
    {
        WriteByteBuf(writer, assertionData);
    }

    public static void WriteByteBuf(Utf8JsonWriter writer, ByteBuf assertionData)
    {
        writer.WriteStartArray();

        foreach (var bufEntry in assertionData.Data)
        {
            writer.WriteNumberValue(bufEntry);
        }

        writer.WriteEndArray();
    }
}

public class ByteBufListConverter : JsonConverter<List<ByteBuf>>
{
    public override List<ByteBuf> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Going from Json to Builder is currently not supported
        throw new NotImplementedException("Deserialization not implemented for this example.");
    }

    public override void Write(Utf8JsonWriter writer, List<ByteBuf> assertionData, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var buf in assertionData)
        {
            ByteBufConverter.WriteByteBuf(writer, buf);
        }
        writer.WriteEndArray();
    }
}

public class TimeStampAssertionConverter : JsonConverter<TimeStampAssertionData>
{
    public override TimeStampAssertionData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Going from Json to Builder is currently not supported
        throw new NotImplementedException("Deserialization not implemented for this example.");
    }

    public override void Write(Utf8JsonWriter writer, TimeStampAssertionData assertionData, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        FlattenDictionary(writer, assertionData.Timestamps, "", options);

        writer.WriteEndObject();
    }

    private void FlattenDictionary(Utf8JsonWriter writer, Dictionary<string, ByteBuf> dictionary, string prefix, JsonSerializerOptions options)
    {
        foreach (var entry in dictionary)
        {
            var key = string.IsNullOrEmpty(prefix) ? entry.Key : $"{prefix}.{entry.Key}";

            // Write simple values directly
            writer.WritePropertyName(key);
            ByteBufConverter.WriteByteBuf(writer, entry.Value);
        }
    }
}

public static class Utils
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters =
        {
            new ByteBufConverter(),
            new ByteBufListConverter(),
            new TimeStampAssertionConverter(),
            new MetadataAssertionConverter(),
            new JsonStringEnumMemberConverter(),
            new JsonStringEnumConverter(),
            new AssertionTypeConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new JsonException("Failed to deserialize JSON.");
    }

    public static string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    public static Type GetAssertionTypeFromLabel(string label)
    {
        return label switch
        {
            "c2pa.actions" => typeof(ActionsAssertion),
            "c2pa.actions.v2" => typeof(ActionsAssertionV2),
            "c2pa.training-mining" => typeof(TrainingAssertion),
            "stds.schema-org.CreativeWork" => typeof(CreativeWorkAssertion),
            "c2pa.embedded-data" => typeof(EmbeddedDataAssertion),
            "c2pa.metadata" => typeof(MetadataAssertion),
            "c2pa.soft-binding" => typeof(SoftBindingAssertion),
            "c2pa.certificate-status" => typeof(CertificateStatusAssertion),
            "c2pa.time-stamp" => typeof(TimeStampAssertion),
            "c2pa.asset-ref" => typeof(AssetReferenceAssertion),
            "c2pa.asset-type" => typeof(AssetTypeAssertion),
            _ => typeof(CustomAssertion),
        };
    }

    public unsafe static string FromCString(sbyte* ptr, bool freeResource = true)
    {
        if (ptr == null)
        {
            return string.Empty;
        }
        var value = Marshal.PtrToStringUTF8((nint)ptr)!;
        if (freeResource)
            C2paBindings.string_free(ptr);

        return value;
    }

    public unsafe static string[] FromCStringArray(sbyte** ptr, nuint count)
    {
        if (count <= 0)
        {
            return [];
        }
        var values = new string[count];
        for (nuint i = 0; i < count; i++)
        {
            values[i] = FromCString(ptr[i], freeResource: false);
        }
        C2paBindings.free_string_array(ptr, count);
        return values;
    }

    public static string GetMimeTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".tiff" or ".tif" => "image/tiff",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}