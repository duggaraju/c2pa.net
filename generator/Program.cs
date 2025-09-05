// See https://aka.ms/new-console-template for more information
// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NJsonSchema;
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

var generator = new CSharpGenerator(schema);
generator.Settings.Namespace = "ContentAuthenticity";
generator.Settings.JsonLibrary = CSharpJsonLibrary.SystemTextJson;

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