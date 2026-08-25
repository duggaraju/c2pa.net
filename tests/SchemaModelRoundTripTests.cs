// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Text.Json.Nodes;

namespace ContentAuthenticity.Tests;

public class SchemaModelRoundTripTests
{
    [Theory]
    [InlineData(typeof(ManifestDefinition), """
        {
            "title": "Test image",
            "format": "image/jpeg",
            "vendor": "org.example",
            "assertions": [
                {
                    "label": "org.example.test",
                    "data": { "value": 42 }
                }
            ]
        }
        """)]
    [InlineData(typeof(ManifestStore), """
        {
            "active_manifest": "org.example:urn:uuid:test",
            "manifests": {
                "org.example:urn:uuid:test": {
                    "title": "Test image",
                    "format": "image/jpeg",
                    "label": "org.example:urn:uuid:test",
                    "claim_version": 2
                }
            }
        }
        """)]
    [InlineData(typeof(Settings), """
        {
            "core": {},
            "verify": {
                "verify_after_reading": true,
                "remote_manifest_fetch": false
            }
        }
        """)]
    public void GeneratedModel_ShouldRoundTripJson(Type modelType, string json)
    {
        var options = JsonExtensions.JsonSerializerOptions(indented: false);
        var model = JsonSerializer.Deserialize(json, modelType, options);
        var roundTrippedJson = JsonSerializer.Serialize(model, modelType, options);

        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(roundTrippedJson)),
            $"JSON documents are not equivalent.\nExpected: {json}\nActual: {roundTrippedJson}");
    }
}