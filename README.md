# c2pa.net

.NET bindings for c2pa Rust library.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Rust toolchain](https://rustup.rs/) (for building the native c2pa-rs library)
- [Git](https://git-scm.com/) with submodule support

## Getting Started

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
dotnet build

# Build in Release mode
dotnet build --configuration Release

# Build for specific platform (x64)
dotnet build --configuration Release --arch x64
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

### Package

Create a nuget package for publishing.

```bash
cd lib
dotnet pack -c Release -p RuntimeIdentitifer=linx-x64 # or win-x64 for Windows.
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

2. **Platform target mismatch**: The project is configured for x64. Ensure your build environment matches:

   ```bash
   dotnet build --arch x64
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
