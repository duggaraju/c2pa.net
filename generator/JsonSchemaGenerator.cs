// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "RoslynDiagnostics",
    "RS1041:Do not use obsoleted APIs",
    Justification = "Suppression is necessary for this project.")]
[Generator(LanguageNames.CSharp)]
public sealed class JsonSchemaClassGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor SchemaParseFailed = new(
        id: "C2PANET001",
        title: "JSON schema parse failed",
        messageFormat: "Failed to parse JSON schema '{0}': {1}",
        category: "ContentAuthenticity.SchemaGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SchemaUnsupported = new(
        id: "C2PANET002",
        title: "JSON schema unsupported shape",
        messageFormat: "Schema '{0}' contains an unsupported construct at '{1}'; generated type may be incomplete",
        category: "ContentAuthenticity.SchemaGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SchemaFileNotFound = new(
        id: "C2PANET003",
        title: "JSON schema file not found",
        messageFormat: "Type '{0}' references schema '{1}', but the file could not be found",
        category: "ContentAuthenticity.SchemaGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SchemaReferencedMoreThanOnce = new(
        id: "C2PANET004",
        title: "JSON schema referenced more than once",
        messageFormat: "Schema '{0}' is referenced by multiple types; reference each schema file only once",
        category: "ContentAuthenticity.SchemaGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RefUnionArityUnsupported = new(
        id: "C2PANET005",
        title: "Ref-only union arity unsupported",
        messageFormat: "Schema '{0}' contains a ref-only union at '{1}' with {2} branch(es); only 2 are supported",
        category: "ContentAuthenticity.SchemaGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilation = context.CompilationProvider;

        // Also support schema discovery via [ContentAuthenticity.JsonSchema] on types.
        // This allows consumers to opt-in per-type; the referenced schema must still be provided as an MSBuild AdditionalFile.
        var attributedSchemaRefs = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "ContentAuthenticity.JsonSchemaAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => GetSchemaReference(ctx))
            .Where(static r => r.SchemaPath is not null);

        // Only generate when [JsonSchema] is present.
        var combinedInputs = attributedSchemaRefs.Collect().Combine(compilation);

        context.RegisterSourceOutput(combinedInputs, static (spc, input) =>
        {
            var references = input.Left;
            var comp = input.Right;

            if (references.IsDefaultOrEmpty)
                return;

            // Group by resolved schema file path and enforce each schema file is only referenced once.
            // (Multiple references makes generation ambiguous and can cause duplicate types.)
            var resolved = references
                .Where(static r => !string.IsNullOrWhiteSpace(r.SchemaPath))
                .Select(r => (Ref: r, SchemaDiskPath: ResolveSchemaDiskPath(NormalizePath(StripJsonPointer(r.SchemaPath!)), r.ReferencingSourceFilePath)))
                .ToImmutableArray();

            foreach (var item in resolved)
            {
                if (!string.IsNullOrWhiteSpace(item.SchemaDiskPath))
                    continue;

                spc.ReportDiagnostic(Diagnostic.Create(
                    SchemaFileNotFound,
                    Location.None,
                    item.Ref.ReferencingTypeMetadataName,
                    item.Ref.SchemaPath ?? string.Empty));
            }

            var groups = resolved
                .Where(static x => !string.IsNullOrWhiteSpace(x.SchemaDiskPath))
                .GroupBy(static x => NormalizeDiskPath(x.SchemaDiskPath!), StringComparer.Ordinal)
                .OrderBy(static g => g.Key, StringComparer.Ordinal);

            var usedHintNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var schemaDiskPath = group.Key;
                if (string.IsNullOrWhiteSpace(schemaDiskPath))
                    continue;

                if (group.Count() != 1)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        SchemaReferencedMoreThanOnce,
                        Location.None,
                        schemaDiskPath));
                    continue;
                }

                var first = group.First().Ref;

                // Always emit generated code into the namespace where the [JsonSchema] attribute is applied.
                // (Global namespace is represented by an empty string.)
                var baseNamespace = first.ReferencingNamespace;

                // Keep the attribute path stable in generated code (normalized, no JSON Pointer).
                var schemaAttributePath = NormalizePath(StripJsonPointer(first.SchemaPath!));

                // Load schema JSON from disk. The path in [JsonSchema("...")] is interpreted as relative
                // to the directory containing the .cs file that declares the attributed type.
                var json = TryLoadSchemaJsonFromDisk(schemaDiskPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        SchemaFileNotFound,
                        Location.None,
                        first.ReferencingTypeMetadataName,
                        first.SchemaPath ?? string.Empty));
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var schemaFileRoot = doc.RootElement;

                    // Choose which schema within the file is the generation root.
                    // If RootName is provided, use either the file root (if its title matches)
                    // or a definition under $defs/definitions.
                    var (root, rootPointer) = SelectSchemaRoot(schemaFileRoot, first.RootName);

                    // Root type name matches the attributed type (we extend it via partial).
                    var attributedTypeName = GetUnqualifiedTypeName(first.ReferencingTypeMetadataName);

                    // The schema root type name is based on the selected schema root (rootName/title),
                    // and is generated as a nested type under the attributed type's `Schema` container.
                    var schemaRootTypeName = GetSchemaRootTypeName(root, first.RootName, attributedTypeName);

                    // Collect all type names we might emit (for name reservation).
                    var typeNamesToEmit = GetAllTypeNamesToEmit(root, schemaRootTypeName);

                    // Keep the namespace the same as the attributed type.
                    var targetNamespace = baseNamespace;

                    var emitter = new Emitter(
                        spc,
                        schemaAttributePath,
                        targetNamespace,
                        typeNamesToEmit,
                        comp,
                        baseNamespace,
                        attributedTypeName,
                        first.ContainingTypes,
                        first.ReferencingType,
                        first.UseSchemaClass);

                    emitter.EmitFromSchema(root, schemaRootTypeName, rootPointer);

                    var hintName = GetHintName(first.ReferencingSourceFilePath, first.ReferencingTypeMetadataName);
                    hintName = MakeUniqueHintName(hintName, usedHintNames);
                    spc.AddSource(hintName, SourceText.From(emitter.ToSourceText(), Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(SchemaParseFailed, Location.None, schemaDiskPath, ex.Message));
                }
            }
        });
    }

    private static string GetHintName(string? referencingSourceFilePath, string referencingTypeMetadataName)
    {
        // Hint names must be valid and stable.
        // Prefix with the C# source file that declares [JsonSchema] for easier navigation.
        var stem = !string.IsNullOrWhiteSpace(referencingSourceFilePath)
            ? Path.GetFileNameWithoutExtension(referencingSourceFilePath)
            : GetUnqualifiedTypeName(referencingTypeMetadataName);

        var safeStem = SanitizeHintFileStem(stem);
        return $"{safeStem}.g.cs";
    }

    private static string MakeUniqueHintName(string hintName, HashSet<string> usedHintNames)
    {
        if (usedHintNames.Add(hintName))
            return hintName;

        var stem = Path.GetFileNameWithoutExtension(hintName);
        var ext = Path.GetExtension(hintName);

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"{stem}_{i}{ext}";
            if (usedHintNames.Add(candidate))
                return candidate;
        }

        // Extremely unlikely; fall back to a GUID.
        var fallback = $"{stem}_{Guid.NewGuid():N}{ext}";
        usedHintNames.Add(fallback);
        return fallback;
    }

    private static string SanitizeHintFileStem(string? stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
            return "JsonSchema";

        var sb = new StringBuilder(stem.Length);
        for (var i = 0; i < stem.Length; i++)
        {
            var ch = stem[i];
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                sb.Append(ch);
            else
                sb.Append('_');
        }

        var result = sb.ToString().Trim('_', '-');
        return string.IsNullOrWhiteSpace(result) ? "JsonSchema" : result;
    }

    private static uint Fnv1a32(string value)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= prime;
            }
            return hash;
        }
    }

    private static string GetUnqualifiedTypeName(string metadataName)
    {
        if (string.IsNullOrWhiteSpace(metadataName))
            return "Type";
        var idx = metadataName.LastIndexOf('.');
        return idx < 0 ? metadataName : metadataName.Substring(idx + 1);
    }

    private static string GetSchemaRootTypeName(JsonElement schemaRoot, string? requestedRootName, string attributedTypeName)
    {
        // Prefer the requested root name (2nd argument to [JsonSchema]).
        if (!string.IsNullOrWhiteSpace(requestedRootName))
        {
            var name = ToPascalIdentifier(requestedRootName);
            return string.Equals(name, attributedTypeName, StringComparison.Ordinal) ? name + "Schema" : name;
        }

        // Fall back to the schema title.
        var title = TryGetString(schemaRoot, "title");
        if (!string.IsNullOrWhiteSpace(title))
        {
            var name = ToPascalIdentifier(title);
            return string.Equals(name, attributedTypeName, StringComparison.Ordinal) ? name + "Schema" : name;
        }

        // Last resort: use the attributed type name (but it will be nested, so it must not equal the enclosing type).
        return ToPascalIdentifier(attributedTypeName + "Schema");
    }

    private static ImmutableArray<string> GetAllTypeNamesToEmit(JsonElement root, string rootTypeName)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        builder.Add(rootTypeName);

        if (TryGetDefsObject(root, out var defs, out _))
        {
            foreach (var def in defs.EnumerateObject())
                builder.Add(ToPascalIdentifier(def.Name));
        }
        return builder.ToImmutable();
    }

    private static (JsonElement Root, string Pointer) SelectSchemaRoot(JsonElement schemaFileRoot, string? requestedRootName)
    {
        // Default: use the schema document root.
        if (schemaFileRoot.ValueKind != JsonValueKind.Object)
            return (schemaFileRoot, "#");

        var title = TryGetString(schemaFileRoot, "title");

        if (!string.IsNullOrWhiteSpace(requestedRootName))
        {
            // If the schema root's title matches, treat the file root as the requested schema.
            if (!string.IsNullOrWhiteSpace(title) && string.Equals(title, requestedRootName, StringComparison.Ordinal))
                return (schemaFileRoot, "#");

            if (TryGetDefsObject(schemaFileRoot, out var defs, out var defsPointer) &&
                defs.TryGetProperty(requestedRootName, out var named) &&
                named.ValueKind != JsonValueKind.Undefined)
            {
                return (named, defsPointer + "/" + requestedRootName);
            }

            // If the requested name couldn't be resolved, fall back to the file root.
            return (schemaFileRoot, "#");
        }

        // If no root name is requested and the file root is just a container with $defs,
        // try selecting the schema named by its title (common in generated schemas).
        if (!string.IsNullOrWhiteSpace(title) &&
            TryGetDefsObject(schemaFileRoot, out var defsByTitle, out var defsPointerByTitle) &&
            defsByTitle.TryGetProperty(title, out var titled) &&
            titled.ValueKind != JsonValueKind.Undefined)
        {
            return (titled, defsPointerByTitle + "/" + title);
        }

        return (schemaFileRoot, "#");
    }

    private static bool TryGetDefsObject(JsonElement root, out JsonElement defs, out string pointer)
    {
        // Draft 2020-12 uses $defs; older drafts use definitions.
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("$defs", out defs) && defs.ValueKind == JsonValueKind.Object)
        {
            pointer = "#/$defs";
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("definitions", out defs) && defs.ValueKind == JsonValueKind.Object)
        {
            pointer = "#/definitions";
            return true;
        }

        defs = default;
        pointer = string.Empty;
        return false;
    }

    private static bool WouldCollide(Compilation compilation, string targetNamespace, ImmutableArray<string> typeNames)
    {
        if (string.IsNullOrWhiteSpace(targetNamespace))
            return false;
        for (var i = 0; i < typeNames.Length; i++)
        {
            var name = typeNames[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var metadataName = targetNamespace + "." + name;
            if (compilation.GetTypeByMetadataName(metadataName) is not null)
                return true;
        }
        return false;
    }

    private static string NormalizeDiskPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        try
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
        catch
        {
            return path.Replace('\\', '/');
        }
    }

    private readonly record struct TypeContainer(
        string Name,
        TypeKind Kind,
        bool IsRecord,
        bool IsStatic,
        Accessibility Accessibility);

    private readonly record struct SchemaReference(
        string? SchemaPath,
        string? RootName,
        string ReferencingTypeMetadataName,
        string ReferencingNamespace,
        string? ReferencingSourceFilePath,
        ImmutableArray<TypeContainer> ContainingTypes,
        TypeContainer ReferencingType,
        bool UseSchemaClass);

    private static SchemaReference GetSchemaReference(GeneratorAttributeSyntaxContext ctx)
    {
        // Only the first constructor argument matters: [JsonSchema("path/to/file.schema.json#/pointer")]
        string? schemaPath = null;
        string? rootName = null;
        var useSchemaClass = false;
        if (ctx.Attributes.Length > 0 &&
            ctx.Attributes[0].ConstructorArguments.Length > 0 &&
            ctx.Attributes[0].ConstructorArguments[0].Value is string s &&
            !string.IsNullOrWhiteSpace(s))
        {
            schemaPath = s.Trim();
        }

        // Optional second ctor arg: [JsonSchema("file.json", "RootName")]
        if (ctx.Attributes.Length > 0 &&
            ctx.Attributes[0].ConstructorArguments.Length > 1 &&
            ctx.Attributes[0].ConstructorArguments[1].Value is string rn &&
            !string.IsNullOrWhiteSpace(rn))
        {
            rootName = rn.Trim();
        }

        // Optional named arg/property: [JsonSchema("file.json") { UseSchemaClass = true }]
        if (ctx.Attributes.Length > 0 && ctx.Attributes[0].NamedArguments.Length > 0)
        {
            foreach (var kvp in ctx.Attributes[0].NamedArguments)
            {
                if (string.Equals(kvp.Key, "UseSchemaClass", StringComparison.Ordinal) && kvp.Value.Value is bool b)
                {
                    useSchemaClass = b;
                }
            }
        }

        var ns = ctx.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var sourceFilePath = ctx.TargetNode?.SyntaxTree?.FilePath;
        var named = (INamedTypeSymbol)ctx.TargetSymbol;

        // Preserve nested type structure (e.g. C2pa.Settings) when emitting partial types.
        // Without this, we would emit a top-level Settings type and create duplicates.
        var containersReversed = new List<INamedTypeSymbol>();
        for (var ct = named.ContainingType; ct is not null; ct = ct.ContainingType)
            containersReversed.Add(ct);
        containersReversed.Reverse();

        var containingTypes = containersReversed
            .Select(static t => new TypeContainer(
                t.Name,
                t.TypeKind,
                t.IsRecord,
                t.IsStatic,
                t.DeclaredAccessibility))
            .ToImmutableArray();

        var referencingType = new TypeContainer(
            named.Name,
            named.TypeKind,
            named.IsRecord,
            named.IsStatic,
            named.DeclaredAccessibility);

        return new SchemaReference(
            schemaPath,
            rootName,
            ctx.TargetSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            ns,
            string.IsNullOrWhiteSpace(sourceFilePath) ? null : sourceFilePath,
            containingTypes,
            referencingType,
            useSchemaClass);
    }

    private static string StripJsonPointer(string schemaPath)
    {
        var idx = schemaPath.IndexOf('#');
        return idx < 0 ? schemaPath : schemaPath.Substring(0, idx);
    }

    private static string? TryLoadSchemaJsonFromDisk(string? schemaDiskPath)
    {
        if (string.IsNullOrWhiteSpace(schemaDiskPath))
            return null;

        try
        {
            if (!File.Exists(schemaDiskPath))
                return null;
            return File.ReadAllText(schemaDiskPath, Encoding.UTF8);
        }
        catch
        {
            // Treat read errors as missing; a clearer diagnostic can be added if needed.
            return null;
        }
    }

    private static string? TryLoadSchemaJson(string schemaDiskPath, ImmutableArray<AdditionalText> additionalTexts)
    {
        if (string.IsNullOrWhiteSpace(schemaDiskPath))
            return null;

        var normalizedTarget = NormalizeDiskPath(schemaDiskPath);

        if (!additionalTexts.IsDefaultOrEmpty)
        {
            for (var i = 0; i < additionalTexts.Length; i++)
            {
                var additional = additionalTexts[i];
                if (additional is null)
                    continue;

                var additionalPath = NormalizeDiskPath(additional.Path);
                if (!string.Equals(additionalPath, normalizedTarget, StringComparison.Ordinal))
                    continue;

                try
                {
                    var text = additional.GetText();
                    if (text is null)
                        break;
                    return text.ToString();
                }
                catch
                {
                    break;
                }
            }
        }

        return TryLoadSchemaJsonFromDisk(schemaDiskPath);
    }

    private static string? ResolveSchemaDiskPath(string schemaAttributePath, string? referencingSourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(schemaAttributePath))
            return null;

        // If attribute already specifies an absolute path, keep it as-is.
        var candidate = schemaAttributePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(candidate))
        {
            try
            {
                return Path.GetFullPath(candidate);
            }
            catch
            {
                return candidate;
            }
        }

        // Primary rule: resolve relative to the .cs file that uses [JsonSchema].
        var baseDir = !string.IsNullOrWhiteSpace(referencingSourceFilePath)
            ? Path.GetDirectoryName(referencingSourceFilePath)
            : null;

        // If we can't determine the directory containing the attributed .cs file, and the schema path
        // isn't absolute, we can't reliably locate it.
        if (string.IsNullOrWhiteSpace(baseDir))
            return null;

        try
        {
            return Path.GetFullPath(Path.Combine(baseDir, candidate));
        }
        catch
        {
            return Path.Combine(baseDir, candidate);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        return path.Replace('\\', '/');
    }

    // NOTE: Generator now runs purely from [JsonSchema] and reads schema files from disk.

    private static string? TryGetString(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;
        if (!obj.TryGetProperty(propertyName, out var prop))
            return null;
        if (prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static string ToPascalIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Type";

        var parts = raw
            .Replace('-', '_')
            .Replace(' ', '_')
            .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

        var name = string.Concat(parts.Select(static p =>
        {
            if (p.Length == 0) return string.Empty;
            if (p.Length == 1) return char.ToUpperInvariant(p[0]).ToString();
            return char.ToUpperInvariant(p[0]) + p.Substring(1);
        }));

        if (name.Length == 0)
            name = "Type";
        if (!SyntaxFacts.IsIdentifierStartCharacter(name[0]))
            name = "T" + name;
        return SanitizeIdentifier(name);
    }

    private static string SanitizeIdentifier(string name)
    {
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (i == 0)
            {
                sb.Append(SyntaxFacts.IsIdentifierStartCharacter(ch) ? ch : '_');
            }
            else
            {
                sb.Append(SyntaxFacts.IsIdentifierPartCharacter(ch) ? ch : '_');
            }
        }

        var result = sb.ToString();
        return SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None ? "@" + result : result;
    }

    private sealed class Emitter
    {
        private readonly SourceProductionContext spc;
        private readonly string schemaPath;
        private readonly string schemaNamespace;
        private readonly HashSet<string> reservedTypeNames;
        private readonly Compilation compilation;
        private readonly string baseNamespace;
        private readonly string rootTypeName;
        private readonly ImmutableArray<TypeContainer> containingTypes;
        private readonly TypeContainer rootType;
        private readonly bool useSchemaClass;
        private readonly string generatedTypeQualifier;
        private const string Indent = "    ";
        private readonly StringBuilder sb = new();
        private readonly HashSet<string> emittedTypes = new(StringComparer.Ordinal);
        private readonly HashSet<string> enumTypes = new(StringComparer.Ordinal);
        private readonly HashSet<string> reservedGeneratedNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> stringAliasDefinitions = new(StringComparer.Ordinal);
        private readonly HashSet<string> jsonElementAliasDefinitions = new(StringComparer.Ordinal);
        private readonly List<(string Name, string[] Values)> pendingStringUnionEnums = new();
        private readonly HashSet<string> pendingStringUnionEnumNames = new(StringComparer.Ordinal);
        private readonly List<(string Name, string[] Values)> pendingStringUnionValueTypes = new();
        private readonly HashSet<string> pendingStringUnionValueTypeNames = new(StringComparer.Ordinal);
        private readonly HashSet<string> stringValueTypes = new(StringComparer.Ordinal);
        private readonly HashSet<string> generatedStructValueTypes = new(StringComparer.Ordinal);
        private string rootPointer = "#";
        private string schemaRootTypeName = string.Empty;

        public Emitter(
            SourceProductionContext spc,
            string schemaPath,
            string schemaNamespace,
            ImmutableArray<string> reservedTypeNames,
            Compilation compilation,
            string baseNamespace,
            string rootTypeName,
            ImmutableArray<TypeContainer> containingTypes,
            TypeContainer rootType,
            bool useSchemaClass)
        {
            this.spc = spc;
            this.schemaPath = schemaPath.Replace('\\', '/');
            this.schemaNamespace = schemaNamespace;
            this.reservedTypeNames = new HashSet<string>(reservedTypeNames.IsDefault ? Array.Empty<string>() : reservedTypeNames, StringComparer.Ordinal);
            this.compilation = compilation;
            this.baseNamespace = baseNamespace;
            this.rootTypeName = rootTypeName;
            this.containingTypes = containingTypes;
            this.rootType = rootType;
            this.useSchemaClass = useSchemaClass;
            generatedTypeQualifier = useSchemaClass ? "Schema." : string.Empty;

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(schemaNamespace))
            {
                sb.Append("namespace ").Append(schemaNamespace).AppendLine(";");
                sb.AppendLine();
            }
        }

        public void EmitFromSchema(JsonElement root, string schemaRootTypeName, string rootPointer)
        {
            this.rootPointer = string.IsNullOrWhiteSpace(rootPointer) ? "#" : rootPointer;
            this.schemaRootTypeName = schemaRootTypeName;

            // Discover simple type-alias definitions (e.g. DateT is just a string in Reader.schema.json).
            if (TryGetDefsObject(root, out var defsForAliases, out _))
            {
                foreach (var def in defsForAliases.EnumerateObject())
                {
                    var defName = ToPascalIdentifier(def.Name);
                    if (IsStringTypeSchema(def.Value, out var allowsNull))
                    {
                        stringAliasDefinitions[defName] = allowsNull;
                    }

                    // Some schema helper defs are intentionally untyped (e.g. `anyOf: [ true ]`), meaning
                    // "any JSON value is valid". Model these as JsonElement instead of emitting a wrapper type.
                    if (IsAlwaysValidSchema(def.Value))
                    {
                        jsonElementAliasDefinitions.Add(defName);
                    }
                }
            }

            // Pre-scan for enums so we can avoid emitting 'required' on enum properties (value types).
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("enum", out var rootEnum) && rootEnum.ValueKind == JsonValueKind.Array)
            {
                enumTypes.Add(rootTypeName);
            }

            if (TryGetDefsObject(root, out var defsForScan, out _))
            {
                foreach (var def in defsForScan.EnumerateObject())
                {
                    if (def.Value.ValueKind == JsonValueKind.Object && def.Value.TryGetProperty("enum", out var defEnum) && defEnum.ValueKind == JsonValueKind.Array)
                    {
                        enumTypes.Add(ToPascalIdentifier(def.Name));
                    }

                    if (def.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (def.Value.TryGetProperty("oneOf", out var defOneOf) && defOneOf.ValueKind == JsonValueKind.Array &&
                            TryExtractStringLiteralUnion(defOneOf, out _, out _, out _))
                        {
                            enumTypes.Add(ToPascalIdentifier(def.Name));
                        }
                        else if (def.Value.TryGetProperty("anyOf", out var defAnyOf) && defAnyOf.ValueKind == JsonValueKind.Array &&
                                 TryExtractStringLiteralUnion(defAnyOf, out _, out _, out _))
                        {
                            enumTypes.Add(ToPascalIdentifier(def.Name));
                        }
                    }
                }
            }

            // Emit partial type declarations matching the original nesting.
            // Without this, schemas applied to nested types (e.g. C2pa.Settings) would produce
            // a duplicate top-level type (e.g. Settings), which is confusing and breaks consumers.
            var currentIndent = string.Empty;
            if (!containingTypes.IsDefaultOrEmpty)
            {
                foreach (var ct in containingTypes)
                {
                    sb.AppendLine($"{currentIndent}{GetTypeDeclaration(ct)}");
                    sb.AppendLine($"{currentIndent}{{");
                    currentIndent += Indent;
                }
            }

            // By default, emit schema types directly as nested types on the attributed type.
            // If UseSchemaClass=true, emit them under a nested static Schema class.
            sb.AppendLine($"{currentIndent}{GetTypeDeclaration(rootType, rootTypeName)}");
            sb.AppendLine($"{currentIndent}{{");

            var rootMemberIndent = currentIndent + Indent;
            var schemaTypeIndent = rootMemberIndent;
            if (useSchemaClass)
            {
                sb.AppendLine($"{rootMemberIndent}public static partial class Schema");
                sb.AppendLine($"{rootMemberIndent}{{");
                schemaTypeIndent = rootMemberIndent + Indent;
            }

            // Emit the schema root type first.
            EmitTypeFromSchema(schemaRootTypeName, root, this.rootPointer, typeIndent: schemaTypeIndent);

            // Emit all schema definitions.
            if (TryGetDefsObject(root, out var defs, out var defsPointer))
            {
                foreach (var def in defs.EnumerateObject())
                {
                    var defName = ToPascalIdentifier(def.Name);

                    // If this definition is just a string alias, don't emit a wrapper type.
                    if (stringAliasDefinitions.ContainsKey(defName))
                        continue;

                    // If this definition is intentionally untyped (always valid), map it to JsonElement.
                    if (jsonElementAliasDefinitions.Contains(defName))
                        continue;

                    // If a type with this name already exists in the target namespace, don't emit a duplicate.
                    if (TypeExistsInBaseNamespace(defName))
                        continue;

                    EmitTypeFromSchema(defName, def.Value, defsPointer + "/" + def.Name, typeIndent: schemaTypeIndent);
                }
            }

            // Emit any property-level string union enums.
            if (pendingStringUnionEnums.Count > 0)
            {
                foreach (var item in pendingStringUnionEnums.OrderBy(static kv => kv.Name, StringComparer.Ordinal))
                {
                    EmitEnumFromStrings(item.Name, item.Values, schemaTypeIndent);
                }
            }

            // Emit any property-level open-string union value types.
            if (pendingStringUnionValueTypes.Count > 0)
            {
                foreach (var item in pendingStringUnionValueTypes.OrderBy(static kv => kv.Name, StringComparer.Ordinal))
                {
                    EmitStringValueTypeFromStrings(item.Name, item.Values, schemaTypeIndent);
                }
            }

            if (useSchemaClass)
            {
                sb.AppendLine($"{rootMemberIndent}}}");
            }

            sb.AppendLine($"{currentIndent}}}");
            if (!containingTypes.IsDefaultOrEmpty)
            {
                for (var i = containingTypes.Length - 1; i >= 0; i--)
                {
                    currentIndent = currentIndent.Substring(0, Math.Max(0, currentIndent.Length - Indent.Length));
                    sb.AppendLine($"{currentIndent}}}");
                }
            }
            sb.AppendLine();
        }

        private static string GetTypeDeclaration(TypeContainer t, string? overrideName = null)
        {
            var name = string.IsNullOrWhiteSpace(overrideName) ? t.Name : overrideName;
            var access = t.Accessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => "internal"
            };

            var staticKw = t.IsStatic ? "static " : string.Empty;
            var kind = t.Kind switch
            {
                TypeKind.Struct => t.IsRecord ? "record struct" : "struct",
                TypeKind.Class => t.IsRecord ? "record" : "class",
                _ => t.IsRecord ? "record" : "class"
            };

            return $"{access} {staticKw}partial {kind} {name}";
        }

        public string ToSourceText()
        {
            return sb.ToString();
        }

        private void EmitTypeFromSchema(string typeName, JsonElement schema, string pointer, string typeIndent)
        {
            if (!emittedTypes.Add(typeName))
                return;

            var memberIndent = typeIndent + Indent;
            var isSchemaRoot = string.Equals(typeName, schemaRootTypeName, StringComparison.Ordinal);

            // Handle enum definitions.
            if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
            {
                EmitEnum(typeName, enumValues, typeIndent);
                return;
            }

            // Handle string literal unions defined via oneOf/anyOf.
            if (schema.ValueKind == JsonValueKind.Object)
            {
                // Handle object-branch unions like SignerSettings:
                // oneOf/anyOf: [ { type: object, properties: { local: {..} }, required:["local"] }, { ... remote ... } ]
                if (schema.TryGetProperty("oneOf", out var objOneOf) && objOneOf.ValueKind == JsonValueKind.Array &&
                    TryExtractObjectDiscriminatedUnion(objOneOf, out var objOneOfArms, out var hasNullObjOneOf))
                {
                    var typedArms = AssignObjectUnionPayloadTypeNames(typeName, objOneOfArms, pointer + "/oneOf");
                    EmitObjectDiscriminatedUnion(typeName, typedArms, pointer + "/oneOf", typeIndent, allowsNull: hasNullObjOneOf);
                    return;
                }

                if (schema.TryGetProperty("anyOf", out var objAnyOf) && objAnyOf.ValueKind == JsonValueKind.Array &&
                    TryExtractObjectDiscriminatedUnion(objAnyOf, out var objAnyOfArms, out var hasNullObjAnyOf))
                {
                    var typedArms = AssignObjectUnionPayloadTypeNames(typeName, objAnyOfArms, pointer + "/anyOf");
                    EmitObjectDiscriminatedUnion(typeName, typedArms, pointer + "/anyOf", typeIndent, allowsNull: hasNullObjAnyOf);
                    return;
                }

                // Handle ref-only unions like UriOrResource: anyOf/oneOf: [ { $ref: "..." }, { $ref: "..." } ]
                if (schema.TryGetProperty("oneOf", out var refOneOf) && refOneOf.ValueKind == JsonValueKind.Array &&
                    TryExtractRefUnion(refOneOf, out var refOneOfRefs, out var hasNullRefOneOf))
                {
                    EmitRefUnion(typeName, refOneOfRefs, pointer, typeIndent, allowsNull: hasNullRefOneOf);
                    return;
                }

                if (schema.TryGetProperty("anyOf", out var refAnyOf) && refAnyOf.ValueKind == JsonValueKind.Array &&
                    TryExtractRefUnion(refAnyOf, out var refAnyOfRefs, out var hasNullRefAnyOf))
                {
                    EmitRefUnion(typeName, refAnyOfRefs, pointer, typeIndent, allowsNull: hasNullRefAnyOf);
                    return;
                }

                if (schema.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array &&
                    TryExtractStringLiteralUnion(oneOf, out var oneOfValues, out _, out var hasOpenStringOneOf))
                {
                    if (hasOpenStringOneOf)
                        EmitStringValueTypeFromStrings(typeName, oneOfValues, typeIndent);
                    else
                        EmitEnumFromStrings(typeName, oneOfValues, typeIndent);
                    return;
                }

                if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array &&
                    TryExtractStringLiteralUnion(anyOf, out var anyOfValues, out _, out var hasOpenStringAnyOf))
                {
                    if (hasOpenStringAnyOf)
                        EmitStringValueTypeFromStrings(typeName, anyOfValues, typeIndent);
                    else
                        EmitEnumFromStrings(typeName, anyOfValues, typeIndent);
                    return;
                }

                // Handle mixed unions like BuilderIntent: one object branch + string literal branches.
                if (schema.TryGetProperty("oneOf", out var mixedOneOf) && mixedOneOf.ValueKind == JsonValueKind.Array &&
                    TryExtractObjectAndStringLiteralUnion(mixedOneOf, out var objectBranch, out var stringBranches, out var hasNullMixedOneOf))
                {
                    EmitObjectAndStringLiteralUnion(typeName, objectBranch, stringBranches, pointer, typeIndent, allowsNull: hasNullMixedOneOf);
                    return;
                }

                if (schema.TryGetProperty("anyOf", out var mixedAnyOf) && mixedAnyOf.ValueKind == JsonValueKind.Array &&
                    TryExtractObjectAndStringLiteralUnion(mixedAnyOf, out var objectBranch2, out var stringBranches2, out var hasNullMixedAnyOf))
                {
                    EmitObjectAndStringLiteralUnion(typeName, objectBranch2, stringBranches2, pointer, typeIndent, allowsNull: hasNullMixedAnyOf);
                    return;
                }
            }

            // Object-like schemas: "type": "object" or has "properties" or "additionalProperties".
            var isObjectLike = schema.ValueKind == JsonValueKind.Object &&
                               (
                                   (schema.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "object") ||
                                   schema.TryGetProperty("properties", out _) ||
                                   schema.TryGetProperty("additionalProperties", out _)
                               );

            if (!isObjectLike)
            {
                // For non-object definitions, emit as a type alias wrapper class (JsonElement payload).
                sb.AppendLine($"{typeIndent}public sealed partial class {typeName}");
                sb.AppendLine($"{typeIndent}{{");
                sb.AppendLine($"{memberIndent}[JsonExtensionData]");
                sb.AppendLine($"{memberIndent}public Dictionary<string, JsonElement>? AdditionalData {{ get; set; }}");
                sb.AppendLine($"{typeIndent}}}");
                sb.AppendLine();
                spc.ReportDiagnostic(Diagnostic.Create(SchemaUnsupported, Location.None, Path.GetFileName(schemaPath), pointer));
                return;
            }

            var required = new HashSet<string>(StringComparer.Ordinal);
            if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in req.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        required.Add(item.GetString() ?? string.Empty);
                }
            }
            sb.AppendLine($"{typeIndent}public partial class {typeName}");
            sb.AppendLine($"{typeIndent}{{");

            // additionalProperties => Dictionary<string, T>
            if (schema.TryGetProperty("additionalProperties", out var additionalProps))
            {
                // If additionalProperties is true/false or schema, treat as extension data.
                if (additionalProps.ValueKind == JsonValueKind.Object)
                {
                    var valueType = ResolveType(additionalProps, nullable: true);
                    sb.AppendLine($"{memberIndent}[JsonExtensionData]");
                    sb.AppendLine($"{memberIndent}public Dictionary<string, {valueType}>? AdditionalProperties {{ get; set; }}");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"{memberIndent}[JsonExtensionData]");
                    sb.AppendLine($"{memberIndent}public Dictionary<string, JsonElement>? AdditionalData {{ get; set; }}");
                    sb.AppendLine();
                }
            }

            if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in props.EnumerateObject())
                {
                    var jsonName = prop.Name;
                    var propName = ToPascalIdentifier(jsonName);
                    var isRequired = required.Contains(jsonName);

                    // Match expected casing for *_id properties on the schema root type.
                    if (isSchemaRoot && jsonName.EndsWith("_id", StringComparison.Ordinal) && propName.EndsWith("Id", StringComparison.Ordinal))
                    {
                        propName = propName.Substring(0, propName.Length - 2) + "ID";
                    }

                    // If the property is a string enum (type=string + enum=[...]), emit an enum type.
                    if (prop.Value.ValueKind == JsonValueKind.Object &&
                        TryExtractStringEnum(prop.Value, out var stringEnumValues, out var stringEnumHasNull))
                    {
                        var enumName = EnsureUniqueTypeName(typeName + propName + "Enum");

                        // Treat it as a value type immediately so we don't emit `required` for it.
                        enumTypes.Add(enumName);

                        if (pendingStringUnionEnumNames.Add(enumName))
                        {
                            pendingStringUnionEnums.Add((enumName, stringEnumValues));
                        }

                        var enumType = (!isRequired || stringEnumHasNull) ? enumName + "?" : enumName;
                        var requiredKeywordEnum = ShouldEmitRequired(enumType) ? "required " : string.Empty;

                        sb.AppendLine($"{memberIndent}[JsonPropertyName(\"{EscapeString(jsonName)}\")] ");
                        sb.AppendLine($"{memberIndent}public {requiredKeywordEnum}{enumType} {propName} {{ get; set; }}");
                        sb.AppendLine();
                        continue;
                    }

                    // If the property is a string union (oneOf/anyOf of string const/enum), emit an enum type.
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        var hasUnion = false;
                        string[] unionValues = Array.Empty<string>();
                        var unionHasNull = false;
                        var unionHasOpenString = false;

                        if (prop.Value.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array &&
                            TryExtractStringLiteralUnion(oneOf, out unionValues, out unionHasNull, out unionHasOpenString))
                        {
                            hasUnion = true;
                        }
                        else if (prop.Value.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array &&
                                 TryExtractStringLiteralUnion(anyOf, out unionValues, out unionHasNull, out unionHasOpenString))
                        {
                            hasUnion = true;
                        }

                        if (hasUnion)
                        {
                            if (unionHasOpenString)
                            {
                                var valueTypeName = EnsureUniqueTypeName(typeName + propName);

                                // Treat it as a value type immediately so we don't emit `required` for it.
                                stringValueTypes.Add(valueTypeName);

                                // Track these so we can emit them under the root Schema container.
                                if (pendingStringUnionValueTypeNames.Add(valueTypeName))
                                {
                                    pendingStringUnionValueTypes.Add((valueTypeName, unionValues));
                                }

                                var propType = (!isRequired || unionHasNull) ? valueTypeName + "?" : valueTypeName;
                                var requiredKeywordValueType = ShouldEmitRequired(propType) ? "required " : string.Empty;

                                sb.AppendLine($"{memberIndent}[JsonPropertyName(\"{EscapeString(jsonName)}\")] ");
                                sb.AppendLine($"{memberIndent}public {requiredKeywordValueType}{propType} {propName} {{ get; set; }}");
                                sb.AppendLine();
                                continue;
                            }

                            var enumName = EnsureUniqueTypeName(typeName + propName + "Enum");

                            // Treat it as a value type immediately so we don't emit `required` for it.
                            enumTypes.Add(enumName);

                            // Track these so we can emit them under the root Schema container.
                            if (pendingStringUnionEnumNames.Add(enumName))
                            {
                                pendingStringUnionEnums.Add((enumName, unionValues));
                            }

                            var enumType = (!isRequired || unionHasNull) ? enumName + "?" : enumName;
                            var requiredKeywordEnum = ShouldEmitRequired(enumType) ? "required " : string.Empty;

                            sb.AppendLine($"{memberIndent}[JsonPropertyName(\"{EscapeString(jsonName)}\")] ");
                            sb.AppendLine($"{memberIndent}public {requiredKeywordEnum}{enumType} {propName} {{ get; set; }}");
                            sb.AppendLine();
                            continue;
                        }
                    }

                    var type = ResolveType(prop.Value, nullable: !isRequired);
                    var requiredKeyword2 = ShouldEmitRequired(type) ? "required " : string.Empty;

                    sb.AppendLine($"{memberIndent}[JsonPropertyName(\"{EscapeString(jsonName)}\")] ");
                    sb.AppendLine($"{memberIndent}public {requiredKeyword2}{type} {propName} {{ get; set; }}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"{typeIndent}}}");
            sb.AppendLine();
        }

        private bool TypeExistsInBaseNamespace(string typeName)
        {
            if (string.IsNullOrWhiteSpace(baseNamespace) || string.IsNullOrWhiteSpace(typeName))
                return false;

            // Don't treat the root type as an existing type to skip; we extend it.
            if (string.Equals(typeName, rootTypeName, StringComparison.Ordinal))
                return false;

            var metadataName = baseNamespace + "." + typeName;
            return compilation.GetTypeByMetadataName(metadataName) is not null;
        }

        private void EmitEnum(string typeName, JsonElement enumValues, string typeIndent)
        {
            // Only support string enums for now.
            var strings = new List<string>();
            foreach (var v in enumValues.EnumerateArray())
            {
                if (v.ValueKind == JsonValueKind.String)
                    strings.Add(v.GetString() ?? string.Empty);
            }

            EmitEnumFromStrings(typeName, strings.ToArray(), typeIndent);
        }

        private void EmitEnumFromStrings(string typeName, string[] values, string typeIndent)
        {
            var memberIndent = typeIndent + Indent;

            enumTypes.Add(typeName);
            sb.AppendLine($"{typeIndent}[JsonConverter(typeof(JsonStringEnumConverter))]");
            sb.AppendLine($"{typeIndent}public enum {typeName}");
            sb.AppendLine($"{typeIndent}{{");

            var usedMembers = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < values.Length; i++)
            {
                var raw = values[i];
                var member = EnsureUniqueEnumMemberName(usedMembers, ToPascalIdentifier(GetEnumTokenFromValue(raw)));
                sb.AppendLine($"{memberIndent}[global::System.Text.Json.Serialization.JsonStringEnumMemberName(\"{EscapeString(raw)}\")] ");
                sb.AppendLine($"{memberIndent}{member}{(i == values.Length - 1 ? string.Empty : ",")}");
            }

            sb.AppendLine($"{typeIndent}}}");
            sb.AppendLine();
        }

        private void EmitStringValueTypeFromStrings(string typeName, string[] values, string typeIndent)
        {
            var memberIndent = typeIndent + Indent;

            // Track these so we avoid emitting `required` for them.
            stringValueTypes.Add(typeName);

            sb.AppendLine($"{typeIndent}[JsonConverter(typeof({typeName}JsonConverter))]");
            sb.AppendLine($"{typeIndent}public readonly record struct {typeName}(string Value)");
            sb.AppendLine($"{typeIndent}{{");
            sb.AppendLine($"{memberIndent}public override string ToString() => Value;");
            sb.AppendLine();
            sb.AppendLine($"{memberIndent}public static implicit operator string({typeName} value) => value.Value;");
            sb.AppendLine($"{memberIndent}public static implicit operator {typeName}(string value) => new(value);");
            sb.AppendLine();

            // Emit known values as named helpers.
            var usedMembers = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Length; i++)
            {
                var raw = values[i];
                var name = EnsureUniqueEnumMemberName(usedMembers, ToPascalIdentifier(GetEnumTokenFromValue(raw)));
                sb.AppendLine($"{memberIndent}public static {typeName} {name} => new(\"{EscapeString(raw)}\");");
            }

            sb.AppendLine();
            sb.AppendLine($"{memberIndent}private sealed class {typeName}JsonConverter : JsonConverter<{typeName}>");
            sb.AppendLine($"{memberIndent}{{");
            sb.AppendLine($"{memberIndent}{Indent}public override {typeName} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
            sb.AppendLine($"{memberIndent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (reader.TokenType != JsonTokenType.String)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}return new {typeName}(reader.GetString() ?? string.Empty);");
            sb.AppendLine($"{memberIndent}{Indent}}}");
            sb.AppendLine();
            sb.AppendLine($"{memberIndent}{Indent}public override void Write(Utf8JsonWriter writer, {typeName} value, JsonSerializerOptions options)");
            sb.AppendLine($"{memberIndent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}writer.WriteStringValue(value.Value);");
            sb.AppendLine($"{memberIndent}{Indent}}}");
            sb.AppendLine($"{memberIndent}}}");

            sb.AppendLine($"{typeIndent}}}");
            sb.AppendLine();
        }

        private void EmitObjectAndStringLiteralUnion(
            string typeName,
            JsonElement objectBranch,
            string[] stringValues,
            string pointer,
            string typeIndent,
            bool allowsNull)
        {
            var memberIndent = typeIndent + Indent;

            // Track this so we avoid emitting `required` for a value-type union wrapper.
            generatedStructValueTypes.Add(typeName);

            var createTypeName = EnsureUniqueTypeName(typeName + "Create");
            var kindEnumName = EnsureUniqueTypeName(typeName + "Kind");

            // Emit the supporting payload type and enum in the same scope.
            EmitTypeFromSchema(createTypeName, objectBranch, pointer + "/object", typeIndent: typeIndent);
            EmitEnumFromStrings(kindEnumName, stringValues, typeIndent);

            sb.AppendLine($"{typeIndent}[JsonConverter(typeof({typeName}JsonConverter))]");
            sb.AppendLine($"{typeIndent}public readonly record struct {typeName}");
            sb.AppendLine($"{typeIndent}{{");
            sb.AppendLine($"{memberIndent}public {kindEnumName}? Kind {{ get; }}");
            sb.AppendLine($"{memberIndent}public {createTypeName}? Create {{ get; }}");
            sb.AppendLine();

            sb.AppendLine($"{memberIndent}private {typeName}({kindEnumName}? kind, {createTypeName}? create)");
            sb.AppendLine($"{memberIndent}{{");
            sb.AppendLine($"{memberIndent}{Indent}Kind = kind;");
            sb.AppendLine($"{memberIndent}{Indent}Create = create;");
            sb.AppendLine($"{memberIndent}}}");
            sb.AppendLine();

            sb.AppendLine($"{memberIndent}public static {typeName} FromKind({kindEnumName} kind) => new(kind, null);");
            sb.AppendLine($"{memberIndent}public static {typeName} FromCreate({createTypeName} create) => new(null, create);");
            sb.AppendLine();

            // Convenience helpers for known string values.
            var usedMembers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in stringValues)
            {
                var member = EnsureUniqueEnumMemberName(usedMembers, ToPascalIdentifier(GetEnumTokenFromValue(raw)));
                sb.AppendLine($"{memberIndent}public static {typeName} {member} => FromKind({kindEnumName}.{member});");
            }

            sb.AppendLine();
            sb.AppendLine($"{memberIndent}private sealed class {typeName}JsonConverter : JsonConverter<{typeName}>");
            sb.AppendLine($"{memberIndent}{{");
            sb.AppendLine($"{memberIndent}{Indent}public override {typeName} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
            sb.AppendLine($"{memberIndent}{Indent}{{");

            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (reader.TokenType == JsonTokenType.Null)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return default;");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
            sb.AppendLine();

            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (reader.TokenType == JsonTokenType.String)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}var kind = JsonSerializer.Deserialize<{kindEnumName}>(ref reader, options);");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return FromKind(kind);");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
            sb.AppendLine();

            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (reader.TokenType == JsonTokenType.StartObject)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}var create = JsonSerializer.Deserialize<{createTypeName}>(ref reader, options);");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}if (create is null)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return FromCreate(create);");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
            sb.AppendLine();
            sb.AppendLine($"{memberIndent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine($"{memberIndent}{Indent}}}");
            sb.AppendLine();

            sb.AppendLine($"{memberIndent}{Indent}public override void Write(Utf8JsonWriter writer, {typeName} value, JsonSerializerOptions options)");
            sb.AppendLine($"{memberIndent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (value.Create is not null)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}JsonSerializer.Serialize(writer, value.Create, options);");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return;");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
            sb.AppendLine();
            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (value.Kind is not null)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}JsonSerializer.Serialize(writer, value.Kind.Value, options);");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return;");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
            if (allowsNull)
            {
                sb.AppendLine();
                sb.AppendLine($"{memberIndent}{Indent}{Indent}writer.WriteNullValue();");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}return;");
                sb.AppendLine();
            }
            sb.AppendLine($"{memberIndent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine($"{memberIndent}{Indent}}}");
            sb.AppendLine($"{memberIndent}}}");
            sb.AppendLine($"{typeIndent}}}");
            sb.AppendLine();
        }

        private void EmitObjectDiscriminatedUnion(
            string typeName,
            (string DiscriminatorJsonName, string DiscriminatorPropName, string PayloadTypeName, JsonElement PayloadSchema, string ArmPointer)[] arms,
            string pointer,
            string typeIndent,
            bool allowsNull)
        {
            var memberIndent = typeIndent + Indent;

            // Track this so we avoid emitting `required` for a value-type union wrapper.
            generatedStructValueTypes.Add(typeName);

            // Emit payload types first.
            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                EmitTypeFromSchema(arm.PayloadTypeName, arm.PayloadSchema, arm.ArmPointer, typeIndent: typeIndent);
            }

            sb.AppendLine($"{typeIndent}[JsonConverter(typeof({typeName}JsonConverter))]");
            sb.AppendLine($"{typeIndent}public readonly record struct {typeName}");
            sb.AppendLine($"{typeIndent}{{");

            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.AppendLine($"{memberIndent}public {arm.PayloadTypeName}? {arm.DiscriminatorPropName} {{ get; }}");
            }
            sb.AppendLine();

            sb.Append($"{memberIndent}private {typeName}(");
            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.Append($"{arm.PayloadTypeName}? {ToCamelIdentifier(arm.DiscriminatorPropName)}");
                if (i != arms.Length - 1)
                    sb.Append(", ");
            }
            sb.AppendLine(")");
            sb.AppendLine($"{memberIndent}{{");
            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.AppendLine($"{memberIndent}{Indent}{arm.DiscriminatorPropName} = {ToCamelIdentifier(arm.DiscriminatorPropName)};");
            }
            sb.AppendLine($"{memberIndent}}}");
            sb.AppendLine();

            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.AppendLine($"{memberIndent}public static {typeName} From{arm.DiscriminatorPropName}({arm.PayloadTypeName} value) => new({string.Join(", ", arms.Select(a => a.DiscriminatorPropName == arm.DiscriminatorPropName ? "value" : "null"))});");
            }
            sb.AppendLine();

            sb.AppendLine($"{memberIndent}private sealed class {typeName}JsonConverter : JsonConverter<{typeName}>");
            sb.AppendLine($"{memberIndent}{{");
            sb.AppendLine($"{memberIndent}{Indent}public override {typeName} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
            sb.AppendLine($"{memberIndent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (reader.TokenType == JsonTokenType.Null)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return default;");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
            sb.AppendLine();
            sb.AppendLine($"{memberIndent}{Indent}{Indent}using var doc = JsonDocument.ParseValue(ref reader);");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}var element = doc.RootElement;");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (element.ValueKind != JsonValueKind.Object)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine();

            // Count matches and deserialize the single matching arm.
            sb.AppendLine($"{memberIndent}{Indent}{Indent}var matches = 0;");
            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.AppendLine($"{memberIndent}{Indent}{Indent}if (element.TryGetProperty(\"{EscapeString(arm.DiscriminatorJsonName)}\", out _)) matches++; ");
            }
            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (matches > 1)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine();

            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.AppendLine($"{memberIndent}{Indent}{Indent}if (element.TryGetProperty(\"{EscapeString(arm.DiscriminatorJsonName)}\", out var {ToCamelIdentifier(arm.DiscriminatorPropName)}Elem))");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}var value = {ToCamelIdentifier(arm.DiscriminatorPropName)}Elem.Deserialize<{arm.PayloadTypeName}>(options);");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}if (value is null)");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}{Indent}throw new JsonException();");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return new {typeName}({string.Join(", ", arms.Select(a => a.DiscriminatorPropName == arm.DiscriminatorPropName ? "value" : "null"))});");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
                sb.AppendLine();
            }

            sb.AppendLine($"{memberIndent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine($"{memberIndent}{Indent}}}");
            sb.AppendLine();

            sb.AppendLine($"{memberIndent}{Indent}public override void Write(Utf8JsonWriter writer, {typeName} value, JsonSerializerOptions options)");
            sb.AppendLine($"{memberIndent}{Indent}{{");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}var count = 0;");
            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.AppendLine($"{memberIndent}{Indent}{Indent}if (value.{arm.DiscriminatorPropName} is not null) count++; ");
            }
            sb.AppendLine($"{memberIndent}{Indent}{Indent}if (count > 1)");
            sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine();

            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                sb.AppendLine($"{memberIndent}{Indent}{Indent}if (value.{arm.DiscriminatorPropName} is not null)");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{{");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}writer.WriteStartObject();");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}writer.WritePropertyName(\"{EscapeString(arm.DiscriminatorJsonName)}\");");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}JsonSerializer.Serialize(writer, value.{arm.DiscriminatorPropName}, options);");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}writer.WriteEndObject();");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}{Indent}return;");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}}}");
            }

            if (allowsNull)
            {
                sb.AppendLine();
                sb.AppendLine($"{memberIndent}{Indent}{Indent}writer.WriteNullValue();");
                sb.AppendLine($"{memberIndent}{Indent}{Indent}return;");
                sb.AppendLine();
            }

            sb.AppendLine($"{memberIndent}{Indent}{Indent}throw new JsonException();");
            sb.AppendLine($"{memberIndent}{Indent}}}");
            sb.AppendLine($"{memberIndent}}}");
            sb.AppendLine($"{typeIndent}}}");
            sb.AppendLine();
        }

        private static string GetEnumTokenFromValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Unknown";

            var trimmed = raw.Trim();

            // Use the last URI segment (or fragment) as the token.
            var lastSlash = trimmed.LastIndexOf('/');
            var lastHash = trimmed.LastIndexOf('#');
            var split = Math.Max(lastSlash, lastHash);

            if (split >= 0 && split < trimmed.Length - 1)
                return trimmed.Substring(split + 1);

            return trimmed;
        }

        private static string EnsureUniqueEnumMemberName(HashSet<string> usedMembers, string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "Unknown";

            var name = baseName;
            var i = 2;
            while (usedMembers.Contains(name))
            {
                name = baseName + i;
                i++;
            }

            usedMembers.Add(name);
            return name;
        }

        private string EnsureUniqueTypeName(string baseName)
        {
            var name = baseName;
            var i = 2;
            while (!string.IsNullOrWhiteSpace(name) && (emittedTypes.Contains(name) || reservedTypeNames.Contains(name) || reservedGeneratedNames.Contains(name)))
            {
                name = baseName + i;
                i++;
            }

            // Reserve the name so subsequent generated types don't collide with it,
            // but don't mark it as emitted (definitions still need to be emitted later).
            reservedGeneratedNames.Add(name);
            return name;
        }

        private static bool TryExtractStringLiteralUnion(JsonElement unionArray, out string[] values, out bool hasNull, out bool hasOpenString)
        {
            hasNull = false;
            hasOpenString = false;
            var list = new List<string>();

            foreach (var item in unionArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                // { "type": "null" }
                if (item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "null")
                {
                    hasNull = true;
                    continue;
                }

                // { "const": "foo" }
                if (item.TryGetProperty("const", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    list.Add(c.GetString() ?? string.Empty);
                    continue;
                }

                // { "enum": ["foo", "bar"] } (or single-value enum)
                if (item.TryGetProperty("enum", out var e) && e.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in e.EnumerateArray())
                    {
                        if (v.ValueKind != JsonValueKind.String)
                            return Fail(out values, out hasNull, out hasOpenString);
                        list.Add(v.GetString() ?? string.Empty);
                    }
                    continue;
                }

                // { "type": "string" } means arbitrary string values are allowed (open union).
                // Only treat it as an open-ended branch if it doesn't also define const/enum.
                if (item.TryGetProperty("type", out var openType) && openType.ValueKind == JsonValueKind.String && openType.GetString() == "string")
                {
                    hasOpenString = true;
                    continue;
                }

                // If a branch is not a string literal, we can't safely model it as an enum.
                return Fail(out values, out hasNull, out hasOpenString);
            }

            values = list
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static s => s, StringComparer.Ordinal)
                .ToArray();

            return values.Length > 0;

            static bool Fail(out string[] values, out bool hasNull, out bool hasOpenString)
            {
                values = Array.Empty<string>();
                hasNull = false;
                hasOpenString = false;
                return false;
            }
        }

        private static bool TryExtractRefUnion(JsonElement unionArray, out string[] refs, out bool hasNull)
        {
            hasNull = false;
            var list = new List<string>();

            foreach (var item in unionArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    return Fail(out refs, out hasNull);

                // Allow explicit null branches.
                if (item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "null")
                {
                    hasNull = true;
                    continue;
                }

                // Only allow a pure $ref branch.
                if (!item.TryGetProperty("$ref", out var r) || r.ValueKind != JsonValueKind.String)
                    return Fail(out refs, out hasNull);

                var refStr = r.GetString();
                if (string.IsNullOrWhiteSpace(refStr))
                    return Fail(out refs, out hasNull);

                // Allow common annotation keywords alongside $ref; reject keywords that change semantics.
                if (item.EnumerateObject().Any(static p => p.Name is not ("$ref" or "description" or "title" or "$comment" or "deprecated")))
                    return Fail(out refs, out hasNull);

                list.Add(refStr);
            }

            refs = list
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static s => s, StringComparer.Ordinal)
                .ToArray();

            return refs.Length > 0;

            static bool Fail(out string[] refs, out bool hasNull)
            {
                refs = Array.Empty<string>();
                hasNull = false;
                return false;
            }
        }

        private static bool TryExtractObjectDiscriminatedUnion(
            JsonElement unionArray,
            out (string DiscriminatorJsonName, string DiscriminatorPropName, string PayloadTypeName, JsonElement PayloadSchema, string ArmPointer)[] arms,
            out bool hasNull)
        {
            hasNull = false;
            var list = new List<(string DiscriminatorJsonName, JsonElement PayloadSchema, string ArmPointer)>();

            var i = 0;
            foreach (var branch in unionArray.EnumerateArray())
            {
                var branchPointer = "";
                if (branch.ValueKind != JsonValueKind.Object)
                    return Fail(out arms, out hasNull);

                // Allow explicit null branches.
                if (branch.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "null")
                {
                    hasNull = true;
                    i++;
                    continue;
                }

                if (!IsObjectLikeSchema(branch))
                    return Fail(out arms, out hasNull);

                if (!branch.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
                    return Fail(out arms, out hasNull);

                // Require exactly one discriminator property.
                var propCount = 0;
                string? discriminator = null;
                JsonElement payload = default;
                foreach (var p in props.EnumerateObject())
                {
                    propCount++;
                    discriminator = p.Name;
                    payload = p.Value;
                }
                if (propCount != 1 || string.IsNullOrWhiteSpace(discriminator))
                    return Fail(out arms, out hasNull);

                // Require that discriminator is required (keeps the union well-formed).
                if (!branch.TryGetProperty("required", out var req) || req.ValueKind != JsonValueKind.Array)
                    return Fail(out arms, out hasNull);

                var isRequired = false;
                foreach (var r in req.EnumerateArray())
                {
                    if (r.ValueKind == JsonValueKind.String && string.Equals(r.GetString(), discriminator, StringComparison.Ordinal))
                    {
                        isRequired = true;
                        break;
                    }
                }
                if (!isRequired)
                    return Fail(out arms, out hasNull);

                // Payload must itself be object-like.
                if (!IsObjectLikeSchema(payload))
                    return Fail(out arms, out hasNull);

                // Arm pointer (relative) for diagnostics/emission (caller will prefix with union pointer).
                branchPointer = i.ToString();
                list.Add((discriminator, payload, branchPointer));
                i++;
            }

            if (list.Count < 2)
                return Fail(out arms, out hasNull);

            // Discriminator names must be unique.
            if (list.Select(x => x.DiscriminatorJsonName).Distinct(StringComparer.Ordinal).Count() != list.Count)
                return Fail(out arms, out hasNull);

            // Construct final arm info (names + payload type names assigned later by caller).
            arms = list
                .Select(x => (
                    DiscriminatorJsonName: x.DiscriminatorJsonName,
                    DiscriminatorPropName: ToPascalIdentifier(x.DiscriminatorJsonName),
                    PayloadTypeName: string.Empty,
                    PayloadSchema: x.PayloadSchema,
                    ArmPointer: x.ArmPointer))
                .ToArray();

            return true;

            static bool Fail(out (string, string, string, JsonElement, string)[] arms, out bool hasNull)
            {
                arms = Array.Empty<(string, string, string, JsonElement, string)>();
                hasNull = false;
                return false;
            }
        }

        private (string DiscriminatorJsonName, string DiscriminatorPropName, string PayloadTypeName, JsonElement PayloadSchema, string ArmPointer)[] AssignObjectUnionPayloadTypeNames(
            string unionTypeName,
            (string DiscriminatorJsonName, string DiscriminatorPropName, string PayloadTypeName, JsonElement PayloadSchema, string ArmPointer)[] arms,
            string unionPointer)
        {
            // Ensure unique payload type names and arm pointers.
            var usedPropNames = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < arms.Length; i++)
            {
                var arm = arms[i];
                var propName = EnsureUniqueEnumMemberName(usedPropNames, arm.DiscriminatorPropName);
                var payloadType = EnsureUniqueTypeName(unionTypeName + propName);
                var armPointer = unionPointer + "/" + arm.ArmPointer + "/properties/" + arm.DiscriminatorJsonName;
                arms[i] = (arm.DiscriminatorJsonName, propName, payloadType, arm.PayloadSchema, armPointer);
            }
            return arms;
        }

        private void EmitRefUnion(string typeName, string[] refs, string pointer, string typeIndent, bool allowsNull)
        {
            var memberIndent = typeIndent + Indent;

            // Track this so we avoid emitting `required` for a value-type union wrapper.
            generatedStructValueTypes.Add(typeName);

            // Resolve referenced types to C# type names (qualified when needed).
            var options = refs
                .Select(r => (Ref: r, Type: ResolveRefType(r)))
                .Where(static x => !string.IsNullOrWhiteSpace(x.Type))
                .ToArray();

            if (options.Length == 0)
            {
                spc.ReportDiagnostic(Diagnostic.Create(SchemaUnsupported, Location.None, Path.GetFileName(schemaPath), pointer));
                sb.AppendLine($"{typeIndent}public sealed partial class {typeName}");
                sb.AppendLine($"{typeIndent}{{");
                sb.AppendLine($"{memberIndent}[JsonExtensionData]");
                sb.AppendLine($"{memberIndent}public Dictionary<string, JsonElement>? AdditionalData {{ get; set; }}");
                sb.AppendLine($"{typeIndent}}}");
                sb.AppendLine();
                return;
            }

            // Property names are based on the referenced type names.
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var props = options
                .Select(o => new
                {
                    o.Ref,
                    o.Type,
                    Name = EnsureUniqueEnumMemberName(usedNames, ToPascalIdentifier(GetLastTypeToken(o.Type)))
                })
                .ToArray();

            if (props.Length != 2)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    RefUnionArityUnsupported,
                    Location.None,
                    Path.GetFileName(schemaPath),
                    pointer,
                    props.Length));

                // Emit a minimal stub to avoid cascading compile errors; build will fail due to the diagnostic.
                sb.AppendLine($"{typeIndent}public sealed partial class {typeName}");
                sb.AppendLine($"{typeIndent}{{");
                sb.AppendLine($"{memberIndent}[JsonExtensionData]");
                sb.AppendLine($"{memberIndent}public Dictionary<string, JsonElement>? AdditionalData {{ get; set; }}");
                sb.AppendLine($"{typeIndent}}}");
                sb.AppendLine();
                return;
            }

            var converter = allowsNull
                ? $"NullableUnionJsonConverter<{props[0].Type}, {props[1].Type}, {typeName}>"
                : $"UnionJsonConverter<{props[0].Type}, {props[1].Type}, {typeName}>";
            sb.AppendLine($"{typeIndent}[JsonConverter(typeof({converter}))]");
            sb.AppendLine($"{typeIndent}public readonly record struct {typeName}");
            sb.AppendLine($"{typeIndent}{{");

            foreach (var p in props)
            {
                sb.AppendLine($"{memberIndent}public {p.Type}? {p.Name} {{ get; }}");
            }

            sb.AppendLine();

            sb.Append($"{memberIndent}private {typeName}(");
            for (var i = 0; i < props.Length; i++)
            {
                var p = props[i];
                sb.Append($"{p.Type}? {ToCamelIdentifier(p.Name)}");
                if (i != props.Length - 1)
                    sb.Append(", ");
            }
            sb.AppendLine(")");
            sb.AppendLine($"{memberIndent}{{");
            foreach (var p in props)
            {
                sb.AppendLine($"{memberIndent}{Indent}{p.Name} = {ToCamelIdentifier(p.Name)};");
            }
            sb.AppendLine($"{memberIndent}}}");
            sb.AppendLine();

            foreach (var p in props)
            {
                sb.AppendLine($"{memberIndent}public static {typeName} From{p.Name}({p.Type} value) => new({string.Join(", ", props.Select(pp => pp.Name == p.Name ? "value" : "null"))});");
            }

            sb.AppendLine();

            sb.AppendLine($"{typeIndent}}}");
            sb.AppendLine();
        }

        private string ResolveRefType(string refStr)
        {
            if (TryResolveRefAlias(refStr, nullable: false, out var aliasedType))
                return StripNullable(aliasedType);

            var refType = RefToTypeName(refStr);
            if (string.IsNullOrWhiteSpace(refType))
                return "JsonElement";

            if (!TypeExistsInBaseNamespace(refType) && !string.Equals(refType, rootTypeName, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(generatedTypeQualifier))
                    refType = generatedTypeQualifier + refType;
            }

            return refType;
        }

        private static string GetLastTypeToken(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "Value";

            var trimmed = typeName.TrimEnd('?');
            var lastDot = trimmed.LastIndexOf('.');
            return lastDot >= 0 ? trimmed.Substring(lastDot + 1) : trimmed;
        }

        private static string ToCamelIdentifier(string pascal)
        {
            if (string.IsNullOrWhiteSpace(pascal))
                return "value";
            if (pascal.Length == 1)
                return pascal.ToLowerInvariant();
            return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        }

        private static bool TryExtractStringEnum(JsonElement schema, out string[] values, out bool hasNull)
        {
            values = Array.Empty<string>();
            hasNull = false;

            if (schema.ValueKind != JsonValueKind.Object)
                return false;

            if (!schema.TryGetProperty("enum", out var enumValues) || enumValues.ValueKind != JsonValueKind.Array)
                return false;

            // Only generate enums for string-typed schemas.
            if (!schema.TryGetProperty("type", out var typeProp))
                return false;

            var hasStringType = false;
            if (typeProp.ValueKind == JsonValueKind.String)
            {
                hasStringType = string.Equals(typeProp.GetString(), "string", StringComparison.Ordinal);
            }
            else if (typeProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in typeProp.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        return false;
                    var kind = item.GetString();
                    if (string.Equals(kind, "string", StringComparison.Ordinal))
                        hasStringType = true;
                    else if (string.Equals(kind, "null", StringComparison.Ordinal))
                        hasNull = true;
                    else
                        return false;
                }
            }
            else
            {
                return false;
            }

            if (!hasStringType)
                return false;

            var list = new List<string>();
            foreach (var v in enumValues.EnumerateArray())
            {
                if (v.ValueKind == JsonValueKind.Null)
                {
                    hasNull = true;
                    continue;
                }
                if (v.ValueKind != JsonValueKind.String)
                    return false;

                list.Add(v.GetString() ?? string.Empty);
            }

            values = list
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static s => s, StringComparer.Ordinal)
                .ToArray();

            return values.Length > 0;
        }

        private static bool TryExtractObjectAndStringLiteralUnion(JsonElement unionArray, out JsonElement objectSchema, out string[] values, out bool hasNull)
        {
            objectSchema = default;
            hasNull = false;
            var hasObject = false;
            var list = new List<string>();

            foreach (var item in unionArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    return Fail(out objectSchema, out values, out hasNull);

                // { "type": "null" }
                if (item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "null")
                {
                    hasNull = true;
                    continue;
                }

                if (IsObjectLikeSchema(item))
                {
                    if (hasObject)
                        return Fail(out objectSchema, out values, out hasNull);
                    hasObject = true;
                    objectSchema = item;
                    continue;
                }

                // { "const": "foo" }
                if (item.TryGetProperty("const", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    list.Add(c.GetString() ?? string.Empty);
                    continue;
                }

                // { "enum": ["foo", "bar"] }
                if (item.TryGetProperty("enum", out var e) && e.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in e.EnumerateArray())
                    {
                        if (v.ValueKind != JsonValueKind.String)
                            return Fail(out objectSchema, out values, out hasNull);
                        list.Add(v.GetString() ?? string.Empty);
                    }
                    continue;
                }

                // An open string branch (type=string without const/enum) is not supported for this mixed union.
                if (item.TryGetProperty("type", out var openType) && openType.ValueKind == JsonValueKind.String && openType.GetString() == "string")
                {
                    return Fail(out objectSchema, out values, out hasNull);
                }

                return Fail(out objectSchema, out values, out hasNull);
            }

            values = list
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static s => s, StringComparer.Ordinal)
                .ToArray();

            return hasObject && values.Length > 0;

            static bool Fail(out JsonElement objectSchema, out string[] values, out bool hasNull)
            {
                objectSchema = default;
                values = Array.Empty<string>();
                hasNull = false;
                return false;
            }
        }

        private static bool IsObjectLikeSchema(JsonElement schema)
        {
            if (schema.ValueKind != JsonValueKind.Object)
                return false;

            if (schema.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "object")
                return true;

            return schema.TryGetProperty("properties", out _) || schema.TryGetProperty("additionalProperties", out _);
        }

        private string ResolveType(JsonElement schema, bool nullable)
        {
            // anyOf/oneOf with null => nullable
            if (schema.ValueKind == JsonValueKind.Object)
            {
                if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
                {
                    // Common pattern in this schema is { "allOf": [ { "$ref": "#/definitions/X" } ] }.
                    // For now, resolve the first entry (usually the $ref) and ignore constraints in other entries.
                    foreach (var item in allOf.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Undefined || item.ValueKind == JsonValueKind.Null)
                            continue;
                        return ResolveType(item, nullable);
                    }
                }

                if (schema.TryGetProperty("$ref", out var refProp) && refProp.ValueKind == JsonValueKind.String)
                {
                    var refStr = refProp.GetString() ?? string.Empty;

                    if (TryResolveRefAlias(refStr, nullable, out var aliasedType))
                        return aliasedType;

                    var refType = RefToTypeName(refStr);
                    if (string.IsNullOrWhiteSpace(refType))
                        return nullable ? "JsonElement?" : "JsonElement";

                    // Only qualify refs that point at schema defs (those we emit under the root Schema class).
                    // Keep mapped library types (like Assertion) unqualified.
                    if (!TypeExistsInBaseNamespace(refType) && !string.Equals(refType, rootTypeName, StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(generatedTypeQualifier))
                    {
                        refType = generatedTypeQualifier + refType;
                    }

                    return nullable ? refType + "?" : refType;
                }

                if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
                {
                    return ResolveUnion(anyOf, nullable);
                }
                if (schema.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array)
                {
                    return ResolveUnion(oneOf, nullable);
                }

                if (schema.TryGetProperty("type", out var typeProp))
                {
                    if (typeProp.ValueKind == JsonValueKind.String)
                    {
                        var kind = typeProp.GetString();
                        return kind switch
                        {
                            "string" => nullable ? "string?" : "string",
                            "boolean" => nullable ? "bool?" : "bool",
                            "integer" => ResolveIntegerType(schema, nullable),
                            "number" => nullable ? "double?" : "double",
                            "array" => ResolveArrayType(schema, nullable),
                            "object" => ResolveObjectType(schema, nullable),
                            _ => nullable ? "JsonElement?" : "JsonElement"
                        };
                    }

                    if (typeProp.ValueKind == JsonValueKind.Array)
                    {
                        var seenNull = false;
                        string? nonNullKind = null;

                        foreach (var item in typeProp.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.String)
                                return nullable ? "JsonElement?" : "JsonElement";
                            var kind = item.GetString();
                            if (string.Equals(kind, "null", StringComparison.Ordinal))
                            {
                                seenNull = true;
                                continue;
                            }

                            nonNullKind ??= kind;

                            // Multiple non-null kinds are not modelled; fall back.
                            if (!string.Equals(nonNullKind, kind, StringComparison.Ordinal))
                                return nullable ? "JsonElement?" : "JsonElement";
                        }

                        if (string.IsNullOrWhiteSpace(nonNullKind))
                            return nullable ? "JsonElement?" : "JsonElement";

                        var effectiveNullable = nullable || seenNull;
                        return nonNullKind switch
                        {
                            "string" => effectiveNullable ? "string?" : "string",
                            "boolean" => effectiveNullable ? "bool?" : "bool",
                            "integer" => ResolveIntegerType(schema, effectiveNullable),
                            "number" => effectiveNullable ? "double?" : "double",
                            "array" => ResolveArrayType(schema, effectiveNullable),
                            "object" => ResolveObjectType(schema, effectiveNullable),
                            _ => effectiveNullable ? "JsonElement?" : "JsonElement"
                        };
                    }
                }

                // object shape without explicit type
                if (schema.TryGetProperty("properties", out _) || schema.TryGetProperty("additionalProperties", out _))
                {
                    return ResolveObjectType(schema, nullable);
                }
            }

            return nullable ? "JsonElement?" : "JsonElement";
        }

        private bool TryResolveRefAlias(string refStr, bool nullable, out string typeName)
        {
            typeName = string.Empty;

            var referenced = RefToTypeName(refStr);
            if (string.IsNullOrWhiteSpace(referenced))
                return false;

            // Map schema helper definitions onto existing library model types when appropriate.
            if (string.Equals(referenced, "Assertion", StringComparison.Ordinal))
            {
                typeName = "Assertion";
                return true;
            }

            if (stringAliasDefinitions.TryGetValue(referenced, out var defAllowsNull))
            {
                var effectiveNullable = nullable || defAllowsNull;
                typeName = effectiveNullable ? "string?" : "string";
                return true;
            }

            if (jsonElementAliasDefinitions.Contains(referenced))
            {
                typeName = nullable ? "JsonElement?" : "JsonElement";
                return true;
            }

            return false;
        }

        private static bool IsStringTypeSchema(JsonElement schema, out bool allowsNull)
        {
            allowsNull = false;

            if (schema.ValueKind != JsonValueKind.Object)
                return false;

            // Unwrap common allOf wrapper patterns.
            if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in allOf.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Undefined || item.ValueKind == JsonValueKind.Null)
                        continue;
                    return IsStringTypeSchema(item, out allowsNull);
                }
            }

            // Don't treat enums as aliases.
            if (schema.TryGetProperty("enum", out _))
                return false;

            // Allow the common pattern anyOf/oneOf: [ { type: "string" }, { type: "null" } ]
            // to be treated as a string? alias.
            if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
            {
                return IsStringNullUnion(anyOf, out allowsNull);
            }

            if (schema.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array)
            {
                return IsStringNullUnion(oneOf, out allowsNull);
            }

            if (!schema.TryGetProperty("type", out var typeProp))
                return false;

            if (typeProp.ValueKind == JsonValueKind.String)
            {
                return string.Equals(typeProp.GetString(), "string", StringComparison.Ordinal);
            }

            if (typeProp.ValueKind == JsonValueKind.Array)
            {
                var hasString = false;
                foreach (var item in typeProp.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        return false;
                    var kind = item.GetString();
                    if (string.Equals(kind, "string", StringComparison.Ordinal))
                        hasString = true;
                    else if (string.Equals(kind, "null", StringComparison.Ordinal))
                        allowsNull = true;
                    else
                        return false;
                }
                return hasString;
            }

            return false;

            static bool IsStringNullUnion(JsonElement unionArray, out bool allowsNull)
            {
                allowsNull = false;
                var hasString = false;

                foreach (var branch in unionArray.EnumerateArray())
                {
                    if (branch.ValueKind != JsonValueKind.Object)
                        return false;

                    // Any const/enum/refs should be handled by other generation paths, not as an alias.
                    if (branch.TryGetProperty("const", out _) || branch.TryGetProperty("enum", out _) || branch.TryGetProperty("$ref", out _))
                        return false;

                    if (!branch.TryGetProperty("type", out var t))
                        return false;

                    if (t.ValueKind == JsonValueKind.String)
                    {
                        var kind = t.GetString();
                        if (string.Equals(kind, "string", StringComparison.Ordinal))
                            hasString = true;
                        else if (string.Equals(kind, "null", StringComparison.Ordinal))
                            allowsNull = true;
                        else
                            return false;
                        continue;
                    }

                    if (t.ValueKind == JsonValueKind.Array)
                    {
                        var branchAllowsNull = false;
                        var branchHasString = false;
                        foreach (var item in t.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.String)
                                return false;
                            var kind = item.GetString();
                            if (string.Equals(kind, "string", StringComparison.Ordinal))
                                branchHasString = true;
                            else if (string.Equals(kind, "null", StringComparison.Ordinal))
                                branchAllowsNull = true;
                            else
                                return false;
                        }

                        hasString |= branchHasString;
                        allowsNull |= branchAllowsNull;
                        continue;
                    }

                    return false;
                }

                return hasString && allowsNull;
            }
        }

        private static bool IsAlwaysValidSchema(JsonElement schema)
        {
            // JSON Schema allows boolean schemas; `true` means "accept any instance".
            if (schema.ValueKind == JsonValueKind.True)
                return true;

            // Also treat `anyOf: [ true ]` (or `anyOf` containing `true`) as always-valid.
            if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in anyOf.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.True)
                        return true;
                }
            }

            return false;
        }

        private bool ShouldEmitRequired(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return false;

            // Nullable properties never need 'required'.
            if (type.EndsWith("?", StringComparison.Ordinal))
                return false;

            // Avoid forcing callers to set value types.
            var nonNullable = type.TrimEnd('?');
            var unqualified = (!string.IsNullOrWhiteSpace(generatedTypeQualifier) && nonNullable.StartsWith(generatedTypeQualifier, StringComparison.Ordinal))
                ? nonNullable.Substring(generatedTypeQualifier.Length)
                : nonNullable;

            if (IsKnownValueType(unqualified))
                return false;

            // Avoid forcing callers to set enums.
            if (enumTypes.Contains(unqualified))
                return false;

            // Avoid forcing callers to set generated string value types.
            if (stringValueTypes.Contains(unqualified))
                return false;

            // Avoid forcing callers to set generated record-struct union wrappers.
            if (generatedStructValueTypes.Contains(unqualified))
                return false;

            // Known reference types (plus generated object types) benefit from 'required' to avoid CS8618.
            return true;
        }

        private static bool IsKnownValueType(string type)
        {
            // This generator mainly emits these primitives/structs. Keep conservative.
            return type switch
            {
                "bool" => true,
                "byte" => true,
                "sbyte" => true,
                "short" => true,
                "ushort" => true,
                "int" => true,
                "uint" => true,
                "long" => true,
                "ulong" => true,
                "float" => true,
                "double" => true,
                "decimal" => true,
                "char" => true,
                "nint" => true,
                "nuint" => true,
                "Guid" => true,
                "DateTime" => true,
                "DateTimeOffset" => true,
                "TimeSpan" => true,
                "JsonElement" => true,
                _ => false
            };
        }

        private string ResolveUnion(JsonElement unionArray, bool nullable)
        {
            JsonElement? nonNull = null;
            var seenNull = false;
            foreach (var item in unionArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "null")
                {
                    seenNull = true;
                    continue;
                }
                nonNull ??= item;
            }

            if (nonNull is null)
                return "JsonElement";

            return ResolveType(nonNull.Value, nullable: nullable || seenNull);
        }

        private string ResolveIntegerType(JsonElement schema, bool nullable)
        {
            var format = TryGetString(schema, "format");
            var type = format switch
            {
                "uint8" => "byte",
                "int8" => "sbyte",
                "int16" => "short",
                "uint16" => "ushort",
                "int32" => "int",
                "uint32" => "uint",
                "int64" => "long",
                "uint64" => "ulong",
                _ => "int"
            };

            return nullable ? type + "?" : type;
        }

        private string ResolveArrayType(JsonElement schema, bool nullable)
        {
            if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("items", out var items))
            {
                var elementType = ResolveType(items, nullable: false);
                var listType = $"List<{StripNullable(elementType)}>";
                return nullable ? listType + "?" : listType;
            }

            return nullable ? "List<JsonElement>?" : "List<JsonElement>";
        }

        private string ResolveObjectType(JsonElement schema, bool nullable)
        {
            // If additionalProperties is present and no explicit properties, emit dictionary.
            if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                // Inline object: use JsonElement to avoid generating anonymous types.
                return nullable ? "JsonElement?" : "JsonElement";
            }

            if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("additionalProperties", out var ap) && ap.ValueKind == JsonValueKind.Object)
            {
                var valueType = ResolveType(ap, nullable: true);
                var dictType = $"Dictionary<string, {StripNullable(valueType)}>";
                return nullable ? dictType + "?" : dictType;
            }

            return nullable ? "JsonElement?" : "JsonElement";
        }

        private static string StripNullable(string typeName)
        {
            return typeName.EndsWith("?", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 1) : typeName;
        }

        private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string? RefToTypeName(string refStr)
        {
            // Only support local refs like #/$defs/Foo or #/definitions/Foo
            const string defsPrefix = "#/$defs/";
            const string legacyPrefix = "#/definitions/";

            string? name = null;
            if (refStr.StartsWith(defsPrefix, StringComparison.Ordinal))
                name = refStr.Substring(defsPrefix.Length);
            else if (refStr.StartsWith(legacyPrefix, StringComparison.Ordinal))
                name = refStr.Substring(legacyPrefix.Length);
            else
                return null;

            var typeName = ToPascalIdentifier(name);
            return typeName;
        }
    }
}