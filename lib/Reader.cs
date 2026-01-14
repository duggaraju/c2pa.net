// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

[JsonSchema("../generator/Reader.schema.json", "ManifestStore")]
public sealed partial class Reader : IDisposable
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
            C2paBindings.reader_free(reader);
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

    public static Reader FromStreamAndManifest(Stream stream, string format, ReadOnlySpan<byte> manifest)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            fixed (byte* manifestBytes = manifest)
            {
                var reader = C2paBindings.reader_from_manifest_data_and_stream((sbyte*)formatBytes, c2paStream, manifestBytes, (nuint)manifest.Length);
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

    public Uri? RemoteUrl
    {
        get
        {
            unsafe
            {
                var urlPtr = C2paBindings.reader_remote_url(reader);
                return urlPtr == null ? null : new Uri(Utils.FromCString(urlPtr));
            }
        }
    }

    public string DetailedJson
    {
        get
        {
            unsafe
            {
                return Utils.FromCString(C2paBindings.reader_detailed_json(reader));
            }
        }
    }

    public void ResourceToStream(Uri uri, Stream stream)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            fixed (byte* uriBytes = Encoding.UTF8.GetBytes(uri.ToString()))
            {
                var result = C2paBindings.reader_resource_to_stream(reader, (sbyte*)uriBytes, c2paStream);
                if (result != 0)
                    C2pa.CheckError();
            }
        }
    }

    public ManifestStore Store => Json.Deserialize<ManifestStore>();

    public static Reader FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return FromStream(stream, path.GetMimeType());
    }
}