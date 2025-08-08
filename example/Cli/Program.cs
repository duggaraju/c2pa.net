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
            Console.WriteLine("C2PA .NET CLI");
            Console.WriteLine($"C2PA SDK Version: {C2pa.Version}");
            Console.WriteLine($"Supported MimeTypes: {string.Join(", ", C2pa.SupportedMimeTypes)}");
            Console.WriteLine($"Reader supported MimeTypes: {string.Join(", ", C2paReader.SupportedMimeTypes)}");
            Console.WriteLine($"Builder supported MimeTypes: {string.Join(", ", C2paBuilder.SupportedMimeTypes)}");
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

                Console.WriteLine("Manifest Store:");
                Console.WriteLine(reader.ManifestStore.ToJson());
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
            "Input file to sign");
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
            "Output file path for signed file");

        var manifestOption = new Option<FileInfo>(
            ["--manifest", "-m"],
            "Manifest definition JSON file");
        manifestOption.AddValidator(result =>
        {
            var file = result.GetValueForOption(manifestOption);
            if (file != null && !file.Exists)
            {
                result.ErrorMessage = $"Manifest file does not exist: {file.FullName}";
            }
        });

        var certOption = new Option<FileInfo>(
            ["--cert", "--certificate"],
            "Certificate file path (.pem or .crt)")
        {
        };
        certOption.AddValidator(result =>
        {
            var file = result.GetValueForOption(certOption);
            if (file != null && !file.Exists)
            {
                result.ErrorMessage = $"Certificate file does not exist: {file.FullName}";
            }
        });

        var keyOption = new Option<FileInfo>(
            ["--key", "--private-key"],
            "Private key file path (.pem or .key)")
        {
        };
        keyOption.AddValidator(result =>
        {
            var file = result.GetValueForOption(keyOption);
            if (file != null && !file.Exists)
            {
                result.ErrorMessage = $"Private key file does not exist: {file.FullName}";
            }
        });

        var tsaUrlOption = new Option<string?>(
            ["--tsa-url"],
            "Time Authority URL for timestamping")
        {
            IsRequired = false
        };

        var algorithmOption = new Option<string?>(
            ["--algorithm", "--alg"],
            "Signing algorithm (ES256, ES384, ES512, PS256, PS384, PS512, Ed25519). If not specified, will be determined from the certificate.")
        {
            IsRequired = false
        };

        signCommand.AddOption(inputOption);
        signCommand.AddOption(outputOption);
        signCommand.AddOption(manifestOption);
        signCommand.AddOption(certOption);
        signCommand.AddOption(keyOption);
        signCommand.AddOption(tsaUrlOption);

        signCommand.SetHandler((manifestFile, certFile, keyFile, input, output, tsaUrl) =>
        {
            try
            {
                Console.WriteLine($"Signing file: {input.FullName}");

                // Create or load manifest definition
                var manifestJson = File.ReadAllText(manifestFile.FullName);
                var manifest = ManifestDefinition.FromJson(manifestJson);

                Console.WriteLine("Using file-based signer with provided certificate and key");
                var certContent = File.ReadAllText(certFile.FullName);
                var keyContent = File.ReadAllText(keyFile.FullName);
                using var signer = new FileSigner(certContent, keyContent, tsaUrl, manifest.Alg);

                Console.WriteLine($"Detected/Selected signing algorithm: {signer.Alg}");

                // Create builder and sign
                using var builder = C2paBuilder.Create(manifest);

                builder.Sign(signer, input.FullName, output.FullName);

                Console.WriteLine($"Successfully signed file: {output.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to sign file: {ex.Message}");
                Environment.Exit(1);
            }
        }, manifestOption, certOption, keyOption, inputOption, outputOption, tsaUrlOption);

        return signCommand;
    }

}
