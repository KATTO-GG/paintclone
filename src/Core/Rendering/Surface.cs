namespace PaintNetClone.Core.Rendering;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PaintNetClone.Core.Color;
using PaintNetClone.Core.Geometry;

public sealed class Surface : IDisposable
{
    private readonly ColorBgra[] _pixels;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private bool _disposed;

    public int Width => _width;
    public int Height => _height;
    public int Stride => _stride;
    public SizeInt32 Size => new(_width, _height);
    public RectInt32 Bounds => new(0, 0, _width, _height);
    public ColorBgra[] Pixels => _pixels;

    public Surface(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Width and height must be positive");

        _width = width;
        _height = height;
        _stride = width;
        _pixels = new ColorBgra[width * height];
    }

    public Surface(int width, int height, ColorBgra fillColor) : this(width, height)
    {
        Fill(fillColor);
    }

    public Surface(ColorBgra[] pixels, int width, int height)
    {
        if (pixels == null) throw new ArgumentNullException(nameof(pixels));
        if (width <= 0 || height <= 0) throw new ArgumentException("Width and height must be positive");
        if (pixels.Length != width * height) throw new ArgumentException("Pixel array length must match width * height");

        _pixels = pixels;
        _width = width;
        _height = height;
        _stride = width;
    }

    public ColorBgra this[int x, int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
                throw new IndexOutOfRangeException();
            return _pixels[y * _stride + x];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
                throw new IndexOutOfRangeException();
            _pixels[y * _stride + x] = value;
        }
    }

    public ColorBgra this[PointInt32 pt]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this[pt.X, pt.Y];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this[pt.X, pt.Y] = value;
    }

    public ref ColorBgra GetPixelRef(int x, int y)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
            throw new IndexOutOfRangeException();
        return ref _pixels[y * _stride + x];
    }

    public ReadOnlySpan<ColorBgra> GetRowSpan(int y)
    {
        if ((uint)y >= (uint)_height) throw new IndexOutOfRangeException();
        return new ReadOnlySpan<ColorBgra>(_pixels, y * _stride, _width);
    }

    public Span<ColorBgra> GetRowSpanMutable(int y)
    {
        if ((uint)y >= (uint)_height) throw new IndexOutOfRangeException();
        return new Span<ColorBgra>(_pixels, y * _stride, _width);
    }

    public void Fill(ColorBgra color)
    {
        _pixels.AsSpan().Fill(color);
    }

    public void Fill(RectInt32 rect, ColorBgra color)
    {
        rect = rect.Intersect(Bounds);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            var row = GetRowSpanMutable(y);
            for (int x = rect.X; x < rect.Right; x++)
                row[x] = color;
        }
    }

    public void CopyFrom(Surface source, PointInt32 destPt = default)
    {
        CopyFrom(source, source.Bounds, destPt);
    }

    public void CopyFrom(Surface source, RectInt32 sourceRect, PointInt32 destPt)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        sourceRect = sourceRect.Intersect(source.Bounds);
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0) return;

        var destRect = new RectInt32(destPt.X, destPt.Y, sourceRect.Width, sourceRect.Height).Intersect(Bounds);
        if (destRect.Width <= 0 || destRect.Height <= 0) return;

        int copyWidth = Math.Min(sourceRect.Width, destRect.Width);
        int copyHeight = Math.Min(sourceRect.Height, destRect.Height);

        for (int y = 0; y < copyHeight; y++)
        {
            var srcRow = source.GetRowSpan(sourceRect.Y + y);
            var dstRow = GetRowSpanMutable(destRect.Y + y);
            srcRow.Slice(sourceRect.X, copyWidth).CopyTo(dstRow.Slice(destRect.X));
        }
    }

    public void CopyFrom(Surface source, RectInt32 sourceRect, PointInt32 destPt, ColorBgra blendColor)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        sourceRect = sourceRect.Intersect(source.Bounds);
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0) return;

        var destRect = new RectInt32(destPt.X, destPt.Y, sourceRect.Width, sourceRect.Height).Intersect(Bounds);
        if (destRect.Width <= 0 || destRect.Height <= 0) return;

        int copyWidth = Math.Min(sourceRect.Width, destRect.Width);
        int copyHeight = Math.Min(sourceRect.Height, destRect.Height);

        for (int y = 0; y < copyHeight; y++)
        {
            var srcRow = source.GetRowSpan(sourceRect.Y + y);
            var dstRow = GetRowSpanMutable(destRect.Y + y);
            for (int x = 0; x < copyWidth; x++)
            {
                var src = srcRow[sourceRect.X + x];
                var dst = dstRow[destRect.X + x];
                dstRow[destRect.X + x] = AlphaBlend(dst, src, blendColor);
            }
        }
    }

    private static ColorBgra AlphaBlend(ColorBgra dst, ColorBgra src, ColorBgra blendColor)
    {
        int srcA = src.A * blendColor.A / 255;
        int invSrcA = 255 - srcA;
        return new ColorBgra(
            (byte)((src.B * srcA + dst.B * invSrcA) / 255),
            (byte)((src.G * srcA + dst.G * invSrcA) / 255),
            (byte)((src.R * srcA + dst.R * invSrcA) / 255),
            (byte)Math.Min(255, dst.A + srcA)
        );
    }

    public Surface Clone() => new((ColorBgra[])_pixels.Clone(), _width, _height);

    public Surface Clone(RectInt32 rect)
    {
        rect = rect.Intersect(Bounds);
        if (rect.Width <= 0 || rect.Height <= 0) return new Surface(1, 1);

        var result = new Surface(rect.Width, rect.Height);
        CopyFrom(this, rect, PointInt32.Zero);
        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    public static Surface CreateFromPointer(IntPtr ptr, int width, int height, int stride)
    {
        var pixels = new ColorBgra[width * height];
        unsafe
        {
            var src = (ColorBgra*)ptr.ToPointer();
            for (int y = 0; y < height; y++)
            {
                var srcRow = src + y * (stride / sizeof(ColorBgra));
                var dstRow = pixels.AsSpan(y * width, width);
                for (int x = 0; x < width; x++)
                    dstRow[x] = srcRow[x];
            }
        }
        return new Surface(pixels, width, height);
    }

    public override string ToString() => $"Surface {_width}x{_height}";
}

public static class SurfaceExtensions
{
    public static void DrawLine(this Surface surface, PointInt32 p1, PointInt32 p2, ColorBgra color)
    {
        int x0 = p1.X, y0 = p1.Y, x1 = p2.X, y1 = p2.Y;
        bool steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
        if (steep) { (x0, y0) = (y0, x0); (x1, y1) = (y1, x1); }
        if (x0 > x1) { (x0, x1) = (x1, x0); (y0, y1) = (y1, y0); }

        int dx = x1 - x0, dy = Math.Abs(y1 - y0);
        int error = dx / 2, ystep = y0 < y1 ? 1 : -1, y = y0;

        for (int x = x0; x <= x1; x++)
        {
            if (steep) surface.SafeSetPixel(y, x, color);
            else surface.SafeSetPixel(x, y, color);
            error -= dy;
            if (error < 0) { y += ystep; error += dx; }
        }
    }

    public static void SafeSetPixel(this Surface surface, int x, int y, ColorBgra color)
    {
        if ((uint)x < (uint)surface.Width && (uint)y < (uint)surface.Height)
            surface[x, y] = color;
    }

    public static void DrawRect(this Surface surface, RectInt32 rect, ColorBgra color)
    {
        for (int x = rect.X; x < rect.Right; x++)
        {
            surface.SafeSetPixel(x, rect.Y, color);
            surface.SafeSetPixel(x, rect.Bottom - 1, color);
        }
        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            surface.SafeSetPixel(rect.X, y, color);
            surface.SafeSetPixel(rect.Right - 1, y, color);
        }
    }

    public static void FillRect(this Surface surface, RectInt32 rect, ColorBgra color)
    {
        rect = rect.Intersect(surface.Bounds);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            var row = surface.GetRowSpanMutable(y);
            for (int x = rect.X; x < rect.Right; x++)
                row[x] = color;
        }
    }
}