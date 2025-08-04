using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.ContentAuthenticity.Bindings
{
    public enum AssertionKind
    {
        Cbor,
        Json
    }

    public record Assertion(string Label, object Data, AssertionKind Kind = AssertionKind.Json)
    {
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, Utils.JsonOptions);
        }

        public string DataAsJson() {
            return JsonSerializer.Serialize(Data, Utils.JsonOptions);
        }

        public static Assertion? FromJson(string json)
        {
            return JsonSerializer.Deserialize<Assertion>(json, Utils.JsonOptions);
        }
    }

    public record ThumbnailAssertionData(string Thumbnail, string InstanceID)
    {
    }

    public record ThumbnailAssertion(ThumbnailAssertionData data) : Assertion("c2pa.thumbnail", data)
    {
        new ThumbnailAssertionData Data { get; } = data;
    }

    public record ClaimThumbnailAssertion(ThumbnailAssertionData data) : Assertion("c2pa.thumbnail.claim", data)
    {
    }

    public record IngredientThumbnailAssertion(ThumbnailAssertionData data) : Assertion("c2pa.thumbnail.ingredient", data)
    {
    }

    public record ActionAssertion(ActionAssertionData data) : Assertion("c2pa.action", data)
    {
    }


    public record C2paAction(string Action, string? When = null, string? SoftwareAgent = null, string? Changed = null, string? InstanceID = null, List<dynamic>? Actors = null)
    {
    }

    public record ActionAssertionData
    {
        public List<C2paAction> Actions { get; set; } = new();
    }

    // Fix for CS1975: Cast 'data' to object in the base constructor initializer
    public record CustomAssertion(string Label, dynamic data) : Assertion(Label, (object)data)
    {
        public new dynamic Data { get; } = data;
        public ExpandoObject GetDataAsExpandoObject()
        {
            return ConvertElementToExpandoObject(Data);
        }

        private ExpandoObject ConvertElementToExpandoObject(JsonElement element)
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

    public record CreativeWorkAssertion(CreativeWorkAssertionData data) : Assertion("stds.schema-org.CreativeWork", data)
    {
    }

    public record CreativeWorkAssertionData(
        [property: JsonPropertyName("@context")] string? Context = "",
        [property: JsonPropertyName("@type")] string? Type = "",
        AuthorInfo[]? Authors = null)
    {
    }

    public record AuthorInfo(
        [property: JsonPropertyName("@type")] string Type, string Name)
    {
    }
}