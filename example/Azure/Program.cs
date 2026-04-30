// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using Azure.Identity;
using ContentAuthenticity;
using ContentAuthenticity.Bindings;
using static ContentAuthenticity.Builder;

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
        using var contextBuilder = ContextBuilder.Create();
        contextBuilder.SetHttpResolver(new HttpResolver());
        contextBuilder.SetSettings(json);

        var credential = new DefaultAzureCredential(true);
        var config = new TrustedSignerConfiguration
        {
            EndpointUri = "https://eus.codesigning.azure.net/",
            AccountName = "rai-provenance-sign",
            CertificateProfile = "rai-poc-provenance-sign",
            Algorithm = SigningAlg.Ps384,
            TimeAuthorityUrl = new("http://timestamp.digicert.com"),
        };
        TrustedSigner signer = new(credential, config);
        contextBuilder.SetSigner(signer);

        using var context = contextBuilder.Build();
        if (outputFile == null)
            ValidateFile(context, inputFile);
        else
            SignFile(context, inputFile, outputFile);
    }

    private static void ValidateFile(Context context, string inputFile)
    {
        using var reader = Reader.FromContext(context).WithFile(inputFile);
        if (reader != null)
        {
            Console.WriteLine(reader.Store.ToJson());
        }
        else
        {
            Console.WriteLine("No manifest found in file.");
        }
    }

    private static void SignFile(Context context, string inputFile, string outputFile)
    {

        ManifestDefinition manifest = new()
        {
            Format = Utils.GetMimeType(inputFile),
            ClaimGeneratorInfo = new()
            {
                new ClaimGeneratorInfo
                {
                   Name = "C# Binding test",
                   Version = "1.0.0"
                }
            },
            Title = "C# Test Image",
            Assertions = new()
            {
                new CreativeWorkAssertion(
                    new CreativeWorkAssertionData("http://schema.org/", "CreativeWork")
                    {
                        Value = new Dictionary<string, object>
                        {
                            { "person", "Isaiah Carrington" }
                        }
                    })
            },
            TimestampManifestLabels = []
        };

        using var builder = Builder.FromContext(context).WithDefinition(manifest);
        builder.Sign(inputFile, outputFile);

        ValidateFile(context, outputFile);
    }
}