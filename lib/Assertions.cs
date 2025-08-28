using System.Dynamic;

// NOTE [8/28/25]: Cloud Data, Font Information, and Depthmap Assertion Authoring is not curently supported in Rust
namespace ContentAuthenticity;

public enum DigitalSourceType
{
    Empty,
    TrainedAlgorithmicData,
    DigitalCapture,
    ComputationalCapture,
    NegativeFilm,
    PositiveFilm,
    Print,
    MinorHumanEdits,
    HumanEdits,
    CompositeWithTrainedAlgorithmicMedia,
    AlgorithmicallyEnhanced,
    SoftwareImage,
    DigitalArt,
    DigitalCreation,
    DataDrivenMedia,
    TrainedAlgorithmicMedia,
    AlgorithmicMedia,
    ScreenCapture,
    VirtualRecording,
    Composite,
    CompositeCapture,
    CompositeSynthetic,
    Other
}

public enum AssetTypeEnum
{
    Classifier,
    Cluster,
    Dataset,
    DatasetJax,
    DatasetKeras,
    DatasetMlNet,
    DatasetMxNet,
    DatasetOnnx,
    DatasetOpenVino,
    DatasetPyTorch,
    DatasetTensoflow,
    FormatNumpy,
    FormatProtoBuf,
    FormatPickle,
    Generator,
    GeneratorPrompt,
    GeneratorSeed,
    Model,
    ModelJax,
    ModelKeras,
    ModelMlNet,
    ModelMxNet,
    ModelOnnx,
    ModelOpenVino,
    ModelOpenVinoParameter,
    ModelOpenVinoTopology,
    ModelPyTorch,
    ModelTensorflow,
    Regressor,
    TensorflowHubModule,
    TensorflowSaveModel,
    Other,
}

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
    [property: JsonPropertyName("softwareAgent")] string? SoftwareAgent = null,
    DateTimeOffset? When = null,
    [property: JsonPropertyName("softwareAgentIndex")] UIntPtr? SoftwareAgentIndex = null,
    string? Changed = null,
    List<RegionOfInterestSetting>? Changes = null,
    Dictionary<string, object>? Parameters = null,
    List<Actor>? Actors = null,
    [property: JsonPropertyName("digitalSourceType")] DigitalSourceType? SourceType = null,
    List<ActionV2>? Related = null,
    string? Reason = null,
    string? Description = null)
{
    public ActionV2(
        string Action,
        ClaimGeneratorInfo SoftwareAgent,
        DateTimeOffset? When = null,
        UIntPtr? SoftwareAgentIndex = null,
        string? Changed = null,
        List<RegionOfInterestSetting>? Changes = null,
        Dictionary<string, object>? Parameters = null,
        List<Actor>? Actors = null,
        DigitalSourceType? SourceType = null,
        List<ActionV2>? Related = null,
        string? Reason = null,
        string? Description = null) : this(Action, Utils.Serialize<ClaimGeneratorInfo>(SoftwareAgent), When, SoftwareAgentIndex, Changed, Changes, Parameters, Actors, SourceType, Related, Reason, Description)
    {
    }
}

public record ActionTemplate(
    string Action,
    [property: JsonPropertyName("softwareAgents")] ClaimGeneratorInfo? SoftwareAgents = null,
    [property: JsonPropertyName("softwareAgentIndex")] UIntPtr? SoftwareAgentIndex = null,
    [property: JsonPropertyName("digitalSourceType")] DigitalSourceType? SourceType = null,
    ResourceRef? Icon = null,
    string? Description = null,
    [property: JsonPropertyName("templateParameters")] Dictionary<string, object>? TemplateParameters = null);

public record ActionsAssertionV2Data(
    List<ActionV2> Actions,
    [property: JsonPropertyName("softwareAgent")] ClaimGeneratorInfo? SoftwareAgent = null,
    [property: JsonPropertyName("allActionsIncluded")] bool? AllActionsIncluded = true,
    List<ActionTemplate>? Templates = null,
    AssertionMetadata? Metadata = null);

public record ActionsAssertionV2(ActionsAssertionV2Data Data) : Assertion<ActionsAssertionV2Data>("c2pa.actions.v2", Data);

public record EmbeddedDataAssertionData(string ContentType, byte[] Data);

public record EmbeddedDataAssertion(EmbeddedDataAssertionData Data) : Assertion<EmbeddedDataAssertionData>("c2pa.embedded-data", Data);

[JsonConverter(typeof(MetadataAssertionConverter))]
public record MetadataAssertionData(
    [property: JsonPropertyName("@context")] Dictionary<string, string> Context,
    Dictionary<string, object> Value);

public record MetadataAssertion(MetadataAssertionData Data) : Assertion<MetadataAssertionData>("c2pa.metadata", Data);

public record SoftBindingTimespan(UIntPtr Start, UIntPtr End);

public record SoftBindingScope(SoftBindingTimespan? Timespan = null, RegionOfInterestSetting? Region = null, string? Extent = null);

public record SoftBindingBlock(SoftBindingScope Scope, string value);

public record ByteBuf(List<byte> Data);

public record SoftBindingAssertionData(
    List<SoftBindingBlock> Blocks,
    List<byte> Pad,
    string? Alg = null,
    string? AlgParams = null,
    [property: JsonConverter(typeof(ByteBufConverter))]ByteBuf? Pad2 = null,
    string? Url = null)
{
    public SoftBindingAssertionData(List<SoftBindingBlock> Blocks, string? Alg = null, string? AlgParams = null, ByteBuf? Pad2 = null, string? Url = null)
        : this(Blocks, new List<byte>(), Alg, AlgParams, Pad2, Url) { }
}

public record SoftBindingAssertion(SoftBindingAssertionData Data) : Assertion<SoftBindingAssertionData>("c2pa.soft-binding", Data);

public record CertificateStatusAssertionData([property: JsonPropertyName("ocspVals")][property: JsonConverter(typeof(ByteBufListConverter))] List<ByteBuf> OcspVals);

public record CertificateStatusAssertion(CertificateStatusAssertionData Data) : Assertion<CertificateStatusAssertionData>("c2pa.certificate-status", Data);

[JsonConverter(typeof(TimeStampAssertionConverter))]
public record TimeStampAssertionData(Dictionary<string, ByteBuf> Timestamps);

public record TimeStampAssertion(TimeStampAssertionData Data) : Assertion<TimeStampAssertionData>("c2pa.time-stamp", Data);

public record ReferenceUri(string Uri);

public record ReferenceSetting(ReferenceUri Reference, string? Description = null);

public record AssetReferenceAssertionData(List<ReferenceSetting> References);

public record AssetReferenceAssertion(AssetReferenceAssertionData Data) : Assertion<AssetReferenceAssertionData>("c2pa.asset-ref", Data);

public record AssetTypeAssertionData(List<AssetTypeEnum> Types, AssertionMetadata? Metadata = null);

public record AssetTypeAssertion(AssetTypeAssertionData Data) : Assertion<AssetTypeAssertionData>("c2pa.asset-type", Data);

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

public record CreativeWorkAssertionData(
    [property: JsonPropertyName("@context")] string? Context = "",
    [property: JsonPropertyName("@type")] string? Type = "",
    Dictionary<string, object>? Value = null);

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