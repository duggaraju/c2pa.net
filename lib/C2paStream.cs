namespace Microsoft.ContentAuthenticity.Bindings
{
    public partial class StreamContext
    {
        public readonly Stream Stream;

        public StreamContext(Stream stream) : this()
        {
            Stream = stream;
        }


        public static unsafe long Read(nint context, byte* data, long len)
        {
            if (__TryGetNativeToManagedMapping(context, out var stream))
            {
                return stream.Stream.Read(new Span<byte>(data, (int)len));
            }
            throw new ObjectDisposedException(nameof(C2paStream), "The C2paStream has been disposed.");
        }

        public unsafe static long Seek(nint context, long offset, C2paSeekMode mode)
        {
            if (__TryGetNativeToManagedMapping(context, out var stream))
            {
                return stream.Stream.Seek(offset, ToSeekOrigin(mode));
            }
            throw new ObjectDisposedException(nameof(C2paStream), "The C2paStream has been disposed.");
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

        public unsafe static long Write(nint context, byte* data, long len)
        {
            if (__TryGetNativeToManagedMapping(context, out var stream))
            {
                stream.Stream.Write(new Span<byte>(data, (int)len));
                return len;
            }
            throw new ObjectDisposedException(nameof(C2paStream), "The C2paStream has been disposed.");
        }

        public static long Flush(nint context)
        {
            if (__TryGetNativeToManagedMapping(context, out var stream))
            {
                stream.Stream.Flush();
                return 0; // Return 0 to indicate success
            }
            throw new ObjectDisposedException(nameof(C2paStream), "The C2paStream has been disposed.");
        }

        partial void DisposePartial(bool disposing)
        {
            // Dispose of the stream if it is not null
            if (disposing)
            {
                Stream.Dispose();
            }
        }
    }

    public partial class C2paStream
    {
        /// <summary>
        /// Create a C2paStream object that wraps a native C# Stream.
        /// </summary>
        /// <param name="stream">The <see cref="System.IO.Stream"/> to use.</param>
        public C2paStream(Stream stream) : this()
        {
            unsafe
            {
                Reader = StreamContext.Read;
                Flusher = StreamContext.Flush;
                Seeker = StreamContext.Seek;
                Writer = StreamContext.Write;
                Context = new StreamContext(stream);
            }
        }
    }
}