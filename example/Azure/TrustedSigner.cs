// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using Azure.CodeSigning;
using Azure.CodeSigning.Models;
using Azure.Core;
using ContentAuthenticity;
using ContentAuthenticity.Bindings;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace C2paSample;

class TrustedSignerConfiguration
{
    public required string EndpointUri { get; init; }

    public required string AccountName { get; init; }

    public required string CertificateProfile { get; init; }

    public SigningAlg Algorithm { get; init; }

    public string? TimeAuthorityUrl { get; init; }
}

class TrustedSigner : ISigner
{
    private readonly TrustedSignerConfiguration _config;
    private readonly CertificateProfileClient _client;

    public TrustedSigner(TokenCredential credential, TrustedSignerConfiguration config)
    {
        _config = config;
        _client = new CertificateProfileClient(credential, new Uri(_config.EndpointUri));
    }

    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        byte[] digest = GetDigest(data);

        SignRequest req = new(GetAlgorithm(), digest);
        CertificateProfileSignOperation operation = _client.StartSign(_config.AccountName, _config.CertificateProfile, req);
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
        using Stream stream = _client.GetSignCertificateChain(_config.AccountName, _config.CertificateProfile);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes, 0, bytes.Length);
        var certCollection = X509CertificateLoader.LoadPkcs12Collection(bytes, password: null);

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

    private SignatureAlgorithm GetAlgorithm()
    {
        return _config.Algorithm switch
        {
            SigningAlg.Ps256 => SignatureAlgorithm.PS256,
            SigningAlg.Ps384 => SignatureAlgorithm.PS384,
            SigningAlg.Ps512 => SignatureAlgorithm.PS512,
            SigningAlg.Es256 => SignatureAlgorithm.ES256,
            SigningAlg.Es384 => SignatureAlgorithm.ES384,
            SigningAlg.Es512 => SignatureAlgorithm.ES512,
            _ => throw new NotSupportedException($"The algorithm {_config.Algorithm} is not supported."),
        };
    }

    public SigningAlg Alg => _config.Algorithm;

    public string Certs => GetCertificates();

    public string? TimeAuthorityUrl => _config.TimeAuthorityUrl;
}