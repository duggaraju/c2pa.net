# c2pa.net

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
[![c2pa-rs](https://img.shields.io/badge/c2pa--rs-0.91.0-informational)](https://github.com/contentauth/c2pa-rs/releases/tag/c2pa-c-ffi-v0.91.0)
[![NuGet](https://img.shields.io/nuget/v/ContentAuthenticity.svg)](https://www.nuget.org/packages/ContentAuthenticity/)
[![Target Framework](https://img.shields.io/badge/TFM-net10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

.NET bindings for c2pa [Rust](https://github.com/contentauth/c2pa-rs) library.

## Description

This repository provides a .NET-friendly API surface over the native `c2pa_c` C ABI exposed by the upstream c2pa-rs project.

- **Interop bindings are generated automatically**: the build runs the `clangsharppinvokegenerator` .NET tool (ClangSharp P/Invoke Generator) against the `c2pa.h` header and emits platform-specific binding files under `lib/Bindings/*`. The dispatcher in `lib/C2paBindings.cs` selects the correct platform/architecture implementation at runtime.
- **Typed models are generated from JSON Schema for safety**: a cross-platform MSBuild target runs pinned quicktype to convert the C2PA Builder, Reader, Settings, and standard assertion schemas into committed `System.Text.Json` models. This keeps normal builds independent of Node.js while retaining compile-time checks and IntelliSense.

## Usage

The `Reader` and `Builder` classes have **strongly-typed models generated from JSON Schema**.

Reading and signing are now driven by an immutable `Context`, produced from a
`ContextBuilder`. The context owns shared configuration (settings, signer, HTTP
resolver, progress callback, etc.) and is then handed to a `Reader` or
`Builder`. This keeps per-operation classes thin and lets you reuse one
configured context across many reads/signs.

### Read an asset and use the typed `ManifestStore`

```csharp
using ContentAuthenticity;

var assetPath = "./my-image.jpg";

// Build a context. Settings/HTTP resolver are optional; defaults are fine for
// most reads.
using var contextBuilder = new ContextBuilder();
// contextBuilder.SetHttpResolver(new HttpResolver()); // optional, for remote manifests
using var context = contextBuilder.Build();

// Attach the context to a Reader and feed it the asset.
using var reader = new Reader(context).WithFile(assetPath);

// Raw JSON (if you need it)
string json = reader.Json;

// Strongly-typed view of the manifest store
ManifestStore store = reader.Store;

Console.WriteLine($"Embedded: {reader.IsEmbedded}");
Console.WriteLine($"Active manifest: {store.ActiveManifest}");

if (store.Manifests.TryGetValue(store.ActiveManifest, out var manifest))
{
    Console.WriteLine($"Title: {manifest.Title}");
    Console.WriteLine($"Format: {manifest.Format}");

    // Example: if the manifest has a thumbnail resource reference, fetch it.
    if (manifest.Thumbnail is not null)
    {
        using var thumbOut = File.Create("./thumbnail.bin");
        reader.ResourceToStream(new Uri(manifest.Thumbnail.Identifier), thumbOut);
    }
}

// Round-trip back to JSON using the same schema-driven serializer options.
string roundTripped = store.ToJson();
```

### Create a typed manifest definition and sign an asset

```csharp
using ContentAuthenticity;

// Build a minimal typed manifest definition.
var definition = new ManifestDefinition
{
    Title = "my-image.jpg",
    Format = "image/jpeg",
    InstanceId = Builder.GenerateInstanceID(),
    ClaimGeneratorInfo =
    [
        new ClaimGeneratorInfo { Name = "c2pa.net" }
    ],
};

// Provide a signer using either:
// - SignerInfo (simplest when you already have cert/key PEM strings), or
// - a custom class implementing ICallbackSigner (for external/HSM/remote signing).
// This example uses SignerInfo.
var signer = new SignerInfo(
    SigningAlg.Ps256,
    File.ReadAllText("./certs/rs256.pub"),
    File.ReadAllText("./certs/rs256.pem"),
    new Uri("https://timestamp.digicert.com"));

// Configure a context with the signer (and any other shared options).
using var contextBuilder = new ContextBuilder();
contextBuilder.SetSigner(signer);
contextBuilder.SetHttpResolver(new HttpResolver()); // optional
using var context = contextBuilder.Build();

// Attach the context to a Builder and apply the typed manifest definition.
using var builder = new Builder(context).WithDefinition(definition);
builder.AddAction(new ContentAuthenticity.Schema.ActionItemV2 { Action = "c2pa.edited" });

// Optional: add extra resources that the manifest may reference (thumbnails, etc.)
builder.AddResource("thumbnail", "./thumbnail.jpg");

var input = "./my-image.jpg";
var output = "./my-image.signed.jpg";

// Sign using the signer configured on the context.
builder.Sign(input, output);
```

### Advanced: Sign with an identity credential holder

Implement `ICredentialHolder` for callback-backed CAWG identity credentials, such
as credentials issued by an identity claims aggregator. Set `SignatureType` to the
credential's CAWG signature type and `ReserveSize` to its maximum signature size
in bytes. `Sign` receives the CBOR-encoded signer payload, writes the credential
signature into the supplied buffer, and returns the number of bytes written.

Pass your implementation as `credentialHolder` when configuring the context:

```csharp
var options = new SigningOptions
{
    C2paSigner = signer,
    ReferencedAssertions = ["c2pa.actions"],
    Roles = ["creator"],
    CredentialHolder = credentialHolder
};
using var contextBuilder = new ContextBuilder();
contextBuilder.SetSigner(options);
using var context = contextBuilder.Build();
```

The context retains the callback for its native lifetime. Keep any resources used
by your implementation available and usable from the signing thread until the
context and its builders are disposed. Negative return values, oversized results,
or callback exceptions cause signing to fail with `C2paException`. Supplying an
`IdentitySigner` as well emits both X.509 and callback-backed identity assertions.

### Advanced: Embed a manifest with your own asset writer

Prefer `Builder.Sign` unless you need to control embedding and patching yourself.
The context-based embeddable API replaces the older DataHash signing workflow:

| Older API | Preferred workflow |
| --- | --- |
| `DataHashedPlaceholder(reservedSize, format)` | `Placeholder(format)` uses the Context signer's reserve size. |
| `SignDataHashedEmbeddable(signer, dataHashJson, format, asset)` | Set exclusions, call `UpdateHashFromStream`, then `SignEmbeddable`. |
| `FormatEmbeddable(format, rawManifest)` | `builder.ComposeManifest(format, rawManifest)` for existing raw manifests. Placeholder and signed embeddable bytes are already formatted. |

The pinned native SDK deprecates these older APIs, so their managed wrappers have
been removed. Migrate signing workflows as shown above. Use `ComposeManifest` only
for raw `application/c2pa` bytes, not the already composed output of `Placeholder`
or `SignEmbeddable`.

This JPEG example uses the `signer` configured above and an unsigned input asset:

```csharp
using var contextBuilder = new ContextBuilder();
contextBuilder.SetSigner(signer);
using var context = contextBuilder.Build();
using var builder = new Builder(context).WithDefinition(new ManifestDefinition
{
    Title = "my-image.jpg",
    Format = "image/jpeg",
    InstanceId = Builder.GenerateInstanceID(),
    ClaimGeneratorInfo = [new ClaimGeneratorInfo { Name = "c2pa.net" }],
});
builder.AddAction(new ContentAuthenticity.Schema.ActionItemV2
{
    Action = "c2pa.created",
    DigitalSourceType = "http://cv.iptc.org/newscodes/digitalsourcetype/digitalCapture"
});

const string format = "image/jpeg";
const int insertOffset = 2;
byte[] source = File.ReadAllBytes("./my-image.jpg");
if (source.Length < 2 || source[0] != 0xff || source[1] != 0xd8)
    throw new InvalidDataException("Expected a JPEG SOI marker.");

byte[] placeholder = builder.Placeholder(format);
using var asset = new MemoryStream();
asset.Write(source.AsSpan(0, insertOffset));
asset.Write(placeholder);
asset.Write(source.AsSpan(insertOffset));

builder.SetDataHashExclusions([(insertOffset, (ulong)placeholder.Length)]);
asset.Position = 0;
builder.UpdateHashFromStream(asset, format);
byte[] signedManifest = builder.SignEmbeddable(format);
if (signedManifest.Length != placeholder.Length)
    throw new InvalidOperationException("Signed manifest must fit the placeholder exactly.");

asset.Position = insertOffset;
asset.Write(signedManifest);
File.WriteAllBytes("./my-image.signed.jpg", asset.ToArray());
```

Keep the same builder from placeholder creation through signing: it retains the
reserved size. Both returned byte arrays are already formatted for embedding.
Embedding is format-specific; the JPEG insertion above is not suitable for other
containers or replacing an existing manifest. For MP4/BMFF, use a container-aware
writer to insert the placeholder box and maintain offsets, then hash, sign, and
patch it. Omit `SetDataHashExclusions` for BMFF because the native SDK excludes
the manifest box automatically.

See the [native embeddable API guide](c2pa-rs/docs/embeddable-api.md) for details.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Rust toolchain](https://rustup.rs/) (for building the native c2pa-rs library)
- [Git](https://git-scm.com/) with submodule support
- [ClangSharp](https://github.com/dotnet/ClangSharp)
- [Node.js 20.19 or later](https://nodejs.org/) (only when regenerating schema models)

### Alpine Linux / musl

The NuGet package includes a separate `linux-musl-x64` native library for Alpine
and compatible musl-based x64 systems. Publish applications with
`dotnet publish -r linux-musl-x64`; the normal `linux-x64` native asset requires
glibc and cannot be substituted. Use a supported .NET 10 musl runtime and its
system dependencies, including musl's `libgcc` package for unwind support. The
native library is cross-compiled against musl 1.2.5 and exercised on Alpine 3.22.

TODO: Add `linux-musl-arm64` with a matching cross-toolchain and a native ARM64
Alpine package-consumer test. GNU/Linux ARM64 support does not imply musl ARM64 support.

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

The c2pa-rs submodule is automatically updated by a GitHub Actions workflow that monitors the upstream repository for new C FFI release tags matching the pattern `c2pa-c-ffi-vx.x.x`. When a new tag is detected, the workflow:

1. Updates the submodule to point to the new tag
2. Creates a pull request with the update details
3. Includes the tag information and release notes link in the PR description

The workflow runs automatically every 6 hours, but can also be triggered manually:

```bash
# Go to Actions tab in GitHub UI
# Select "Update c2pa-rs Submodule" workflow
# Click "Run workflow"
# Optionally specify a specific tag (e.g., c2pa-c-ffi-v0.75.21)
```

**Manual Update (if needed):**

If you need to manually update the submodule to a specific release tag:

```bash
# Navigate to the submodule directory
cd c2pa-rs

# Fetch all tags from the remote repository
git fetch --tags

# List available c2pa-c-ffi tags
git tag -l 'c2pa-c-ffi-v*'

# Checkout to a specific release tag (replace with desired version)
git checkout c2pa-c-ffi-v0.75.21

# Return to the root directory
cd ..

# Commit the submodule update
git add c2pa-rs
git commit -m "Update c2pa-rs submodule to c2pa-c-ffi-v0.75.21"
```

### 2a. Update Schemas and Regenerate Models

When you move to a newer C2PA spec or c2pa-rs release, use this workflow to refresh schema-based types and standard assertions.

1. Update the c2pa-rs submodule (as above) to the target tag.
2. Update the standard assertions schema to the new spec version by replacing:

    - `schemas/c2pa-standard-assertions.schema.json`

3. Build once to export the Builder, Reader, and Settings schemas from c2pa-rs:

```bash
dotnet build
```

4. Regenerate all committed C# models. The project uses quicktype 26.0.0 by default:

```bash
dotnet build lib/ContentAuthenticity.csproj -t:GenerateModels
```

To regenerate C# from schemas that have already been exported, without rebuilding the Rust library:

```bash
dotnet build lib/ContentAuthenticity.csproj -t:GenerateSchemaModels
```

To test another quicktype release explicitly:

```bash
dotnet build lib/ContentAuthenticity.csproj -t:GenerateModels -p:QuicktypeVersion=26.0.0
```

5. Validate and inspect changes:

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~SchemaModelRoundTripTests"
dotnet test --filter "FullyQualifiedName~StandardAssertionsSchemaSyncTests"
dotnet test --filter "FullyQualifiedName~ReaderTests.JsonRoundTrip_ShouldPreserveDataIntegrity"
```

6. Commit updated artifacts together, including `lib/Schema/*.cs`.

Note: `lib/Bindings/*.cs` and `lib/Schema/*.cs` are generated and should not be edited manually.

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

#### Cross-compiling musl x64 on Ubuntu

`BuildLinuxMusl` defaults to `false` for local builds. The GitHub Actions Ubuntu
x64 job explicitly enables it to build and package both GNU and musl binaries.
No container is used to compile Rust; Docker is only needed for the Alpine
package-consumer check.

For an optional local cross-build, install the Rust musl target and the complete
[Bootlin musl toolchain](https://toolchains.bootlin.com/releases_x86-64.html)
from the repository root. The installer pins `x86-64--musl--stable-2025.08-1`
(GCC 14.3.0, musl 1.2.5) and verifies its SHA-256 before extraction. It requires
an x64 Linux host, `curl`, `tar`, `xz`, and `sha256sum`, and an empty destination:

```bash
rustup target add x86_64-unknown-linux-musl
bash .github/scripts/install-musl-toolchain.sh artifacts/toolchains/musl
export PATH="$PWD/artifacts/toolchains/musl/bin:$PATH"
dotnet build --configuration Release -p:BuildLinuxMusl=true
dotnet pack lib/ContentAuthenticity.csproj --configuration Release --no-build \
  --output artifacts/packages -p:BuildLinuxMusl=true -p:PackageVersion=0.0.0-local
docker run --rm --platform linux/amd64 \
  --volume "$PWD:/workspace" --workdir /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0-alpine3.22 \
  sh .github/scripts/test-musl-package.sh 0.0.0-local
```

Pass `BuildLinuxMusl=true` consistently to build and pack. `--no-build` never
compiles Rust; it requires the existing output for the selected configuration
under `c2pa-rs/target/x86_64-unknown-linux-musl/`. Missing artifacts or unsupported
build hosts fail explicitly. Ordinary builds do not need any musl tooling.

The upstream `c2pa-rs` submodule does not track `Cargo.lock`. Like the normal
native build, the musl build lets Cargo create it on the first build and reuse
it on subsequent builds.

The cross-build invokes Cargo separately with `-C target-feature=-crt-static`,
preserving existing `RUSTFLAGS` (or `CARGO_ENCODED_RUSTFLAGS`). Target-specific
linker, C/C++ compiler, and archiver settings apply only to this invocation.
All target dependencies use musl, while build scripts and procedural macros run
on the Ubuntu host. Installing Ubuntu's `musl-tools` alone is not equivalent:
the compiler's unwind/runtime libraries must also be built for musl.

`IncludeLinuxMusl` defaults to the value of `BuildLinuxMusl`. To package an
externally built binary without cross-compiling, leave `BuildLinuxMusl=false`
and set `IncludeLinuxMusl=true` plus `LinuxMuslLibraryPath` to that binary.
ClangSharp and schema generation continue using the normal host targets.
CI consumes the produced NuGet package in Alpine and checks eager symbol
resolution and a sign/read round trip for both portable and RID-specific
published output before uploading it.


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
