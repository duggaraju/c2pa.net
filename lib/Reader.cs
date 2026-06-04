// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

public sealed partial class Reader : IDisposable
{
    private unsafe C2paReader* handle;

    /// <summary>
    /// Creates a new <see cref="Reader"/> bound to the given <see cref="Context"/>.
    /// </summary>
    public Reader(Context context)
    {
        unsafe
        {
            var reader = C2paBindings.reader_from_context(context);
            if (reader == null)
                C2pa.CheckError();
            handle = reader;
        }
    }

    /// <summary>
    /// Creates a new bare <see cref="Reader"/> with default settings.
    /// Use <see cref="Reader(Context)"/> to construct a reader bound to an
    /// explicit <see cref="Context"/>.
    /// </summary>
    public Reader()
    {
        unsafe
        {
            var reader = C2paBindings.reader_new();
            if (reader == null)
                C2pa.CheckError();
            handle = reader;
        }
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
            C2paBindings.free(handle);
        }
    }

    /// <summary>
    /// Configures this reader with a fragmented BMFF asset by supplying both
    /// the main asset stream and a fragment stream. Used for fragmented MP4
    /// where manifests are stored in separate fragments.
    /// </summary>
    public Reader WithFragment(Stream stream, Stream fragment, string format)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            using var fragmentStream = new StreamAdapter(fragment);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                var reader = C2paBindings.reader_with_fragment(handle, (sbyte*)formatBytes, c2paStream, fragmentStream);
                if (reader == null)
                    C2pa.CheckError();
                this.handle = reader;
            }
        }
        return this;
    }

    public Reader WithStream(Stream stream, string format)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                var reader = C2paBindings.reader_with_stream(handle, (sbyte*)formatBytes, c2paStream);
                if (reader == null)
                    C2pa.CheckError();
                this.handle = reader;
            }
        }
        return this;
    }

    public Reader WithFile(string path)
    {
        using var stream = File.OpenRead(path);
        return WithStream(stream, path.GetMimeType());
    }

    public Reader WithStreamAndManifest(Stream stream, string format, ReadOnlySpan<byte> manifest)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            fixed (byte* manifestBytes = manifest)
            {
                var reader = C2paBindings.reader_with_manifest_data_and_stream(handle, (sbyte*)formatBytes, c2paStream, manifestBytes, (nuint)manifest.Length);
                if (reader == null)
                    C2pa.CheckError();
                this.handle = reader;
            }
        }
        return this;
    }

    public string Json
    {
        get
        {
            unsafe
            {
                return Utils.FromCString(C2paBindings.reader_json(handle));
            }
        }
    }

    public bool IsEmbedded
    {
        get
        {
            unsafe
            {
                return C2paBindings.reader_is_embedded(handle) != 0;
            }
        }
    }

    public Uri? RemoteUrl
    {
        get
        {
            unsafe
            {
                var urlPtr = C2paBindings.reader_remote_url(handle);
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
                return Utils.FromCString(C2paBindings.reader_detailed_json(handle));
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
                var result = C2paBindings.reader_resource_to_stream(handle, (sbyte*)uriBytes, c2paStream);
                if (result != 0)
                    C2pa.CheckError();
            }
        }
    }

    public ManifestStore Store => Json.FromJson<ManifestStore>();
}