using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

using Azure.Core;
using Azure.Identity;
using Azure.CodeSigning;
using Azure.CodeSigning.Models;

using ContentAuthenticity.Bindings;

namespace C2paSample
{
    class Program
    {
        public static void Main(string inputFile, string? outputFile = null)
        {
            Console.WriteLine("Version: {0}", C2pa.Version);
            Console.WriteLine("Supprted Extensions: {0}", string.Join(",", C2pa.SupportedMimeTypes));

            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile), "No filename was provided.");
            if (!File.Exists(inputFile))
                throw new IOException($"No file exists with the filename of {inputFile}.");

            if (outputFile == null)
                ValidateFile(inputFile);
            else
                SignFile(inputFile, outputFile);
        }

        private static void ValidateFile(string inputFile)
        {
            var reader = C2paReader.FromFile(inputFile);
            if (reader != null)
            {
                Console.WriteLine(reader.Json);
            }
            else
            {
                Console.WriteLine("No manifest found in file.");
            }
        }

        private static void SignFile(string inputFile, string outputFile)
        {
            TokenCredential credential = new DefaultAzureCredential(true);
            TrustedSigner signer = new(credential);

            var settings = """
            {
                "trust": {
                    "trust_config": "1.3.6.1.5.5.7.3.36\n1.3.6.1.4.1.311.76.59.1.9"
                },
                "verify": {
                    "verify_after_sign": false
                }
            }
            """;
            C2paSettings.Load(settings);

            ManifestDefinition manifest = new()
            {
                ClaimGeneratorInfo = { new ClaimGeneratorInfo { Name = "C# Binding test", Version = "1.0.0" } },
                Format = "jpg",
                Title = "C# Test Image",
                Assertions = { new CreativeWorkAssertion(new CreativeWorkAssertionData("http://schema.org/", "CreativeWork", [new AuthorInfo("person", "Isaiah Carrington")])) }
            };

            C2paBuilder builder = C2paBuilder.Create(manifest);
            builder.Sign(signer, inputFile, outputFile);
        }

        class TrustedSigner(TokenCredential credential) : ISigner
        {
            const string EndpointUri = "https://eus.codesigning.azure.net/";
            static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.PS384;
            const string CertificateProfile = "media-provenance-sign";
            const string AccountName = "ts-80221a56b4b24529a43e";

            private readonly CertificateProfileClient _client = new(credential, new Uri(EndpointUri));

            public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
            {

                byte[] digest = GetDigest(data);

                SignRequest req = new(Algorithm, digest);
                CertificateProfileSignOperation operation = _client.StartSign(AccountName, CertificateProfile, req);
                SignStatus status = operation.WaitForCompletion();
                status.Signature.CopyTo(hash);

                return status.Signature.Length;
            }

            private static byte[] GetDigest(ReadOnlySpan<byte> data)
            {
                byte[] digest = SHA384.HashData(data.ToArray());
                return digest;
            }

            public string GetCertificates()
            {
                Random random = new();
                byte[] hash = new byte[32];
                random.NextBytes(hash);
                byte[] digest = GetDigest(hash);

                SignRequest request = new(Algorithm, digest);
                CertificateProfileSignOperation operation = _client.StartSign(AccountName, CertificateProfile, request);
                SignStatus status = operation.WaitForCompletion();

                SignedCms cmsData = new();
                char[] cdata = Encoding.ASCII.GetChars(status.SigningCertificate);
                byte[] certificate = Convert.FromBase64CharArray(cdata, 0, cdata.Length);

                cmsData.Decode(certificate);

                StringBuilder builder = new();

                foreach (var cert in cmsData.Certificates)
                {
                    Console.WriteLine("Subject = {0} Issuer = {1} Expiry = {2}", cert.Subject, cert.Issuer, cert.GetExpirationDateString());
                    builder.AppendLine($"subject={cert.Subject}");
                    builder.AppendLine($"issuer={cert.Issuer}");
                    var data = PemEncoding.Write("CERTIFICATE", cert.RawData);
                    builder.AppendLine(new string(data));
                }

                string pem = builder.ToString();
                return pem;
            }

            public C2paSigningAlg Alg => C2paSigningAlg.Ps384;

            public string Certs => GetCertificates();

            public string? TimeAuthorityUrl => "http://timestamp.digicert.com";
        }
    }
}