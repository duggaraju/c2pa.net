using Azure.CodeSigning;
using Azure.CodeSigning.Models;
using Azure.Core;
using Azure.Identity;
using ContentAuthenticity;
using ContentAuthenticity.Bindings;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace C2paSample;

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
        var reader = Reader.FromFile(inputFile);
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

        var json = File.ReadAllText("settings.json");
        var settings = Settings.FromJson(json);
        settings.Load();

        ManifestDefinition manifest = new()
        {
            ClaimGeneratorInfo = { new ClaimGeneratorInfo("C# Binding test", "1.0.0") },
            Format = "jpg",
            Title = "C# Test Image",
            Assertions = { new CreativeWorkAssertion(new CreativeWorkAssertionData("http://schema.org/", "CreativeWork", [new AuthorInfo("person", "Isaiah Carrington")])) }
        };

        var builder = Builder.Create(manifest);
        builder.Sign(signer, inputFile, outputFile);
    }

    class TrustedSigner(TokenCredential credential) : ISigner
    {
        const string EndpointUri = "https://eus.codesigning.azure.net/";
        static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.PS384;
        const string CertificateProfile = "rai-poc-provenance-sign";
        const string AccountName = "rai-provenance-sign";

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
            using Stream stream = _client.GetSignCertificateChain(AccountName, CertificateProfile);
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            var certCollection = new X509Certificate2Collection();
            certCollection.Import(bytes);

            List<X509Certificate2> sortedCerts;
            if (certCollection.Count == 1)
            {
                // Special case: single certificate in chain
                sortedCerts = certCollection.Cast<X509Certificate2>().ToList();
            }
            else
            {
                // Build hash tables for O(1) lookup by subject and issuer names
                var certsBySubject = certCollection.Cast<X509Certificate2>()
                    .ToDictionary(cert => cert.SubjectName.Name, cert => cert);
                var certsByIssuer = certCollection.Cast<X509Certificate2>()
                    .GroupBy(cert => cert.IssuerName.Name)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Find leaf certificate (no other cert has this as issuer, excluding self-signed)
                var leafCert = certCollection.Cast<X509Certificate2>()
                    .FirstOrDefault(cert => cert.SubjectName.Name != cert.IssuerName.Name && 
                                       !certsByIssuer.ContainsKey(cert.SubjectName.Name));
                // Build chain in single pass following issuer links
                sortedCerts = new List<X509Certificate2>();
                var currentCert = leafCert;

                while (currentCert != null && currentCert.SubjectName.Name != currentCert.IssuerName.Name)
                {
                    sortedCerts.Add(currentCert);
                    certsBySubject.TryGetValue(currentCert.IssuerName.Name, out currentCert);
                }
            }


            StringBuilder builder = new();
            foreach (var cert in sortedCerts)
            {
                // Console.WriteLine("Subject = {0} Issuer = {1} Expiry = {2}", cert.Subject, cert.Issuer, cert.GetExpirationDateString());
                builder.AppendLine(cert.ExportCertificatePem());
            }

            return builder.ToString();
        }

        public SigningAlg Alg => SigningAlg.Ps384;

        public string Certs => GetCertificates();

        public string? TimeAuthorityUrl => "http://timestamp.digicert.com";
    }
}