// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
namespace ContentAuthenticity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class JsonSchemaAttribute : Attribute
{
    public JsonSchemaAttribute(string schemaPath)
    {
        SchemaPath = schemaPath;
    }

    public JsonSchemaAttribute(string schemaPath, string rootName)
    {
        SchemaPath = schemaPath;
        RootName = rootName;
    }

    /// <summary>
    /// Path (optionally with JSON Pointer fragment) to the schema that defines this type.
    /// Example: "generator/Reader.schema.json#/definitions/Manifest".
    /// </summary>
    public string SchemaPath { get; }

    /// <summary>
    /// Optional: selects which schema within the file represents the root for generation.
    /// When provided, the generator will use either the schema file's root (if its title matches)
    /// or a definition under "$defs"/"definitions" with this name.
    /// </summary>
    public string? RootName { get; }

    /// <summary>
    /// When true, generated schema types are nested under a static <c>Schema</c> class on the attributed type
    /// (e.g. <c>Builder.Schema.ManifestDefinition</c>). When false (default), schema types are emitted directly
    /// as nested types on the attributed type (e.g. <c>Builder.ManifestDefinition</c>).
    /// </summary>
    public bool UseSchemaClass { get; set; }
}