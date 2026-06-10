
namespace ContentAuthenticity
{
    public sealed class ManifestDefinition : Schema.Builder.ManifestDefinition
    {
    }

    public sealed class ManifestStore : Schema.Reader.ManifestStore
    {
    }

    public sealed class ClaimGeneratorInfo : Schema.Builder.ClaimGeneratorInfo
    {
    }

    public sealed class Ingredient : Schema.Builder.Ingredient
    {
    }

    public sealed class Settings : Schema.Settings.SettingsSchema
    {
    }

    namespace Schema
    {
        [JsonSchema("../c2pa-rs/target/schema/ManifestDefinition.schema.json")]
        public static partial class Builder
        {
        }

        [JsonSchema("../c2pa-rs/target/schema/Reader.schema.json", "ManifestStore")]
        public static partial class Reader
        {
        }

        [JsonSchema("../c2pa-rs/target/schema/Settings.schema.json")]
        public static partial class Settings
        {
        }
    }
}