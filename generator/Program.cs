// See https://aka.ms/new-console-template for more information
// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp;

// Check for required arguments
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: generator <schema-file> <output-file>");
    Console.Error.WriteLine("  schema-file: Path to the JSON schema file");
    Console.Error.WriteLine("  output-file: Path to the output C# file");
    return 1;
}

var schemaFile = args[0];
var outputFile = args[1];

// Validate schema file exists
if (!File.Exists(schemaFile))
{
    Console.Error.WriteLine($"Error: Schema file does not exist: {schemaFile}");
    return 1;
}

Console.WriteLine($"Reading schema from: {schemaFile}");
var schema = await JsonSchema.FromFileAsync(schemaFile);

var settings = new CSharpGeneratorSettings
{
    PropertyNameGenerator = new SnakeCaseToPascalCasePropertyNameGenerator(),
    Namespace = "ContentAuthenticity",
    JsonLibrary = CSharpJsonLibrary.SystemTextJson
};
var generator = new CSharpGenerator(schema,settings);

var file = generator.GenerateFile();

// Ensure output directory exists
var outputDir = Path.GetDirectoryName(outputFile);
if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
{
    Directory.CreateDirectory(outputDir);
}

await File.WriteAllTextAsync(outputFile, file);
Console.WriteLine($"Generated C# code written to: {outputFile}");
return 0;

public class SnakeCaseToPascalCasePropertyNameGenerator : IPropertyNameGenerator
{
    public string Generate(JsonSchemaProperty property)
    {
        // Split the snake_case name by underscore
        var parts = property.Name.Split('_');

        // Capitalize the first letter of each part and concatenate them
        var pascalCaseName = string.Concat(parts.Select(part =>
            char.ToUpperInvariant(part[0]) + part.Substring(1)
        ));

        return pascalCaseName;
    }
}

