// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

using System.Security.Cryptography;

public static class Utils
{

    public unsafe static string FromCString(sbyte* ptr, bool freeResource = true)
    {
        if (ptr == null)
        {
            return string.Empty;
        }
        var value = Marshal.PtrToStringUTF8((nint)ptr)!;
        if (freeResource)
            C2paBindings.free(ptr);

        return value;
    }

    public unsafe static string[] FromCStringArray(sbyte** ptr, nuint count)
    {
        if (count <= 0)
        {
            return [];
        }
        var values = new string[count];
        for (nuint i = 0; i < count; i++)
        {
            values[i] = FromCString(ptr[i], freeResource: false);
        }
        C2paBindings.free_string_array(ptr, count);
        return values;
    }

    public static string GetMimeTypeFromExtension(string extension)
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

    public static string GetMimeType(this string Filename) => GetMimeTypeFromExtension(Path.GetExtension(Filename));

    public static SigningAlg GetAlgorithm(this ECDsa ecdsa)
    {
        var keySize = ecdsa.KeySize;
        return keySize switch
        {
            256 => SigningAlg.Es256,
            384 => SigningAlg.Es384,
            521 => SigningAlg.Es512,
            _ => SigningAlg.Es256 // Default to ES256
        };
    }

    public static SigningAlg GetAlgorithm(this RSA rsa)
    {
        var keySize = rsa.KeySize;
        return keySize switch
        {
            256 => SigningAlg.Ps256,
            384 => SigningAlg.Ps384,
            521 => SigningAlg.Ps512,
            _ => SigningAlg.Ps256 // Default to PS256
        };
    }

    public static HashAlgorithmName GetHash(this AsymmetricAlgorithm rsa)
    {
        return rsa.KeySize switch
        {
            256 => HashAlgorithmName.SHA256,
            384 => HashAlgorithmName.SHA384,
            521 or 512 => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Unsupported algorithm {rsa} for hashing.")
        };
    }
}