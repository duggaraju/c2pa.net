using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.ContentAuthenticity.Bindings
{
    public record Assertion(string Label, object Data)
    {
        public string ToJson() => Utils.Serialize(this);

        public static Assertion FromJson(string json) => Utils.Deserialize<Assertion>(json);
    }

    public record Assertion<T> : Assertion where T : notnull
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


    public record AuthorInfo([property: JsonPropertyName("@type")] string Type, string Name);

    public record CreativeWorkAssertionData(
        [property: JsonPropertyName("@context")] string? Context = "",
        [property: JsonPropertyName("@type")] string? Type = "",
        AuthorInfo[]? Authors = null);

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
}