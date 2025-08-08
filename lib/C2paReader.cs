// Copyright (c) 2025 Prakash Duggaraju. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace ContentAuthenticity.Bindings
{
    public partial class C2paReader
    {
        public static string[] SupportedMimeTypes
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

        partial void DisposePartial(bool disposing)
        {
            if (disposing)
            {
                c2pa.C2paReaderFree(this);
            }
        }

        public static C2paReader FromStream(Stream stream, string format)
        {
            unsafe
            {
                var reader = c2pa.C2paReaderFromStream(format, new C2paStream(stream));
                if (reader == null)
                    C2pa.CheckError();
                return reader!;
            }
        }

        public string Json
        {
            get
            {
                unsafe
                {
                    return Utils.FromCString(c2pa.C2paReaderJson(this));
                }
            }
        }

        public ManifestStore ManifestStore => ManifestStore.FromJson(Json);

        public static C2paReader FromFile(string path)
        {
            using var stream = File.OpenRead(path);
            return FromStream(stream, Utils.GetMimeTypeFromExtension(Path.GetExtension(path)));
        }
    }
}
