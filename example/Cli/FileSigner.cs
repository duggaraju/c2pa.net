using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ContentAuthenticity.Bindings;

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
/// The algorithm is automatically detected from the certificate type and key size,
/// or can be explicitly specified during construction.
/// </remarks>
internal class FileSigner : ISigner, IDisposable
{
    private readonly string _certs;
    private readonly string _privateKey;
    private readonly string? _tsaUrl;
    private readonly C2paSigningAlg _algorithm;
    private readonly object _signingKey;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the FileSigner class.
    /// </summary>
    /// <param name="certs">Certificate chain in PEM format</param>
    /// <param name="privateKey">Private key in PEM format</param>
    /// <param name="tsaUrl">Optional timestamp authority URL for timestamping</param>
    /// <param name="preferredAlgorithm">Optional preferred signing algorithm. If not specified, will be auto-detected from the certificate.</param>
    /// <exception cref="InvalidOperationException">Thrown when certificate parsing fails or algorithm is incompatible</exception>
    /// <exception cref="NotSupportedException">Thrown when certificate type is not supported</exception>
    public FileSigner(string certs, string privateKey, string? tsaUrl = null, C2paSigningAlg? preferredAlgorithm = null)
    {
        _certs = certs;
        _privateKey = privateKey;
        _tsaUrl = tsaUrl;

        // Parse the certificate to determine the algorithm and prepare the signing key
        (_algorithm, _signingKey) = ParseCertificateAndKey(certs, privateKey, preferredAlgorithm);
    }

    /// <inheritdoc/>
    public C2paSigningAlg Alg => _algorithm;

    /// <inheritdoc/>
    public string Certs => _certs;

    /// <inheritdoc/>
    public string? TimeAuthorityUrl => _tsaUrl;

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
                C2paSigningAlg.Es256 => SignWithECDsa(data, HashAlgorithmName.SHA256),
                C2paSigningAlg.Es384 => SignWithECDsa(data, HashAlgorithmName.SHA384),
                C2paSigningAlg.Es512 => SignWithECDsa(data, HashAlgorithmName.SHA512),
                C2paSigningAlg.Ps256 => SignWithRSA(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
                C2paSigningAlg.Ps384 => SignWithRSA(data, HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
                C2paSigningAlg.Ps512 => SignWithRSA(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
                C2paSigningAlg.Ed25519 => SignWithEd25519(data),
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

    private static (C2paSigningAlg algorithm, object signingKey) ParseCertificateAndKey(string certsPem, string privateKeyPem, C2paSigningAlg? preferredAlgorithm = null)
    {
        try
        {
            // Parse the certificate to determine the key type
            var cert = ParseCertificateFromPem(certsPem);
            var publicKey = cert.PublicKey;

            // Parse the private key
            object privateKey;
            C2paSigningAlg algorithm;

            if (IsECCertificate(publicKey))
            {
                privateKey = ParseECPrivateKeyFromPem(privateKeyPem);
                algorithm = preferredAlgorithm ?? DetermineECAlgorithm((ECDsa)privateKey);

                // Validate that the preferred algorithm is compatible with EC keys
                if (preferredAlgorithm.HasValue && !IsECAlgorithm(preferredAlgorithm.Value))
                {
                    throw new InvalidOperationException($"Algorithm {preferredAlgorithm} is not compatible with EC certificates. Use ES256, ES384, ES512, or Ed25519.");
                }
            }
            else if (IsRSACertificate(publicKey))
            {
                privateKey = ParseRSAPrivateKeyFromPem(privateKeyPem);
                algorithm = preferredAlgorithm ?? DetermineRSAAlgorithm((RSA)privateKey);

                // Validate that the preferred algorithm is compatible with RSA keys
                if (preferredAlgorithm.HasValue && !IsRSAAlgorithm(preferredAlgorithm.Value))
                {
                    throw new InvalidOperationException($"Algorithm {preferredAlgorithm} is not compatible with RSA certificates. Use PS256, PS384, or PS512.");
                }
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

        return new X509Certificate2(certBytes);
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

    private static C2paSigningAlg DetermineECAlgorithm(ECDsa ecdsa)
    {
        var keySize = ecdsa.KeySize;
        return keySize switch
        {
            256 => C2paSigningAlg.Es256,
            384 => C2paSigningAlg.Es384,
            521 => C2paSigningAlg.Es512, // P-521 uses 521 bits, not 512
            _ => C2paSigningAlg.Es256 // Default to ES256
        };
    }

    private static C2paSigningAlg DetermineRSAAlgorithm(RSA rsa)
    {
        // For RSA, we default to PS256 (PSS padding with SHA-256)
        // Could be made configurable based on requirements
        return C2paSigningAlg.Ps256;
    }

    private static bool IsECAlgorithm(C2paSigningAlg algorithm)
    {
        return algorithm is C2paSigningAlg.Es256 or C2paSigningAlg.Es384 or C2paSigningAlg.Es512 or C2paSigningAlg.Ed25519;
    }

    private static bool IsRSAAlgorithm(C2paSigningAlg algorithm)
    {
        return algorithm is C2paSigningAlg.Ps256 or C2paSigningAlg.Ps384 or C2paSigningAlg.Ps512;
    }
}