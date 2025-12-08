// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using Azure.Identity;
using ContentAuthenticity;
using ContentAuthenticity.Bindings;

namespace C2paSample;

partial class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Version: {0}", C2pa.Version);
        Console.WriteLine("Supprted Extensions: {0}", string.Join(",", C2pa.SupportedMimeTypes));

        if (args.Length != 2)
            throw new ArgumentNullException(nameof(args), "No filename was provided.");

        string inputFile = args[0];
        string? outputFile = args.Length > 1 ? args[1] : null;

        if (string.IsNullOrEmpty(inputFile))
            throw new ArgumentNullException(nameof(inputFile), "No filename was provided.");
        if (!File.Exists(inputFile))
            throw new IOException($"No file exists with the filename of {inputFile}.");

        var json = File.ReadAllText("settings.json");
        var settings = Settings.FromJson(json);
        settings.Load();

        if (outputFile == null)
            ValidateFile(inputFile);
        else
            SignFile(inputFile, outputFile);
    }

    private static void ValidateFile(string inputFile)
    {
        var reader = Reader.FromFile(inputFile);
        if (reader != null)
        {
            Console.WriteLine(reader.ManifestStore.ToJson());
        }
        else
        {
            Console.WriteLine("No manifest found in file.");
        }
    }

    private static void SignFile(string inputFile, string outputFile)
    {
        var credential = new DefaultAzureCredential(true);
        var config = new TrustedSignerConfiguration
        {
            EndpointUri = "https://eus.codesigning.azure.net/",
            AccountName = "rai-provenance-sign",
            CertificateProfile = "rai-poc-provenance-sign",
            Algorithm = SigningAlg.Ps384,
            TimeAuthorityUrl = "http://timestamp.digicert.com",
        };
        TrustedSigner signer = new(credential, config);

        ManifestDefinition manifest = new(Utils.GetMimeType(inputFile))
        {
            ClaimGeneratorInfo = { new ClaimGeneratorInfo("C# Binding test", "1.0.0") },
            Format = "jpg",
            Title = "C# Test Image",
            Assertions = [
                new CreativeWorkAssertion(
                    new CreativeWorkAssertionData("http://schema.org/", "CreativeWork")
                    {
                        Value = new Dictionary<string, object>
                        {
                            { "person", "Isaiah Carrington" }
                        }
                    })
            ]
        };

        var builder = Builder.Create(manifest);
        builder.Sign(signer, inputFile, outputFile);

        ValidateFile(outputFile);
    }
}