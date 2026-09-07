using System;
using System.IO;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// A memory stream that refuses to grow past a fixed size. Used wherever data from an email
    /// is decoded or inflated, so a crafted attachment cannot exhaust memory: a decode that
    /// passes the limit throws <see cref="InvalidDataException"/> instead.
    /// </summary>
    public sealed class BoundedMemoryStream : MemoryStream
    {
        private readonly long _maxLength;

        public BoundedMemoryStream(long maxLength)
        {
            if (maxLength < 0)
            {
                throw new ArgumentOutOfRangeException("maxLength");
            }
            _maxLength = maxLength;
        }

        public long MaxLength
        {
            get { return _maxLength; }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureRoom(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureRoom(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureRoom(1);
            base.WriteByte(value);
        }

        public override void SetLength(long value)
        {
            if (value > _maxLength)
            {
                throw Exceeded(value);
            }
            base.SetLength(value);
        }

        private void EnsureRoom(int count)
        {
            long end = Position + count;
            if (end > _maxLength)
            {
                throw Exceeded(end);
            }
        }

        private InvalidDataException Exceeded(long attempted)
        {
            return new InvalidDataException(string.Format("The data is larger than the {0:N0} byte limit ({1:N0} bytes or more).", _maxLength, attempted));
        }
    }
}
