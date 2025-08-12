using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.ContentAuthenticity.Bindings
{
    public class AssertionTypeConverter : JsonConverter<Assertion>
    {
        public override Assertion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;
            string? label = root.GetProperty("label").GetString() ?? throw new JsonException("Missing label property");
            Type assertionType = GetAssertionTypeFromLabel(label);

            string rawJson = root.GetRawText();

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

    public static class Utils
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters =
            {
                new AssertionTypeConverter(),
                new JsonStringEnumConverter()
            },
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
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
                "c2pa.thumbnail" => typeof(ThumbnailAssertion),
                "c2pa.training-mining" => typeof(TrainingAssertion),
                string s when Regex.IsMatch(s, @"c2pa\.thumbnail\.claim.*") => typeof(ClaimThumbnailAssertion),
                string s when Regex.IsMatch(s, @"c2pa\.thumbnail\.ingredient.*") => typeof(IngredientThumbnailAssertion),
                "stds.schema-org.CreativeWork" => typeof(CreativeWorkAssertion),
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
                c2pa.C2paStringFree(ptr);

            return value;
        }

        public unsafe static string[] FromCStringArray(sbyte** ptr, ulong count)
        {
            if (count <= 0)
            {
                return [];
            }
            var values = new string[count];
            for (ulong i = 0; i < count; i++)
            {
                values[i] = FromCString(ptr[i], freeResource: false);
            }
            c2pa.C2paFreeStringArray(ptr, count);
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

}
