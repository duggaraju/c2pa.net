namespace Microsoft.ContentAuthenticity;

public sealed class StreamAdapter : IDisposable
{
    private unsafe readonly C2paStream* stream;
    private readonly GCHandle handle;

    public unsafe void Dispose()
    {
        unsafe
        {
            C2paBindings.release_stream(stream);
            handle.Free();
        }
    }

    public static unsafe implicit operator C2paStream*(StreamAdapter adapter) => adapter.stream;

    public StreamAdapter(Stream stream)
    {
        unsafe
        {
            handle = GCHandle.Alloc(stream);
            this.stream = C2paBindings.create_stream((StreamContext*)(nint)handle, &Read, &Seek, &Write, &Flush);
            if (this.stream == null)
                C2pa.CheckError();
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static unsafe nint Read(StreamContext* context, byte* data, nint len)
    {
        var handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is Stream stream)
        {
            return stream.Read(new Span<byte>(data, (int)len));
        }
        throw new ObjectDisposedException(nameof(StreamAdapter), "The C2paStream has been disposed.");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private unsafe static nint Seek(StreamContext* context, nint offset, C2paSeekMode mode)
    {
        var handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is Stream stream)
        {
            return (nint)stream.Seek(offset, ToSeekOrigin(mode));
        }
        throw new ObjectDisposedException(nameof(context), "The Stream has been disposed.");
    }

    /// <summary>
    /// Converts C2paSeekMode to SeekOrigin.
    /// </summary>
    /// <param name="mode">The C2paSeekMode to convert</param>
    /// <returns>The corresponding SeekOrigin value</returns>
    private static SeekOrigin ToSeekOrigin(C2paSeekMode mode)
    {
        return mode switch
        {
            C2paSeekMode.Start => SeekOrigin.Begin,
            C2paSeekMode.Current => SeekOrigin.Current,
            C2paSeekMode.End => SeekOrigin.End,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid C2paSeekMode value")
        };
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private unsafe static nint Write(StreamContext* context, byte* data, nint len)
    {
        var handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is Stream stream)
        {
            stream.Write(new Span<byte>(data, (int)len));
            return (nint)len;
        }
        throw new ObjectDisposedException(nameof(context), "The Stream has been disposed.");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe nint Flush(StreamContext* context)
    {
        var handle = GCHandle.FromIntPtr((nint)context);
        if (handle.Target is Stream stream)
        {
            stream.Flush();
            return 0; // Return 0 to indicate success
        }
        throw new ObjectDisposedException(nameof(context), "The Stream has been disposed.");
    }
}
