namespace Microsoft.ContentAuthenticity.Bindings
{
    public partial class C2paReader
    {
        partial void DisposePartial(bool disposing)
        {
            if (disposing)
            {
                c2pa.C2paReaderFree(this);
                C2pa.CheckError();
            }
        }

        public static C2paReader FromStream(Stream stream, string format)
        {
            unsafe
            {
                var reader = c2pa.C2paReaderFromStream(format, new C2paStream(stream));
                C2pa.CheckError();
                return reader;
            }
        }

        public unsafe string Json => Utils.FromCString(c2pa.C2paReaderJson(this));

        public ManifestStore ManifestStore => ManifestStore.FromJson(Json);

        public static C2paReader FromFile(string path)
        {
            using var stream = File.OpenRead(path);
            return FromStream(stream, Utils.GetMimeTypeFromExtension(Path.GetExtension(path)));
        }
    }
}
