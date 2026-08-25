// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity.Schema.Builder
{
    public partial class ClaimGeneratorInfoElement
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
    }
}

namespace ContentAuthenticity.Schema.Reader
{
    public partial class ClaimGeneratorInfoElement
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
    }
}