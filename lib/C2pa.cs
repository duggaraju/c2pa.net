// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;


/// <summary>
/// Top  level SDK entry point.
/// </summary>
public static partial class C2pa
{
    /// <summary>
    /// The version of the Sdk.
    /// </summary>
    public static string Version { get; } = GetVersion();

    private unsafe static string GetVersion()
    {
        return Utils.FromCString(C2paBindings.version());
    }

    public static string[] SupportedMimeTypes => Reader.SupportedMimeTypes;

    public static void CheckError()
    {
        string err;
        unsafe
        {
            err = Utils.FromCString(C2paBindings.error());
        }

        if (string.IsNullOrEmpty(err)) return;

        string errType = err.Split(' ')[0];
        string errMsg = err;

        throw new C2paException(errType, errMsg);
    }

    /// <summary>
    /// Converts a raw binary C2PA manifest (in <c>application/c2pa</c> format)
    /// into an embeddable representation suitable for the given asset format.
    /// </summary>
    /// <param name="format">MIME type or extension of the target asset (e.g. <c>image/jpeg</c>).</param>
    /// <param name="manifest">The raw manifest bytes.</param>
    /// <returns>The embeddable manifest bytes for the requested format.</returns>
    public static byte[] FormatEmbeddable(string format, ReadOnlySpan<byte> manifest)
    {
        unsafe
        {
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            fixed (byte* manifestBytes = manifest)
            {
                byte* result = null;
                var ret = C2paBindings.format_embeddable((sbyte*)formatBytes, manifestBytes, (nuint)manifest.Length, &result);
                if (ret == -1)
                    CheckError();
                var bytes = new byte[ret];
                if (ret > 0 && result != null)
                {
                    Marshal.Copy((nint)result, bytes, 0, bytes.Length);
                    C2paBindings.free(result);
                }
                return bytes;
            }
        }
    }
}