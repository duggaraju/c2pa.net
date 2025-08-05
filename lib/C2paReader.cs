namespace Microsoft.ContentAuthenticity.Bindings
{
    public partial class C2paReader
    {
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
            return FromStream(stream, Path.GetExtension(path).Substring(1));
        }
    }
}
