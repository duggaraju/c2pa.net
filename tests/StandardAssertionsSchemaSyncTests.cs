// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace ContentAuthenticity.Tests;

public class StandardAssertionsSchemaSyncTests
{
    [Fact]
    public void Schema_AssertionLabels_AreRecognizedByC2paRsOrAllowedSpecOnlyLabels()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var schemaPath = Path.Combine(repoRoot, "schemas", "c2pa-standard-assertions.schema.json");
        var labelsPath = Path.Combine(repoRoot, "c2pa-rs", "sdk", "src", "assertions", "labels.rs");

        Assert.True(File.Exists(schemaPath), $"Schema file not found: {schemaPath}");
        Assert.True(File.Exists(labelsPath), $"Rust labels file not found: {labelsPath}");

        var schemaJson = File.ReadAllText(schemaPath);
        using var schemaDoc = JsonDocument.Parse(schemaJson);

        var schemaLabels = schemaDoc.RootElement
            .GetProperty("properties")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var rustLabels = Regex.Matches(
                File.ReadAllText(labelsPath),
                "pub const\\s+\\w+\\s*:\\s*&str\\s*=\\s*\"(?<label>[^\"]+)\";")
            .Select(m => m.Groups["label"].Value)
            .Where(IsAssertionLikeLabel)
            .ToHashSet(StringComparer.Ordinal);

        // Labels currently present in spec/schema but not yet centralized in c2pa-rs labels.rs.
        var allowedSpecOnlyLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            "c2pa.alternative-content-representation",
            "font.info",
            "c2pa.external-reference",
            "c2pa.hash.multi-asset",
            "c2pa.session-keys",
        };

        var unrecognized = schemaLabels
            .Where(label =>
            {
                if (allowedSpecOnlyLabels.Contains(label))
                {
                    return false;
                }

                var normalized = StripVersionSuffix(label);
                return !rustLabels.Contains(label) && !rustLabels.Contains(normalized);
            })
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unrecognized.Length == 0,
            "Schema contains labels not recognized in c2pa-rs labels.rs: " + string.Join(", ", unrecognized));
    }

    [Fact]
    public void HandwrittenAssertions_Labels_AreSchemaCoveredOrExplicitlyHandwrittenOnly()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var schemaPath = Path.Combine(repoRoot, "schemas", "c2pa-standard-assertions.schema.json");
        var assertionsPath = Path.Combine(repoRoot, "lib", "Assertions.cs");

        Assert.True(File.Exists(schemaPath), $"Schema file not found: {schemaPath}");
        Assert.True(File.Exists(assertionsPath), $"Assertions file not found: {assertionsPath}");

        using var schemaDoc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var schemaLabels = schemaDoc.RootElement
            .GetProperty("properties")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var handwrittenLabels = Regex.Matches(
                File.ReadAllText(assertionsPath),
                "base\\(\"(?<label>[^\"]+)\"\\s*,")
            .Select(m => m.Groups["label"].Value)
            .Where(l => l.StartsWith("c2pa.", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        // Handwritten assertions intentionally outside the standard-assertions schema set.
        var handwrittenOnlyLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            "c2pa.thumbnail",
            "c2pa.metadata",
        };

        var missingFromSchema = handwrittenLabels
            .Where(label => !schemaLabels.Contains(label) && !handwrittenOnlyLabels.Contains(label))
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missingFromSchema.Length == 0,
            "Handwritten assertion labels are not covered by schema properties: " + string.Join(", ", missingFromSchema));
    }

    private static string StripVersionSuffix(string label)
    {
        return Regex.Replace(label, "\\.v\\d+$", string.Empty);
    }

    private static bool IsAssertionLikeLabel(string label)
    {
        if (label == "font.info")
        {
            return true;
        }

        return label.StartsWith("c2pa.", StringComparison.Ordinal);
    }
}