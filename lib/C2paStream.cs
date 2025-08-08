// Copyright (c) 2025 Prakash Duggaraju. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace ContentAuthenticity.Bindings
{
    public partial class C2paStream
    {
        private readonly Stream _stream;

        public C2paStream(Stream stream) : this()
        {
            _stream = stream;
            unsafe
            {
                Reader = (context, data, len) => Read(new Span<byte>(data, (int)len));
                Flusher = (_) => Flush();
                Seeker = (_, offset, mode) => Seek(offset, mode);
                Writer = (context, data, len) => Write(new ReadOnlySpan<byte>(data, (int)len));
            }
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

        private unsafe long Seek(long offset, C2paSeekMode mode)
        {
            long position = _stream.Seek(offset, ToSeekOrigin(mode));
            return position;
        }

        private unsafe long Read(Span<byte> buffer)
        {
            int bytesRead = _stream.Read(buffer);
            return bytesRead;
        }

        private unsafe long Write(ReadOnlySpan<byte> data)
        {
            _stream.Write(data);
            return data.Length;
        }

        public long Flush()
        {
            _stream.Flush();
            return 0; // Return 0 to indicate success
        }

        partial void DisposePartial(bool disposing)
        {
            // Dispose of the stream if it is not null
            if (disposing)
            {
                _stream.Dispose();
            }
        }
    }
}
