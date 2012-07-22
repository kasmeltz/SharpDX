// Copyright (c) 2010-2012 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace SharpDX.IO
{
    /// <summary>
    /// Windows File Helper.
    /// </summary>
    public class NativeFileStream : Stream
    {
        private bool canRead;
        private bool canWrite;
        private bool canSeek;
        private IntPtr handle;
        private long position;

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeFileStream"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="fileMode">The file mode.</param>
        /// <param name="access">The access mode.</param>
        /// <param name="share">The share mode.</param>
        public NativeFileStream(string fileName, NativeFileMode fileMode, NativeFileAccess access, NativeFileShare share = NativeFileShare.Read)
        {
#if WIN8METRO
            handle = NativeFile.Create(fileName, access, share, fileMode, IntPtr.Zero);
#else
            handle = NativeFile.Create(fileName, access, share, IntPtr.Zero, fileMode, NativeFileOptions.None, IntPtr.Zero);
#endif
            if (handle == new IntPtr(-1))
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Unable to open file {0}", fileName), Marshal.GetLastWin32Error());

            canRead = 0 != (access & NativeFileAccess.Read);
            canWrite = 0 != (access & NativeFileAccess.Write);

            // TODO how setup correctly canSeek flags? 
            // Kernel32.GetFileType(SafeFileHandle handle); is not available on Win8Metro
            canSeek = true;

        }

        /// <inheritdoc/>
        public override void Flush()
        {
            if (!NativeFile.FlushFileBuffers(handle))
                throw new IOException("Unable to flush stream", Marshal.GetLastWin32Error());
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition;
            if (!NativeFile.SetFilePointerEx(handle, offset, out newPosition, origin))
                throw new IOException("Unable to seek to this position", Marshal.GetLastWin32Error());
            position = newPosition;
            return position;
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            long newPosition;
            if (!NativeFile.SetFilePointerEx(handle, value, out newPosition, SeekOrigin.Begin))
                throw new IOException("Unable to seek to this position", Marshal.GetLastWin32Error());
            if (!NativeFile.SetEndOfFile(handle))
                throw new IOException("Unable to set the new length", Marshal.GetLastWin32Error());

            if (position < value)
            {
                Seek(position, SeekOrigin.Begin);
            } 
            else
            {
                Seek(0, SeekOrigin.End);
            }
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            unsafe
            {
                fixed (void* pbuffer = buffer)
                    return Read((IntPtr) pbuffer, offset, count);
            }
        }

        /// <summary>
        /// Reads a block of bytes from the stream and writes the data in a given buffer.
        /// </summary>
        /// <param name="buffer">When this method returns, contains the specified buffer with the values between offset and (offset + count - 1) replaced by the bytes read from the current source. </param>
        /// <param name="offset">The byte offset in array at which the read bytes will be placed. </param>
        /// <param name="count">The maximum number of bytes to read. </param>
        /// <exception cref="ArgumentNullException">array is null. </exception>
        /// <returns>The total number of bytes read into the buffer. This might be less than the number of bytes requested if that number of bytes are not currently available, or zero if the end of the stream is reached.</returns>
        public int Read(IntPtr buffer, int offset, int count)
        {
            if (buffer == IntPtr.Zero)
                throw new ArgumentNullException("buffer");

            int numberOfBytesRead;
            unsafe
            {
                void* pbuffer = (byte*) buffer + offset;
                {
                    if (!NativeFile.ReadFile(handle, (IntPtr)pbuffer, count, out numberOfBytesRead, IntPtr.Zero))
                        throw new IOException("Unable to read from file", Marshal.GetLastWin32Error());
                }
                position += numberOfBytesRead;
            }
            return numberOfBytesRead;
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            unsafe
            {
                fixed (void* pbuffer = buffer)
                    Write((IntPtr)pbuffer, offset, count);
            }
        }

        /// <summary>
        /// Writes a block of bytes to this stream using data from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer containing data to write to the stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream. </param>
        /// <param name="count">The number of bytes to be written to the current stream. </param>
        public void Write(IntPtr buffer, int offset, int count)
        {
            if (buffer == IntPtr.Zero)
                throw new ArgumentNullException("buffer");

            int numberOfBytesWritten;
            unsafe
            {
                void* pbuffer = (byte*) buffer + offset;
                {
                    if (!NativeFile.WriteFile(handle, (IntPtr)pbuffer, count, out numberOfBytesWritten, IntPtr.Zero))
                        throw new IOException("Unable to write to file", Marshal.GetLastWin32Error());
                }
                position += numberOfBytesWritten;
            }
        }

        /// <inheritdoc/>
        public override bool CanRead
        {
            get
            {
                return canRead;
            }
        }

        /// <inheritdoc/>
        public override bool CanSeek
        {
            get
            {
                return canSeek;
            }
        }

        /// <inheritdoc/>
        public override bool CanWrite
        {
            get
            {
                return canWrite;
            }
        }

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                long length;
                if (!NativeFile.GetFileSizeEx(handle, out length))
                    throw new IOException("Unable to get file length", Marshal.GetLastWin32Error());
                return length;
            }
        }

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
                position = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            Utilities.CloseHandle(handle);
            handle = IntPtr.Zero;
            base.Dispose(disposing);
        }
    }
}
