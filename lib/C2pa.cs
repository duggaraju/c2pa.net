using System.Runtime.InteropServices;
using System.Text.Json;

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

        public static string[] SupportedExtensions
        {
            get
            {
                unsafe
                {
                    ulong count = 0;
                    var buffer = c2pa.C2paReaderSupportedMimeTypes(ref count);
                    return Utils.FromCStringArray(buffer, count);
                }
            }
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

            throw new C2paException(errMsg);
        }
    }
}