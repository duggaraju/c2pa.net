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

    public static void SetError(string type, string message)
    {
        unsafe
        {
            var ptr = Marshal.StringToCoTaskMemUTF8($"{type}: {message}");
            try
            {
                C2paBindings.error_set_last((sbyte*)ptr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }
    }

}