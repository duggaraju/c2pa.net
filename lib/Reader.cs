namespace Microsoft.ContentAuthenticity;

public sealed class Reader : IDisposable
{
    private readonly unsafe C2paReader* reader;

    private unsafe Reader(C2paReader* reader)
    {
        this.reader = reader;
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
                return Utils.FromCString(C2paBindings.reader_json(reader));
            }
        }
    }

    public bool IsEmbedded
    {
        get
        {
            unsafe
            {
                return C2paBindings.reader_is_embedded(reader) != 0;
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