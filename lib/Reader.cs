namespace ContentAuthenticity;

public sealed class Reader : IDisposable
{
    private readonly nint reader;

    private unsafe Reader(C2paReader* reader)
    {
        this.reader = (nint)reader;
    }

    public static string[] SupportedMimeTypes
    {
        get
        {
            unsafe
            {
                nuint count = 0;
                var buffer = C2paBindings.reader_supported_mime_types(&count);
                return Utils.FromCStringArray(buffer, (uint)count);
            }
        }
    }

        public void Dispose()
    {
        unsafe
        {
            C2paBindings.reader_free((C2paReader*)reader);
        }
    }

    public StreamAdapter ToStream(string uri = "")
    {
        StreamAdapter stream = new StreamAdapter(new MemoryStream());
        
        unsafe
        {

            var bytes = Encoding.UTF8.GetBytes(uri);
            fixed (byte* p = bytes)
            {
                var ret = C2paBindings.reader_resource_to_stream((C2paReader*)reader, (sbyte*)p, stream);
                if (ret == -1)
                    C2pa.CheckError();
            }
        }

        return stream;
    }

    public static Reader FromStream(Stream stream, string format)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                var reader = C2paBindings.reader_from_stream((sbyte*)formatBytes, c2paStream);
                if (reader == null)
                    C2pa.CheckError();
                return new Reader(reader);
            }
        }
    }

    public string Json
    {
        get
        {
            unsafe
            {
                return Utils.FromCString(C2paBindings.reader_json((C2paReader*)reader));
            }
        }
    }

    public ManifestStore ManifestStore => ManifestStore.FromJson(Json);

    public static Reader FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return FromStream(stream, Utils.GetMimeTypeFromExtension(Path.GetExtension(path)));
    }
}
