// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace ContentAuthenticity;

public sealed partial class Reader : IDisposable
{
    // Native reader_with_* calls consume this handle, even when they return null.
    private unsafe C2paReader* handle;

    internal unsafe bool IsDisposed => handle == null;

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
    /// Creates a new bare <see cref="Reader"/> with default context and settings.
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
            if (!IsDisposed)
            {
                C2paBindings.free(handle);
                handle = null;
            }
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
                handle = C2paBindings.reader_with_fragment(handle, (sbyte*)formatBytes, c2paStream, fragmentStream);
                if (handle == null)
                    C2pa.CheckError();
            }
        }
        return this;
    }

    /// <summary>
    /// Configures this reader from a stream.
    /// </summary>
    public Reader WithStream(Stream stream, string format)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            {
                handle = C2paBindings.reader_with_stream(handle, (sbyte*)formatBytes, c2paStream);
                if (handle == null)
                    C2pa.CheckError();
            }
        }
        return this;
    }

    /// <summary>
    /// Configures this reader from a file path.
    /// If <paramref name="format"/> is null, the format is inferred from the
    /// file extension.
    /// </summary>
    public Reader WithFile(string path, string? format = null)
    {
        using var stream = File.OpenRead(path);
        return WithStream(stream, format ?? path.GetMimeType());
    }

    /// <summary>
    /// Configures this reader from a stream and detached manifest bytes.
    /// </summary>
    public Reader WithStreamAndManifest(Stream stream, string format, ReadOnlySpan<byte> manifest)
    {
        unsafe
        {
            using var c2paStream = new StreamAdapter(stream);
            fixed (byte* formatBytes = Encoding.UTF8.GetBytes(format))
            fixed (byte* manifestBytes = manifest)
            {
                handle = C2paBindings.reader_with_manifest_data_and_stream(handle, (sbyte*)formatBytes, c2paStream, manifestBytes, (nuint)manifest.Length);
                if (handle == null)
                    C2pa.CheckError();
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

    public string CrJson
    {
        get
        {
            unsafe
            {
                return Utils.FromCString(C2paBindings.reader_crjson(handle));
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