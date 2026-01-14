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

    public static Settings? LoadSettings(string settings, string format = "json")
    {
        unsafe
        {
            fixed (byte* s = Encoding.UTF8.GetBytes(settings))
            fixed (byte* f = Encoding.UTF8.GetBytes(format))
            {
                var ret = C2paBindings.load_settings((sbyte*)s, (sbyte*)f);
                if (ret != 0)
                {
                    C2pa.CheckError();
                }
            }
        }
        if (format == "json")
            return settings.Deserialize<Settings>();
        return null;
    }

}