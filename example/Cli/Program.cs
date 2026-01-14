// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using ContentAuthenticity;
using ContentAuthenticity.Bindings;
using System.CommandLine;
using System.Text.Json;
using static ContentAuthenticity.Builder;

namespace Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("C2PA .NET CLI - Content Provenance and Authenticity tool");

        // Add version command
        var versionCommand = new Command("version", "Display the C2PA SDK version");
        versionCommand.SetAction((_) =>
        {
            Console.WriteLine("C2PA .NET CLI");
            Console.WriteLine($"C2PA SDK Version: {C2pa.Version}");
            Console.WriteLine($"Supported MimeTypes: {string.Join(", ", C2pa.SupportedMimeTypes)}");
            Console.WriteLine($"Reader supported MimeTypes: {string.Join(", ", Reader.SupportedMimeTypes)}");
            Console.WriteLine($"Builder supported MimeTypes: {string.Join(", ", Builder.SupportedMimeTypes)}");
        });
        rootCommand.Subcommands.Add(versionCommand);

        // Add read command
        var readCommand = CreateReadCommand();
        rootCommand.Subcommands.Add(readCommand);

        // Add sign command
        var signCommand = CreateSignCommand();
        rootCommand.Subcommands.Add(signCommand);

        try
        {
            var result = rootCommand.Parse(args);
            return await result.InvokeAsync();
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
            "-i", "--input"
            )
        {
            Description = "Input file path to read C2PA data from",
            Required = true
        };
        inputOption.Validators.Add(result =>
        {
            var file = result.GetValue(inputOption);
            if (file != null && !file.Exists)
            {
                result.AddError($"Input file does not exist: {file.FullName}");
            }
        });

        var prettyOption = new Option<bool>(
            "--pretty", "-p"
            )
        {
            Description = "Pretty print JSON output"
        };

        readCommand.Options.Add(inputOption);
        readCommand.Options.Add(prettyOption);
        readCommand.SetAction(result =>
        {
            var input = result.GetRequiredValue(inputOption);
            var pretty = result.GetRequiredValue(prettyOption);
            Console.WriteLine($"Reading C2PA data from: {input.FullName}");

            using var reader = Reader.FromFile(input.FullName);
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
            Console.WriteLine(reader.Store.ToJson());
        });

        return readCommand;
    }

    private static Command CreateSignCommand()
    {
        var signCommand = new Command("sign", "Sign a file with C2PA manifest");

        var inputOption = new Option<FileInfo>(
            "--input", "-i"
            )
        {
            Description = "Input file to sign",
            Required = true
        };

        inputOption.Validators.Add(result =>
        {
            var file = result.GetValue(inputOption);
            if (file != null && !file.Exists)
            {
                result.AddError($"Input file does not exist: {file.FullName}");
            }
        });

        var outputOption = new Option<FileInfo>(
            "--output", "-o")
        {
            Description = "Output file path for signed file",
            Required = true
        };

        var manifestOption = new Option<FileInfo>(
            "--manifest", "-m"
            )
        {
            Description = "Manifest definition JSON file",
            Required = true
        };
        manifestOption.Validators.Add(result =>
        {
            var file = result.GetValue(manifestOption);
            if (file != null && !file.Exists)
            {
                result.AddError($"Manifest file does not exist: {file.FullName}");
            }
        });

        var certOption = new Option<FileInfo>(
            "-c", "--certificate"
            )
        {
            Description = "Certificate file path (.pem or .crt)",
            Required = true
        };
        certOption.Validators.Add(result =>
        {
            var file = result.GetValue(certOption);
            if (file != null && !file.Exists)
            {
                result.AddError($"Certificate file does not exist: {file.FullName}");
            }
        });

        var keyOption = new Option<FileInfo>(
            "-k", "--private-key"
            )
        {
            Description = "Private key file path (.pem or .key)"
        };
        keyOption.Validators.Add(result =>
        {
            var file = result.GetValue(keyOption);
            if (file != null && !file.Exists)
            {
                result.AddError($"Private key file does not exist: {file.FullName}");
            }
        });

        var tsaUrlOption = new Option<Uri?>(
            "--tsa-url"
            )
        {
            Description = "Time Authority URL for timestamping",
        };

        signCommand.Add(inputOption);
        signCommand.Add(outputOption);
        signCommand.Add(manifestOption);
        signCommand.Add(certOption);
        signCommand.Add(keyOption);
        signCommand.Add(tsaUrlOption);

        signCommand.SetAction(result =>
        {
            var input = result.GetRequiredValue(inputOption);
            var output = result.GetRequiredValue(outputOption);
            var manifestFile = result.GetRequiredValue(manifestOption);
            var keyFile = result.GetRequiredValue(keyOption);
            var certFile = result.GetRequiredValue(certOption);
            var tsaUrl = result.GetValue(tsaUrlOption);
            Console.WriteLine($"Signing file: {input.FullName}");

            // Create or load manifest definition
            var manifestJson = File.ReadAllText(manifestFile.FullName);
            var manifest = manifestJson.Deserialize<ManifestDefinition>();

            Console.WriteLine("Using file-based signer with provided certificate and key");
            var certContent = File.ReadAllText(certFile.FullName);
            var keyContent = File.ReadAllText(keyFile.FullName);
            using var signer = new FileSigner(certContent, keyContent, tsaUrl);

            Console.WriteLine($"Detected signing algorithm: {signer.Alg}");

            // Create builder and sign
            using var builder = Builder.Create(manifest);

            builder.Sign(signer, input.FullName, output.FullName);

            Console.WriteLine($"Successfully signed file: {output.FullName}");
        });

        return signCommand;
    }

}