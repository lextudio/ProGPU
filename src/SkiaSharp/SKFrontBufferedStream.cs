using System.Buffers;
using System.Runtime.InteropServices;

namespace SkiaSharp;

public class SKFrontBufferedStream : Stream
{
    public const int DefaultBufferSize = 4096;

    private readonly long _totalBufferSize;
    private readonly long _totalLength;
    private readonly bool _disposeStream;
    private Stream? _underlyingStream;
    private long _currentOffset;
    private long _bufferedSoFar;
    private byte[]? _internalBuffer;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _totalLength;

    public override long Position
    {
        get => _currentOffset;
        set => Seek(value, SeekOrigin.Begin);
    }

    public SKFrontBufferedStream(Stream stream)
        : this(stream, DefaultBufferSize, disposeUnderlyingStream: false)
    {
    }

    public SKFrontBufferedStream(Stream stream, long bufferSize)
        : this(stream, bufferSize, disposeUnderlyingStream: false)
    {
    }

    public SKFrontBufferedStream(Stream stream, bool disposeUnderlyingStream)
        : this(stream, DefaultBufferSize, disposeUnderlyingStream)
    {
    }

    public SKFrontBufferedStream(Stream stream, long bufferSize, bool disposeUnderlyingStream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _underlyingStream = stream;
        _totalBufferSize = bufferSize;
        _totalLength = stream.CanSeek ? stream.Length : -1;
        _disposeStream = disposeUnderlyingStream;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var start = _currentOffset;
        if (_internalBuffer is null && _currentOffset < _totalBufferSize)
        {
            _internalBuffer = new byte[checked((int)_totalBufferSize)];
        }

        if (_currentOffset < _bufferedSoFar)
        {
            var read = ReadFromBuffer(buffer, offset, count);
            count -= read;
            offset += read;
        }

        if (count > 0 && _bufferedSoFar < _totalBufferSize)
        {
            var read = BufferAndWriteTo(buffer, offset, count);
            count -= read;
            offset += read;
        }

        if (count > 0)
        {
            var read = ReadDirectlyFromStream(buffer, offset, count);
            if (read > 0)
            {
                _internalBuffer = null;
            }
        }

        return checked((int)(_currentOffset - start));
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_currentOffset > _totalBufferSize)
        {
            throw new InvalidOperationException(
                "The position cannot be changed once the stream has moved past the buffer.");
        }

        var target = origin switch
        {
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End when Length == -1 => throw new InvalidOperationException(
                "Can't seek from end as the underlying stream is not seekable."),
            SeekOrigin.End => Length + offset,
            _ => offset,
        };

        if (target <= _currentOffset)
        {
            _currentOffset = target;
        }
        else
        {
            var distance = target - _currentOffset;
            _currentOffset += Read(null!, 0, checked((int)distance));
        }

        return Position;
    }

    public override void SetLength(long value)
    {
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _internalBuffer = null;
        if (_disposeStream)
        {
            _underlyingStream?.Dispose();
        }

        _underlyingStream = null;
    }

    private int ReadFromBuffer(byte[]? destination, int offset, int size)
    {
        var count = Math.Min(size, checked((int)(_bufferedSoFar - _currentOffset)));
        if (destination is not null && offset < destination.Length)
        {
            Buffer.BlockCopy(_internalBuffer!, checked((int)_currentOffset), destination, offset, count);
        }

        _currentOffset += count;
        return count;
    }

    private int BufferAndWriteTo(byte[]? destination, int offset, int size)
    {
        var count = Math.Min(size, checked((int)(_totalBufferSize - _bufferedSoFar)));
        var stream = _underlyingStream ?? throw new ObjectDisposedException(nameof(SKFrontBufferedStream));
        var read = stream.Read(_internalBuffer!, checked((int)_currentOffset), count);
        if (destination is not null && offset < destination.Length)
        {
            Buffer.BlockCopy(_internalBuffer!, checked((int)_currentOffset), destination, offset, read);
        }

        _bufferedSoFar += read;
        _currentOffset = _bufferedSoFar;
        return read;
    }

    private int ReadDirectlyFromStream(byte[]? destination, int offset, int size)
    {
        var stream = _underlyingStream ?? throw new ObjectDisposedException(nameof(SKFrontBufferedStream));
        var read = destination is not null
            ? stream.Read(destination, offset, size)
            : stream.Seek(size, SeekOrigin.Current);
        _currentOffset += read;
        return checked((int)read);
    }
}

public class SKFrontBufferedManagedStream : SKAbstractManagedStream
{
    private SKStream? _stream;
    private readonly bool _disposeStream;
    private readonly bool _hasLength;
    private readonly int _streamLength;
    private readonly int _bufferLength;
    private byte[]? _frontBuffer;
    private int _bufferedSoFar;
    private int _offset;

    public SKFrontBufferedManagedStream(Stream managedStream, int bufferSize)
        : this(managedStream, bufferSize, disposeUnderlyingStream: false)
    {
    }

    public SKFrontBufferedManagedStream(
        Stream managedStream,
        int bufferSize,
        bool disposeUnderlyingStream)
        : this(
            new SKManagedStream(managedStream, disposeUnderlyingStream),
            bufferSize,
            disposeUnderlyingStream: true)
    {
    }

    public SKFrontBufferedManagedStream(SKStream nativeStream, int bufferSize)
        : this(nativeStream, bufferSize, disposeUnderlyingStream: false)
    {
    }

    public SKFrontBufferedManagedStream(
        SKStream nativeStream,
        int bufferSize,
        bool disposeUnderlyingStream)
    {
        ArgumentNullException.ThrowIfNull(nativeStream);
        var length = nativeStream.HasLength ? nativeStream.Length : 0;
        var position = nativeStream.HasPosition ? nativeStream.Position : 0;
        _disposeStream = disposeUnderlyingStream;
        _stream = nativeStream;
        _hasLength = nativeStream.HasPosition && nativeStream.HasLength;
        _streamLength = length - position;
        _bufferLength = bufferSize;
        _frontBuffer = new byte[bufferSize];
    }

    protected internal override IntPtr OnRead(IntPtr buffer, IntPtr size)
    {
        var remaining = checked((int)size.ToInt64());
        var start = _offset;

        if (remaining > 0 && _offset < _bufferedSoFar)
        {
            var count = Math.Min(remaining, _bufferedSoFar - _offset);
            if (buffer != IntPtr.Zero)
            {
                Marshal.Copy(_frontBuffer!, _offset, buffer, count);
                buffer += count;
            }

            _offset += count;
            remaining -= count;
        }

        var reachedEnd = false;
        if (remaining > 0 && _bufferedSoFar < _bufferLength)
        {
            var count = Math.Min(remaining, _bufferLength - _bufferedSoFar);
            var frontBuffer = _frontBuffer!;
            var rented = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                var read = GetStream().Read(rented, count);
                rented.AsSpan(0, read).CopyTo(frontBuffer.AsSpan(_offset));
                reachedEnd = read < count;
                _bufferedSoFar += read;
                if (buffer != IntPtr.Zero)
                {
                    Marshal.Copy(frontBuffer, _offset, buffer, read);
                    buffer += read;
                }

                _offset += read;
                remaining -= read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        if (remaining > 0 && !reachedEnd)
        {
            var read = GetStream().Read(buffer, remaining);
            if (read > 0)
            {
                _frontBuffer = null;
            }

            _offset += read;
        }

        return (IntPtr)(_offset - start);
    }

    protected internal override IntPtr OnPeek(IntPtr buffer, IntPtr size)
    {
        if (_offset >= _bufferLength)
        {
            return IntPtr.Zero;
        }

        var position = _offset;
        var count = Math.Min(checked((int)size.ToInt64()), _bufferLength - _offset);
        var read = Read(buffer, count);
        _offset = position;
        return (IntPtr)read;
    }

    protected internal override bool OnIsAtEnd() =>
        _offset >= _bufferedSoFar && GetStream().IsAtEnd;

    protected internal override bool OnRewind()
    {
        if (_offset > _bufferLength)
        {
            return false;
        }

        _offset = 0;
        return true;
    }

    protected internal override bool OnHasLength() => _hasLength;
    protected internal override IntPtr OnGetLength() => (IntPtr)_streamLength;
    protected internal override bool OnHasPosition() => false;
    protected internal override IntPtr OnGetPosition() => IntPtr.Zero;
    protected internal override bool OnSeek(IntPtr position) => false;
    protected internal override bool OnMove(int offset) => false;
    protected internal override IntPtr OnCreateNew() => IntPtr.Zero;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    protected override void DisposeManaged()
    {
        var stream = _stream;
        _stream = null;
        if (_disposeStream)
        {
            stream?.Dispose();
        }

        _frontBuffer = null;
        base.DisposeManaged();
    }

    private SKStream GetStream() =>
        _stream ?? throw new ObjectDisposedException(nameof(SKFrontBufferedManagedStream));
}
