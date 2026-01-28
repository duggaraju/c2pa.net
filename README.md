# c2pa.net

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
[![c2pa-rs](https://img.shields.io/badge/c2pa--rs-0.75.7-informational)](https://github.com/contentauth/c2pa-rs/releases/tag/v0.75.7)
[![NuGet](https://img.shields.io/nuget/v/ContentAuthenticity.svg)](https://www.nuget.org/packages/ContentAuthenticity/)
[![Target Framework](https://img.shields.io/badge/TFM-net10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

.NET bindings for c2pa [Rust](https://github.com/contentauth/c2pa-rs) library.

## Description

This repository provides a .NET-friendly API surface over the native `c2pa_c` C ABI exposed by the upstream c2pa-rs project.

- **Interop bindings are generated automatically**: the build runs the `clangsharppinvokegenerator` .NET tool (ClangSharp P/Invoke Generator) against the `c2pa.h` header and emits platform-specific binding files under `lib/Bindings/*`. The dispatcher in `lib/C2paBindings.cs` selects the correct platform/architecture implementation at runtime.
- **Typed models are generated from JSON Schema for safety**: the `generator/` project is a Roslyn incremental source generator that reads the C2PA JSON schemas (e.g., Builder/Reader/Settings) and emits strongly-typed C# models during compilation. This keeps the high-level API strongly typed (compile-time checks + IntelliSense) instead of relying on loosely-typed JSON strings.

## Usage

The `Reader` and `Builder` classes have **strongly-typed models generated from JSON Schema**.

### Read an asset and use the typed `ManifestStore`

```csharp
using ContentAuthenticity;
using static ContentAUthenticty.Reader;
var assetPath = "./my-image.jpg";

using var reader = Reader.FromFile(assetPath);

// Raw JSON (if you need it)
string json = reader.Json;

// Strongly-typed view of the manifest store
ManifestStore store = reader.Store;

Console.WriteLine($"Embedded: {reader.IsEmbedded}");
Console.WriteLine($"Active manifest: {store.ActiveManifest}");

store.Manifests.TryGetValue(store.ActiveManifest, out var manifest)
Console.WriteLine($"Title: {manifest.Title}");
Console.WriteLine($"Format: {manifest.Format}");

// Example: if the manifest has a thumbnail resource reference, you can fetch it
if (manifest.Thumbnail is not null)
{
   using var thumbOut = File.Create("./thumbnail.bin");
   reader.ResourceToStream(new Uri(manifest.Thumbnail.Identifier), thumbOut);
}

// Round-trip back to JSON using the same schema-driven serializer options
string roundTripped = store.ToJson();
```

### Create a typed manifest definition and sign an asset

```csharp
using ContentAuthenticity;
using static ContentAuthenticity.Builder;

// Build a minimal typed manifest definition.
// The schema requires `NoEmbed` to be set.
var definition = new ManifestDefinition
{
   NoEmbed = false,
   Title = "my-image.jpg",
   Format = "image/jpeg",
   InstanceId = Builder.GenerateInstanceID(),
   ClaimGeneratorInfo =
   [
      new Builder.ClaimGeneratorInfo { Name = "c2pa.net" }
   ],
   Assertions =
   [
      new ActionAssertion(
      [
         new ActionV1("c2pa.edited"),
      ]),
      new CreativeWorkAssertions(
         new CreativeWorkAsertionData
         {
            Type = "MyType",
            Context = new {
               "SomeName" = "SomeValue"
            }
         }
      )
   ]
};

using var builder = Builder.Create(definition);
// Optional: add extra resources that the manifest may reference (thumbnails, etc.)
builder.AddResource("thumbnail", "./thumbnail.jpg");
builder.AddIngredient(...)

// Signing requires an `ISigner` implementation (see `example/` projects for working signers).
Signer signer = Signer.FromSettings();/* Signer.From(someISigner) */

var input = "./my-image.jpg";
var output = "./my-image.signed.jpg";

// Writes a signed asset to `output` and returns the embedded manifest bytes.
byte[] manifestBytes = builder.Sign(signer, input, output);
```

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Rust toolchain](https://rustup.rs/) (for building the native c2pa-rs library)
- [Git](https://git-scm.com/) with submodule support
- [ClangSharp](https://github.com/dotnet/ClangSharp)

## Development

### 1. Clone the Repository

```bash
git clone --recurse-submodules https://github.com/duggaraju/c2pa.net.git
cd c2pa.net
```

If you've already cloned the repository without submodules, initialize them:

```bash
git submodule update --init --recursive
```

### 2. Update Submodule to Specific Release Tag

To update the c2pa-rs submodule to a specific release tag:

```bash
# Navigate to the submodule directory
cd c2pa-rs

# Fetch all tags from the remote repository
git fetch --tags

# List available tags (optional)
git tag -l

# Checkout to a specific release tag (replace v0.x.x with desired version)
git checkout v0.28.1

# Return to the root directory
cd ..

# Commit the submodule update
git add c2pa-rs
git commit -m "Update c2pa-rs submodule to v0.28.1"
```

### 3. Build the Project

#### Option 1: Using .NET CLI (Recommended)

```bash
# Build the entire solution
dotnet restore && dotnet tool restore
dotnet build

# Build
dotnet build
# or build in Release Mode.
dotnet build --configuration Release

```

#### Option 2: Using Visual Studio

1. Open `c2pa.net.sln` in Visual Studio
2. Select the desired configuration (Debug/Release) and platform (x64)
3. Build → Build Solution (Ctrl+Shift+B)

### 4. Run Tests

```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal
```

### 5. Run Example

```bash
# Navigate to the example CLI project
cd example/Cli

# Run the example
dotnet run
```
### 6. Package

Create a nuget package for publishing.

```bash
cd lib
dotnet pack
```


## Project Structure

- `lib/` - Main .NET bindings library (ContentAuthenticity.Bindings)
- `tests/` - Unit and integration tests
- `example/` - Example CLI application demonstrating usage
- `generator/` - Code generator for creating .NET bindings from Rust
- `c2pa-rs/` - Git submodule containing the Rust c2pa library

## Troubleshooting

### Common Issues

1. **Missing c2pa_c.dll**: Ensure the Rust library is built first:

   ```bash
   cd c2pa-rs
   cargo build --release -p c2pa-c-ffi --no-default-features --features "rust_native_crypto, file_io"
   ```

2. **Missing Rust Tooling**: Ensure that you have cargo installed and right  toolset present (e.g cross compiling for ARM64):

   ```bash
   rustup target add aarch64-unknown-linux-gnu
   ```

3. **Submodule not initialized**: If you see build errors related to missing Rust code:

   ```bash
   git submodule update --init --recursive
   ```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Update the submodule to the appropriate c2pa-rs version if needed
4. Make your changes
5. Run tests to ensure everything works
6. Submit a pull request
