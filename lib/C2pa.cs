namespace Microsoft.ContentAuthenticity.Bindings
{

    /// <summary>
    /// Top  level SDK entry point.
    /// </summary>
    public static class C2pa
    {
        /// <summary>
        /// The version of the Sdk.
        /// </summary>
        public static string Version
        {
            get
            {
                unsafe
                {
                    return Utils.FromCString(c2pa.C2paVersion());
                }
            }
        }

        public static string[] SupportedMimeTypes
        {
            get
            {
                ulong count = 0;
                unsafe
                {
                    var buffer = c2pa.C2paReaderSupportedMimeTypes(ref count);
                    return Utils.FromCStringArray(buffer, count);
                }
            }
        }

        public static void LoadSettings(string settings)
        {
            c2pa.C2paLoadSettings(settings, "json");
            CheckError();
        }

        public static void CheckError()
        {
            string err;
            unsafe
            {
                err = Utils.FromCString(c2pa.C2paError());
            }

            if (string.IsNullOrEmpty(err)) return;

            string errType = err.Split(' ')[0];
            string errMsg = err;

            throw new C2paException(errType, errMsg);
        }
    }
}