// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using ContentAuthenticity;
using ContentAuthenticity.Bindings;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace Cli;

/// <summary>
/// File-based signer for production use that implements cryptographic signing
/// using certificate and private key files in PEM format.
/// </summary>
/// <remarks>
/// This signer supports the following algorithms:
/// - ES256, ES384, ES512 (ECDSA with P-256, P-384, P-521 curves)
/// - PS256, PS384, PS512 (RSA-PSS with SHA-256, SHA-384, SHA-512)
/// - Ed25519 (Edwards-curve signing, not yet implemented)
///
/// The algorithm is inferred from the certificate/public key (and key size where applicable).
/// </remarks>
internal sealed class FileSigner : ISigner, IDisposable
{
    private readonly string _certs;
    private readonly Uri? _tsaUrl;
    private readonly SigningAlg _algorithm;
    private readonly AsymmetricAlgorithm _signingKey;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the FileSigner class.
    /// </summary>
    /// <param name="certs">Certificate chain in PEM format</param>
    /// <param name="privateKey">Private key in PEM format</param>
    /// <param name="tsaUrl">Optional timestamp authority URL for timestamping</param>
    /// <exception cref="InvalidOperationException">Thrown when certificate parsing fails or algorithm is incompatible</exception>
    /// <exception cref="NotSupportedException">Thrown when certificate type is not supported</exception>
    public FileSigner(string certs, string privateKey, Uri? tsaUrl = null)
    {
        _certs = certs;
        _tsaUrl = tsaUrl;

        // Parse the certificate to determine the algorithm and prepare the signing key
        (_algorithm, _signingKey) = ParseCertificateAndKey(certs, privateKey);
    }

    /// <inheritdoc/>
    public SigningAlg Alg => _algorithm;

    /// <inheritdoc/>
    public string Certs => _certs;

    /// <inheritdoc/>
    public Uri? TimeAuthorityUrl => _tsaUrl;

    /// <summary>
    /// Signs the provided data using the configured algorithm and private key.
    /// </summary>
    /// <param name="data">Data to be signed</param>
    /// <param name="signature">Buffer to store the signature</param>
    /// <returns>The length of the signature in bytes</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the signer has been disposed</exception>
    /// <exception cref="InvalidOperationException">Thrown when signing fails</exception>
    /// <exception cref="NotSupportedException">Thrown when the algorithm is not supported</exception>
    public int Sign(ReadOnlySpan<byte> data, Span<byte> signature)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            byte[] signatureBytes = _algorithm switch
            {
                SigningAlg.Es256 => SignWithECDsa(data, HashAlgorithmName.SHA256),
                SigningAlg.Es384 => SignWithECDsa(data, HashAlgorithmName.SHA384),
                SigningAlg.Es512 => SignWithECDsa(data, HashAlgorithmName.SHA512),
                SigningAlg.Ps256 => SignWithRSA(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
                SigningAlg.Ps384 => SignWithRSA(data, HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
                SigningAlg.Ps512 => SignWithRSA(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
                SigningAlg.Ed25519 => SignWithEd25519(data),
                _ => throw new NotSupportedException($"Signing algorithm {_algorithm} is not supported")
            };

            if (signatureBytes.Length > signature.Length)
            {
                throw new InvalidOperationException($"Signature buffer too small. Required: {signatureBytes.Length}, Available: {signature.Length}");
            }

            signatureBytes.CopyTo(signature);
            return signatureBytes.Length;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to sign data: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_signingKey is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _disposed = true;
        }
    }

    private byte[] SignWithECDsa(ReadOnlySpan<byte> data, HashAlgorithmName hashAlgorithm)
    {
        if (_signingKey is not ECDsa ecdsa)
        {
            throw new InvalidOperationException("Expected ECDsa key for ECDSA signing");
        }

        return ecdsa.SignData(data, hashAlgorithm);
    }

    private byte[] SignWithRSA(ReadOnlySpan<byte> data, HashAlgorithmName hashAlgorithm, RSASignaturePadding padding)
    {
        if (_signingKey is not RSA rsa)
        {
            throw new InvalidOperationException("Expected RSA key for RSA signing");
        }

        return rsa.SignData(data, hashAlgorithm, padding);
    }

    private byte[] SignWithEd25519(ReadOnlySpan<byte> data)
    {
        // For Ed25519, we need to use a different approach
        // .NET doesn't have built-in Ed25519 support for signing arbitrary data
        // We could potentially use the C2PA library's Ed25519 function here
        throw new NotImplementedException("Ed25519 signing is not yet implemented. Use ECDSA or RSA algorithms instead.");
    }

    private static (SigningAlg algorithm, AsymmetricAlgorithm signingKey) ParseCertificateAndKey(string certsPem, string privateKeyPem)
    {
        try
        {
            // Parse the certificate to determine the key type
            var cert = ParseCertificateFromPem(certsPem);
            var publicKey = cert.PublicKey;

            // Parse the private key
            AsymmetricAlgorithm privateKey;
            SigningAlg algorithm;

            if (IsEd25519Certificate(publicKey))
            {
                // TODO: Support Ed25519 signing when available in .NET or via native APIs.
                throw new NotSupportedException("Ed25519 certificates are not yet supported by this signer.");
            }
            else if (IsECCertificate(publicKey))
            {
                privateKey = ParseECPrivateKeyFromPem(privateKeyPem);
                algorithm = DetermineECAlgorithm(cert, (ECDsa)privateKey);
            }
            else if (IsRSACertificate(publicKey))
            {
                privateKey = ParseRSAPrivateKeyFromPem(privateKeyPem);
                algorithm = DetermineRSAAlgorithm(cert, (RSA)privateKey);
            }
            else
            {
                throw new NotSupportedException($"Unsupported certificate key type: {publicKey.Oid.Value}");
            }

            return (algorithm, privateKey);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse certificate and key: {ex.Message}", ex);
        }
    }

    private static X509Certificate2 ParseCertificateFromPem(string certPem)
    {
        // Remove PEM headers and whitespace, then convert from base64
        var lines = certPem.Split('\n')
            .Where(line => !line.StartsWith("-----"))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line));

        var base64 = string.Join("", lines);
        var certBytes = Convert.FromBase64String(base64);

        return X509CertificateLoader.LoadCertificate(certBytes);
    }

    private static ECDsa ParseECPrivateKeyFromPem(string privateKeyPem)
    {
        var ecdsa = ECDsa.Create();

        // Try different PEM formats
        try
        {
            // Try PKCS#8 format first
            ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(ExtractPemContent(privateKeyPem)), out _);
        }
        catch
        {
            try
            {
                // Try EC private key format
                ecdsa.ImportECPrivateKey(Convert.FromBase64String(ExtractPemContent(privateKeyPem)), out _);
            }
            catch
            {
                // Try importing from PEM directly (if supported)
                ecdsa.ImportFromPem(privateKeyPem);
            }
        }

        return ecdsa;
    }

    private static RSA ParseRSAPrivateKeyFromPem(string privateKeyPem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return rsa;
    }

    private static string ExtractPemContent(string pem)
    {
        var lines = pem.Split('\n')
            .Where(line => !line.StartsWith("-----"))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line));

        return string.Join("", lines);
    }

    private static bool IsECCertificate(PublicKey publicKey)
    {
        return publicKey.Oid.Value == "1.2.840.10045.2.1"; // EC public key OID
    }

    private static bool IsRSACertificate(PublicKey publicKey)
    {
        return publicKey.Oid.Value == "1.2.840.113549.1.1.1"; // RSA public key OID
    }

    private static bool IsEd25519Certificate(PublicKey publicKey)
    {
        return publicKey.Oid.Value == "1.3.101.112"; // Ed25519 public key OID
    }

    private static SigningAlg DetermineECAlgorithm(X509Certificate2 cert, ECDsa ecdsa)
    {
        var keySize = ecdsa.KeySize;
        var fromKeySize = keySize switch
        {
            256 => SigningAlg.Es256,
            384 => SigningAlg.Es384,
            521 => SigningAlg.Es512, // P-521 uses 521 bits, not 512
            _ => (SigningAlg?)null
        };

        if (fromKeySize.HasValue)
            return fromKeySize.Value;

        // Fallback: infer from the certificate's signature algorithm hash.
        return cert.SignatureAlgorithm?.Value switch
        {
            "1.2.840.10045.4.3.2" => SigningAlg.Es256, // ecdsa-with-SHA256
            "1.2.840.10045.4.3.3" => SigningAlg.Es384, // ecdsa-with-SHA384
            "1.2.840.10045.4.3.4" => SigningAlg.Es512, // ecdsa-with-SHA512
            _ => SigningAlg.Es256
        };
    }

    private static SigningAlg DetermineRSAAlgorithm(X509Certificate2 cert, RSA _)
    {
        // Infer based on the hash used by the certificate signature when available.
        // Default to PS256 (RSA-PSS + SHA-256).
        return cert.SignatureAlgorithm?.Value switch
        {
            "1.2.840.113549.1.1.11" => SigningAlg.Ps256, // sha256WithRSAEncryption
            "1.2.840.113549.1.1.12" => SigningAlg.Ps384, // sha384WithRSAEncryption
            "1.2.840.113549.1.1.13" => SigningAlg.Ps512, // sha512WithRSAEncryption
            _ => SigningAlg.Ps256
        };
    }
}