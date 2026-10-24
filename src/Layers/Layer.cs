namespace PaintNetClone.Layers;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using PaintNetClone.Core.Rendering;
using PaintNetClone.Core.Geometry;
using PaintNetClone.Core.Color;

public enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten,
    ColorDodge,
    ColorBurn,
    HardLight,
    SoftLight,
    Difference,
    Exclusion,
    Hue,
    Saturation,
    Color,
    Luminosity,
    Additive,
    Subtractive,
    Reflect,
    Glow,
    AlphaOver,
    AlphaUnder,
    Behind,
    Erase,
    Copy,
    Min,
    Max
}

public enum LayerVisibility
{
    Visible,
    Hidden,
    Solo
}

public abstract class Layer : IDisposable
{
    private string _name;
    private bool _visible = true;
    private double _opacity = 1.0;
    private BlendMode _blendMode = BlendMode.Normal;
    protected bool _disposed;
    protected readonly List<Layer> _children = new();
    private Layer? _parent;
    private bool _isBackground;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
    public bool Visible { get => _visible; set { _visible = value; OnPropertyChanged(nameof(Visible)); } }
    public double Opacity { get => _opacity; set { _opacity = Math.Clamp(value, 0, 1); OnPropertyChanged(nameof(Opacity)); } }
    public BlendMode BlendMode { get => _blendMode; set { _blendMode = value; OnPropertyChanged(nameof(BlendMode)); } }
    public bool IsBackground { get => _isBackground; internal set => _isBackground = value; }
    public Layer? Parent => _parent;
    public IReadOnlyList<Layer> Children => _children;
    public int Index => _parent?._children.IndexOf(this) ?? -1;
    public int Depth => _parent?.Depth + 1 ?? 0;
    public abstract RectInt32 Bounds { get; }
    public abstract Surface? GetSurface();
    public abstract void Invalidate();

    public event EventHandler? PropertyChanged;
    public event EventHandler? Invalidated;

    protected Layer(string name)
    {
        _name = name;
    }

    internal void SetParent(Layer? parent)
    {
        _parent = parent;
    }

    public void AddChild(Layer child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        if (child._parent != null) child._parent.RemoveChild(child);
        child.SetParent(this);
        _children.Add(child);
        OnPropertyChanged(nameof(Children));
    }

    public bool RemoveChild(Layer child)
    {
        if (child._parent != this) return false;
        child.SetParent(null);
        _children.Remove(child);
        OnPropertyChanged(nameof(Children));
        return true;
    }

    public void MoveChild(Layer child, int newIndex)
    {
        if (child._parent != this) throw new ArgumentException("Child not in this layer");
        int oldIndex = _children.IndexOf(child);
        if (oldIndex == newIndex) return;
        _children.RemoveAt(oldIndex);
        if (newIndex > oldIndex) newIndex--;
        _children.Insert(newIndex, child);
        OnPropertyChanged(nameof(Children));
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void OnInvalidated()
    {
        Invalidated?.Invoke(this, EventArgs.Empty);
        _parent?.OnInvalidated();
    }

    public void InvalidateBounds() => Invalidate();

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            foreach (var child in _children.ToArray())
                child.Dispose();
            _children.Clear();
            _disposed = true;
        }
    }
}

public sealed class BitmapLayer : Layer
{
    private Surface? _surface;
    private RectInt32 _bounds;
    private bool _dirty;

    public override RectInt32 Bounds => _bounds;
    public Surface? Surface => _surface;

    public BitmapLayer(string name, int width, int height) : base(name)
    {
        _surface = new Surface(width, height);
        _bounds = new RectInt32(0, 0, width, height);
    }

    public BitmapLayer(string name, Surface surface) : base(name)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _bounds = new RectInt32(0, 0, surface.Width, surface.Height);
    }

    public override Surface? GetSurface() => _surface;

    public void SetSurface(Surface surface)
    {
        _surface?.Dispose();
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _bounds = new RectInt32(0, 0, surface.Width, surface.Height);
        _dirty = true;
        OnInvalidated();
    }

    public void Resize(int width, int height)
    {
        if (width == _bounds.Width && height == _bounds.Height) return;
        var newSurface = new Surface(width, height);
        if (_surface != null)
        {
            newSurface.CopyFrom(_surface);
            _surface.Dispose();
        }
        _surface = newSurface;
        _bounds = new RectInt32(0, 0, width, height);
        _dirty = true;
        OnInvalidated();
    }

    public void Crop(RectInt32 rect)
    {
        rect = rect.Intersect(Bounds);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var cropped = _surface!.Clone(rect);
        _surface.Dispose();
        _surface = cropped;
        _bounds = new RectInt32(0, 0, rect.Width, rect.Height);
        _dirty = true;
        OnInvalidated();
    }

    public override void Invalidate() => _dirty = true;

    public override void Dispose()
    {
        if (!_disposed)
        {
            _surface?.Dispose();
            _surface = null;
            base.Dispose();
        }
    }
}

public sealed class GroupLayer : Layer
{
    private RectInt32 _cachedBounds;
    private bool _boundsDirty = true;

    public override RectInt32 Bounds
    {
        get
        {
            if (_boundsDirty) RecalculateBounds();
            return _cachedBounds;
        }
    }

    public GroupLayer(string name) : base(name) { }

    private void RecalculateBounds()
    {
        bool first = true;
        RectInt32 bounds = RectInt32.Empty;

        foreach (var child in _children)
        {
            if (!child.Visible) continue;
            var childBounds = child.Bounds;
            if (childBounds.Width <= 0 || childBounds.Height <= 0) continue;

            if (first)
            {
                bounds = childBounds;
                first = false;
            }
            else
            {
                bounds = bounds.Union(childBounds);
            }
        }

        _cachedBounds = bounds;
        _boundsDirty = false;
    }

    public override Surface? GetSurface()
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;

        var result = new Surface(bounds.Width, bounds.Height);
        RenderToSurface(result, new PointInt32(-bounds.X, -bounds.Y));
        return result;
    }

    internal void RenderToSurface(Surface target, PointInt32 offset)
    {
        foreach (var child in _children)
        {
            if (!child.Visible) continue;
            var childSurface = child.GetSurface();
            if (childSurface == null) continue;

            var childBounds = child.Bounds;
            var drawPos = new PointInt32(childBounds.X + offset.X, childBounds.Y + offset.Y);

            if (child is GroupLayer group)
            {
                group.RenderToSurface(target, drawPos);
            }
            else
            {
                target.CopyFrom(childSurface, childBounds, drawPos);
            }
        }
    }

    public override void Invalidate()
    {
        _boundsDirty = true;
        OnInvalidated();
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        if (propertyName == nameof(Children) || propertyName == nameof(Visible))
            _boundsDirty = true;
        base.OnPropertyChanged(propertyName);
    }
}

public sealed class AdjustmentLayer : Layer
{
    private readonly Func<ColorBgra, ColorBgra> _adjustment;
    private RectInt32 _bounds;

    public override RectInt32 Bounds => _bounds;

    public AdjustmentLayer(string name, Func<ColorBgra, ColorBgra> adjustment, RectInt32 bounds) : base(name)
    {
        _adjustment = adjustment ?? throw new ArgumentNullException(nameof(adjustment));
        _bounds = bounds;
    }

    public override Surface? GetSurface()
    {
        return null;
    }

    public ColorBgra Apply(ColorBgra color) => _adjustment(color);

    public override void Invalidate() => OnInvalidated();
}

public static class BlendModeExtensions
{
    public static ColorBgra Blend(this BlendMode mode, ColorBgra dst, ColorBgra src, double opacity)
    {
        if (opacity <= 0) return dst;
        if (opacity >= 1) return mode.Blend(dst, src);

        var blended = mode.Blend(dst, src);
        return AlphaBlend(dst, blended, (byte)Math.Round(opacity * 255));
    }

    public static ColorBgra Blend(this BlendMode mode, ColorBgra dst, ColorBgra src)
    {
        int srcA = src.A;
        int dstA = dst.A;
        int invSrcA = 255 - srcA;

        return mode switch
        {
            BlendMode.Normal => AlphaBlend(dst, src),
            BlendMode.Multiply => MultiplyBlend(dst, src),
            BlendMode.Screen => ScreenBlend(dst, src),
            BlendMode.Overlay => OverlayBlend(dst, src),
            BlendMode.Darken => DarkenBlend(dst, src),
            BlendMode.Lighten => LightenBlend(dst, src),
            BlendMode.ColorDodge => ColorDodgeBlend(dst, src),
            BlendMode.ColorBurn => ColorBurnBlend(dst, src),
            BlendMode.HardLight => HardLightBlend(dst, src),
            BlendMode.SoftLight => SoftLightBlend(dst, src),
            BlendMode.Difference => DifferenceBlend(dst, src),
            BlendMode.Exclusion => ExclusionBlend(dst, src),
            BlendMode.Additive => AdditiveBlend(dst, src),
            BlendMode.Subtractive => SubtractiveBlend(dst, src),
            BlendMode.AlphaOver => AlphaOverBlend(dst, src),
            BlendMode.AlphaUnder => AlphaUnderBlend(dst, src),
            BlendMode.Behind => BehindBlend(dst, src),
            BlendMode.Erase => EraseBlend(dst, src),
            BlendMode.Copy => src,
            BlendMode.Min => MinBlend(dst, src),
            BlendMode.Max => MaxBlend(dst, src),
            _ => AlphaBlend(dst, src)
        };
    }

    private static ColorBgra AlphaBlend(ColorBgra dst, ColorBgra src, byte opacity = 255)
    {
        int srcA = src.A * opacity / 255;
        int invSrcA = 255 - srcA;
        return new ColorBgra(
            (byte)((src.B * srcA + dst.B * invSrcA) / 255),
            (byte)((src.G * srcA + dst.G * invSrcA) / 255),
            (byte)((src.R * srcA + dst.R * invSrcA) / 255),
            (byte)Math.Min(255, dst.A + srcA)
        );
    }

    private static ColorBgra MultiplyBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)(dst.B * src.B / 255), (byte)(dst.G * src.G / 255), (byte)(dst.R * src.R / 255), dst.A);

    private static ColorBgra ScreenBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)(255 - (255 - dst.B) * (255 - src.B) / 255),
        (byte)(255 - (255 - dst.G) * (255 - src.G) / 255),
        (byte)(255 - (255 - dst.R) * (255 - src.R) / 255), dst.A);

    private static ColorBgra OverlayBlend(ColorBgra dst, ColorBgra src)
    {
        byte Overlay(byte d, byte s) => d < 128 ? (byte)(2 * d * s / 255) : (byte)(255 - 2 * (255 - d) * (255 - s) / 255);
        return new(Overlay(dst.B, src.B), Overlay(dst.G, src.G), Overlay(dst.R, src.R), dst.A);
    }

    private static ColorBgra DarkenBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)Math.Min(dst.B, src.B), (byte)Math.Min(dst.G, src.G), (byte)Math.Min(dst.R, src.R), dst.A);

    private static ColorBgra LightenBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)Math.Max(dst.B, src.B), (byte)Math.Max(dst.G, src.G), (byte)Math.Max(dst.R, src.R), dst.A);

    private static ColorBgra ColorDodgeBlend(ColorBgra dst, ColorBgra src)
    {
        byte Dodge(byte d, byte s) => s == 255 ? (byte)255 : (byte)Math.Min(255, d * 255 / (255 - s));
        return new(Dodge(dst.B, src.B), Dodge(dst.G, src.G), Dodge(dst.R, src.R), dst.A);
    }

    private static ColorBgra ColorBurnBlend(ColorBgra dst, ColorBgra src)
    {
        byte Burn(byte d, byte s) => s == 0 ? (byte)0 : (byte)(255 - (255 - d) * 255 / s);
        return new(Burn(dst.B, src.B), Burn(dst.G, src.G), Burn(dst.R, src.R), dst.A);
    }

    private static ColorBgra HardLightBlend(ColorBgra dst, ColorBgra src)
    {
        byte HardLight(byte d, byte s) => s < 128 ? (byte)(2 * d * s / 255) : (byte)(255 - 2 * (255 - d) * (255 - s) / 255);
        return new(HardLight(dst.B, src.B), HardLight(dst.G, src.G), HardLight(dst.R, src.R), dst.A);
    }

    private static ColorBgra SoftLightBlend(ColorBgra dst, ColorBgra src)
    {
        byte SoftLight(byte d, byte s)
        {
            double ds = d / 255.0, ss = s / 255.0;
            double result = ss < 0.5
                ? ds - (1 - 2 * ss) * ds * (1 - ds)
                : ds + (2 * ss - 1) * (Math.Sqrt(ds) - ds);
            return (byte)Math.Round(Math.Clamp(result, 0, 1) * 255);
        }
        return new(SoftLight(dst.B, src.B), SoftLight(dst.G, src.G), SoftLight(dst.R, src.R), dst.A);
    }

    private static ColorBgra DifferenceBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)Math.Abs(dst.B - src.B), (byte)Math.Abs(dst.G - src.G), (byte)Math.Abs(dst.R - src.R), dst.A);

    private static ColorBgra ExclusionBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)(dst.B + src.B - 2 * dst.B * src.B / 255),
        (byte)(dst.G + src.G - 2 * dst.G * src.G / 255),
        (byte)(dst.R + src.R - 2 * dst.R * src.R / 255), dst.A);

    private static ColorBgra AdditiveBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)Math.Min(255, dst.B + src.B), (byte)Math.Min(255, dst.G + src.G), (byte)Math.Min(255, dst.R + src.R), dst.A);

    private static ColorBgra SubtractiveBlend(ColorBgra dst, ColorBgra src) => new(
        (byte)Math.Max(0, dst.B - src.B), (byte)Math.Max(0, dst.G - src.G), (byte)Math.Max(0, dst.R - src.R), dst.A);

    private static ColorBgra AlphaOverBlend(ColorBgra dst, ColorBgra src) => AlphaBlend(dst, src);
    private static ColorBgra AlphaUnderBlend(ColorBgra dst, ColorBgra src) => AlphaBlend(src, dst);
    private static ColorBgra BehindBlend(ColorBgra dst, ColorBgra src) => dst.A == 0 ? src : dst;
    private static ColorBgra EraseBlend(ColorBgra dst, ColorBgra src) => new(dst.B, dst.G, dst.R, (byte)Math.Max(0, dst.A - src.A));
    private static ColorBgra MinBlend(ColorBgra dst, ColorBgra src) => DarkenBlend(dst, src);
    private static ColorBgra MaxBlend(ColorBgra dst, ColorBgra src) => LightenBlend(dst, src);
}