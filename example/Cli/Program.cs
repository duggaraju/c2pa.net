using System.CommandLine;
using System.Text.Json;
using Microsoft.ContentAuthenticity.Bindings;

namespace Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("C2PA .NET CLI - Content Provenance and Authenticity tools");

        // Add version command
        var versionCommand = new Command("version", "Display the C2PA SDK version");
        versionCommand.SetHandler(() =>
        {
            Console.WriteLine($"C2PA .NET CLI");
            Console.WriteLine($"C2PA SDK Version: {C2pa.Version}");
            Console.WriteLine($"Supported Extensions: {string.Join(", ", C2pa.SupportedExtensions)}");
        });
        rootCommand.AddCommand(versionCommand);

        // Add read command
        var readCommand = CreateReadCommand();
        rootCommand.AddCommand(readCommand);

        // Add sign command
        var signCommand = CreateSignCommand();
        rootCommand.AddCommand(signCommand);

        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (C2paException ex)
        {
            Console.Error.WriteLine($"C2PA Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static Command CreateReadCommand()
    {
        var readCommand = new Command("read", "Read and display C2PA manifest data from a file");
        
        var inputOption = new Option<FileInfo>(
            ["--input", "-i"],
            "Input file path to read C2PA data from")
        {
            IsRequired = true
        };
        inputOption.AddValidator(result =>
        {
            var file = result.GetValueForOption(inputOption);
            if (file != null && !file.Exists)
            {
                result.ErrorMessage = $"Input file does not exist: {file.FullName}";
            }
        });

        var prettyOption = new Option<bool>(
            ["--pretty", "-p"],
            "Pretty print JSON output")
        {
            IsRequired = false
        };

        readCommand.AddOption(inputOption);
        readCommand.AddOption(prettyOption);
        readCommand.SetHandler((FileInfo input, bool pretty) =>
        {
            try
            {
                Console.WriteLine($"Reading C2PA data from: {input.FullName}");
                
                using var reader = C2paReader.FromFile(input.FullName);
                var json = reader.Json;
                
                if (pretty)
                {
                    // Parse and re-format JSON for pretty printing
                    using var document = JsonDocument.Parse(json);
                    var options = new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    };
                    json = JsonSerializer.Serialize(document.RootElement, options);
                }
                
                Console.WriteLine("C2PA Manifest Data:");
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read C2PA data: {ex.Message}");
                Environment.Exit(1);
            }
        }, inputOption, prettyOption);

        return readCommand;
    }

    private static Command CreateSignCommand()
    {
        var signCommand = new Command("sign", "Sign a file with C2PA manifest");
        
        var inputOption = new Option<FileInfo>(
            ["--input", "-i"],
            "Input file to sign")
        {
            IsRequired = true
        };
        inputOption.AddValidator(result =>
        {
            var file = result.GetValueForOption(inputOption);
            if (file != null && !file.Exists)
            {
                result.ErrorMessage = $"Input file does not exist: {file.FullName}";
            }
        });

        var outputOption = new Option<FileInfo>(
            ["--output", "-o"],
            "Output file path for signed file")
        {
            IsRequired = true
        };

        var manifestOption = new Option<FileInfo>(
            ["--manifest", "-m"],
            "Manifest definition JSON file")
        {
            IsRequired = false
        };

        var titleOption = new Option<string>(
            ["--title", "-t"],
            "Title for the manifest")
        {
            IsRequired = false
        };

        var authorOption = new Option<string>(
            ["--author", "-a"],
            "Author name for the manifest")
        {
            IsRequired = false
        };

        var claimGeneratorOption = new Option<string>(
            ["--claim-generator", "-c"],
            "Claim generator name")
        {
            IsRequired = false
        };

        signCommand.AddOption(inputOption);
        signCommand.AddOption(outputOption);
        signCommand.AddOption(manifestOption);
        signCommand.AddOption(titleOption);
        signCommand.AddOption(authorOption);
        signCommand.AddOption(claimGeneratorOption);

        signCommand.SetHandler((FileInfo input, FileInfo output, FileInfo? manifestFile, string? title, string? author, string? claimGenerator) =>
        {
            try
            {
                Console.WriteLine($"Signing file: {input.FullName}");
                
                // Create or load manifest definition
                ManifestDefinition manifest;
                if (manifestFile != null && manifestFile.Exists)
                {
                    var manifestJson = File.ReadAllText(manifestFile.FullName);
                    manifest = ManifestDefinition.FromJson(manifestJson);
                }
                else
                {
                    // Create a basic manifest
                    manifest = new ManifestDefinition(GetMimeTypeFromExtension(input.Extension))
                    {
                        Title = title ?? $"Signed {input.Name}",
                        ClaimGeneratorInfo = [new ClaimGeneratorInfo(claimGenerator ?? "C2PA .NET CLI", "1.0.0")]
                    };

                    // Add author assertion if provided
                    if (!string.IsNullOrEmpty(author))
                    {
                        CreativeWorkAssertion creativeWorkAssertion = new(
                            new CreativeWorkAssertionData
                            {
                                Authors = [new AuthorInfo("@Person", author)]
                            }
                        );
                        manifest.Assertions.Add(creativeWorkAssertion);
                    }
                }

                // Create a demo signer (not for production use)
                Console.WriteLine("Warning: Using demo/test signer (not for production)");
                var signer = new DemoSigner();

                // Create builder and sign
                var settings = C2paBuilder.CreateBuilderSettings(claimGenerator ?? "C2PA .NET CLI");
                using var builder = new C2paBuilder(settings, signer, manifest);
                
                if (!string.IsNullOrEmpty(title))
                {
                    builder.SetTitle(title);
                }

                builder.Sign(input.FullName, output.FullName);
                
                Console.WriteLine($"Successfully signed file: {output.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to sign file: {ex.Message}");
                Environment.Exit(1);
            }
        }, inputOption, outputOption, manifestOption, titleOption, authorOption, claimGeneratorOption);

        return signCommand;
    }

    private static string GetMimeTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".tiff" or ".tif" => "image/tiff",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };
    }
}

// Demo signer for testing (not for production use)
internal class DemoSigner : ISigner
{
    public C2paSigningAlg Alg => C2paSigningAlg.Ed25519;
    public string Certs => GenerateDemoCertificate();
    public string? TimeAuthorityUrl => null;

    public int Sign(ReadOnlySpan<byte> data, Span<byte> signature)
    {
        // This is a demo implementation - not cryptographically secure
        var hash = System.Security.Cryptography.SHA256.HashData(data);
        if (signature.Length < hash.Length)
            return -1;
        
        hash.CopyTo(signature);
        return hash.Length;
    }

    private static string GenerateDemoCertificate()
    {
        return """
        -----BEGIN CERTIFICATE-----
        MIIBkTCB+wIJAKZW8y6v1Zv4MA0GCSqGSIb3DQEBCwUAMBoxGDAWBgNVBAMMD0My
        UEEgRGVtbyBTaWduZXIwHhcNMjQwMTAxMDAwMDAwWhcNMjUwMTAxMDAwMDAwWjAa
        MRgwFgYDVQQDDA9DMlBBIERlbW8gU2lnbmVyMFwwDQYJKoZIhvcNAQEBBQADSwAw
        SAJBAKZdtGXvZH8Lf8H4R+6YYf5nV5z9L3BXHZqJ8r8X9aE2nS6Z4q1Y4v0C5+p4
        K8bV2a8c7fE3O7H6rJ8y9p8qXCsCAwEAATANBgkqhkiG9w0BAQsFAANBAE/q8u9d
        bY5y2N7rO8f6P9qW5pE4pZ1x7k8D9fT2vX3sE4m2kQ8v6r9yH4F8cN5oQ8nZ4zJ8
        R7xK2q6M4k8sJ6Y=
        -----END CERTIFICATE-----
        """;
    }
}

// File-based signer for production use
internal class FileSigner : ISigner
{
    private readonly string _certs;
    private readonly string _privateKey;
    private readonly string? _tsaUrl;

    public FileSigner(string certs, string privateKey, string? tsaUrl = null)
    {
        _certs = certs;
        _privateKey = privateKey;
        _tsaUrl = tsaUrl;
    }

    public C2paSigningAlg Alg => C2paSigningAlg.Es256; // Default to ES256
    public string Certs => _certs;
    public string? TimeAuthorityUrl => _tsaUrl;

    public int Sign(ReadOnlySpan<byte> data, Span<byte> signature)
    {
        // This would implement actual cryptographic signing
        // For now, return a placeholder implementation
        throw new NotImplementedException("File-based signing requires cryptographic implementation");
    }
}