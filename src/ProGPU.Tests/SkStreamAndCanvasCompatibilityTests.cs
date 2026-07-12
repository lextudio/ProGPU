using System.Runtime.InteropServices;
using ProGPU.Scene;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkStreamAndCanvasCompatibilityTests
{
    [Fact]
    public void CanvasSaveCountsUseSkiaOneBasedSemantics()
    {
        using var canvas = new SKCanvas(new DrawingContext(), 16f, 16f);

        Assert.Equal(1, canvas.SaveCount);
        var saveCount = canvas.Save();
        Assert.Equal(1, saveCount);
        Assert.Equal(2, canvas.SaveCount);
        var nestedSaveCount = canvas.Save();
        Assert.Equal(2, nestedSaveCount);
        Assert.Equal(3, canvas.SaveCount);

        canvas.RestoreToCount(nestedSaveCount);
        Assert.Equal(2, canvas.SaveCount);
        canvas.RestoreToCount(saveCount);
        Assert.Equal(1, canvas.SaveCount);
        canvas.RestoreToCount(0);
        Assert.Equal(1, canvas.SaveCount);
    }

    [Fact]
    public void AutoCanvasRestoreRestoresNestedSavesExactlyOnce()
    {
        using var canvas = new SKCanvas(new DrawingContext(), 16f, 16f);
        using (var restore = new SKAutoCanvasRestore(canvas, doSave: true))
        {
            Assert.Equal(2, canvas.SaveCount);
            canvas.Save();
            Assert.Equal(3, canvas.SaveCount);
            restore.Restore();
            Assert.Equal(1, canvas.SaveCount);
            restore.Restore();
            Assert.Equal(1, canvas.SaveCount);
        }

        using (var restore = new SKAutoCanvasRestore(canvas, doSave: false))
        {
            Assert.Equal(1, canvas.SaveCount);
            canvas.Save();
            Assert.Equal(2, canvas.SaveCount);
        }

        Assert.Equal(1, canvas.SaveCount);
    }

    [Fact]
    public void FileStreamFeedsCodecAndPreservesInvalidPathContract()
    {
        var path = Path.Combine(Path.GetTempPath(), $"progpu-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(path, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAF/gL+AvzL8QAAAABJRU5ErkJggg=="));

            using var stream = new SKFileStream(path);
            Assert.True(stream.IsValid);
            Assert.Equal(new FileInfo(path).Length, stream.Length);
            Assert.Equal(0, stream.Position);
            Assert.Equal(0x89, stream.ReadByte());
            Assert.Equal(0x4e50, stream.ReadUInt16());
            Assert.Equal(3, stream.Position);
            var peek = Marshal.AllocHGlobal(2);
            try
            {
                Assert.Equal(2, stream.Peek(peek, 2));
                Assert.Equal(0x47, Marshal.ReadByte(peek));
                Assert.Equal(0x0d, Marshal.ReadByte(peek, 1));
                Assert.Equal(3, stream.Position);
            }
            finally
            {
                Marshal.FreeHGlobal(peek);
            }
            using (var data = stream.GetData())
            {
                Assert.Equal(File.ReadAllBytes(path), data.ToArray());
            }
            Assert.Equal(3, stream.Position);
            Assert.True(stream.Rewind());
            Assert.Equal(0, stream.Position);
            using var codec = SKCodec.Create(stream);
            Assert.Equal(1, codec.Info.Width);
            Assert.Equal(1, codec.Info.Height);

            using var missing = new SKFileStream(path + ".missing");
            Assert.False(missing.IsValid);
            Assert.Equal(0, missing.Length);
            Assert.Null(SKFileStream.OpenStream(path + ".missing"));
            using var empty = new SKFileStream(string.Empty);
            using var nullPath = new SKFileStream(null!);
            Assert.False(empty.IsValid);
            Assert.False(nullPath.IsValid);
            Assert.Null(SKFileStream.OpenStream(null!));
            Assert.True(SKFileStream.IsPathSupported(string.Empty));
            Assert.True(SKFileStream.IsPathSupported(null!));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AbstractManagedStreamDispatchesNativeCallbackContract()
    {
        Assert.Equal(typeof(SKAbstractManagedStream), typeof(SKManagedStream).BaseType);

        var stream = new CallbackManagedStream(new byte[] { 1, 2, 3, 4, 5 });
        Assert.NotEqual(IntPtr.Zero, stream.Handle);
        Assert.True(stream.HasPosition);
        Assert.True(stream.HasLength);
        Assert.Equal(5, stream.Length);
        Assert.False(stream.IsAtEnd);
        Assert.Equal(1, stream.ReadByte());
        Assert.Equal(1, stream.Position);

        var peek = Marshal.AllocHGlobal(2);
        try
        {
            Assert.Equal(2, stream.Peek(peek, 2));
            Assert.Equal(2, Marshal.ReadByte(peek));
            Assert.Equal(3, Marshal.ReadByte(peek, 1));
            Assert.Equal(1, stream.Position);
        }
        finally
        {
            Marshal.FreeHGlobal(peek);
        }

        Assert.Equal(1, stream.Skip(1));
        Assert.Equal(2, stream.Position);
        Assert.True(stream.Move(1));
        Assert.Equal(3, stream.Position);
        Assert.Equal(4, stream.ReadByte());
        Assert.True(stream.Seek(1));
        using (var data = stream.GetData())
        {
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, data.ToArray());
        }

        Assert.Equal(1, stream.Position);
        Assert.True(stream.Rewind());
        Assert.Equal(0, stream.Position);
        stream.Dispose();
        Assert.Equal(IntPtr.Zero, stream.Handle);
        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
    }

    private sealed class CallbackManagedStream : SKAbstractManagedStream
    {
        private readonly byte[] _data;
        private int _position;

        public CallbackManagedStream(byte[] data)
        {
            _data = data;
        }

        protected internal override IntPtr OnRead(IntPtr buffer, IntPtr size)
        {
            var requested = checked((int)size.ToInt64());
            ArgumentOutOfRangeException.ThrowIfNegative(requested);
            var count = Math.Min(requested, _data.Length - _position);
            if (buffer != IntPtr.Zero && count > 0)
            {
                Marshal.Copy(_data, _position, buffer, count);
            }

            _position += count;
            return (IntPtr)count;
        }

        protected internal override IntPtr OnPeek(IntPtr buffer, IntPtr size)
        {
            var position = _position;
            var read = OnRead(buffer, size);
            _position = position;
            return read;
        }

        protected internal override bool OnIsAtEnd() => _position >= _data.Length;
        protected internal override bool OnHasPosition() => true;
        protected internal override bool OnHasLength() => true;

        protected internal override bool OnRewind()
        {
            _position = 0;
            return true;
        }

        protected internal override IntPtr OnGetPosition() => (IntPtr)_position;
        protected internal override IntPtr OnGetLength() => (IntPtr)_data.Length;

        protected internal override bool OnSeek(IntPtr position)
        {
            var value = checked((int)position.ToInt64());
            if (value < 0 || value > _data.Length)
            {
                return false;
            }

            _position = value;
            return true;
        }

        protected internal override bool OnMove(int offset) => OnSeek((IntPtr)(_position + offset));
        protected internal override IntPtr OnCreateNew() => IntPtr.Zero;
    }
}
