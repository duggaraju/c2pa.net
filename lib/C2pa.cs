namespace Microsoft.ContentAuthenticity;


/// <summary>
/// Top  level SDK entry point.
/// </summary>
public static class C2pa
{
    /// <summary>
    /// The version of the Sdk.
    /// </summary>
    public static string Version { get; } = GetVersion();

    private unsafe static string GetVersion()
    {
        return Utils.FromCString(C2paBindings.version());
    }

    public static string[] SupportedMimeTypes
    {
        get
        {
            nuint count = 0;
            unsafe
            {
                var buffer = C2paBindings.reader_supported_mime_types(&count);
                return Utils.FromCStringArray(buffer, count);
            }
        }
    }

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
}