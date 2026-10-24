namespace PaintNetClone.Tools;

using System;
using System.Collections.Generic;
using PaintNetClone.Core.Document;
using PaintNetClone.Core.Geometry;
using PaintNetClone.Core.Rendering;
using PaintNetClone.Core.Color;
using PaintNetClone.Layers;

public abstract class Tool : IDisposable
{
    protected Document? _document;
    protected BitmapLayer? _activeLayer;
    protected bool _active;
    private bool _disposed;

    public string Name { get; }
    public string CursorName { get; protected set; } = "Arrow";
    public bool IsActive => _active;

    public event EventHandler? Activated;
    public event EventHandler? Deactivated;
    public event EventHandler<ToolEventArgs>? StatusChanged;

    protected Tool(string name)
    {
        Name = name;
    }

    public virtual void Activate(Document document, BitmapLayer activeLayer)
    {
        if (_active) return;
        _document = document;
        _activeLayer = activeLayer;
        _active = true;
        OnActivated();
        Activated?.Invoke(this, EventArgs.Empty);
    }

    public virtual void Deactivate()
    {
        if (!_active) return;
        OnDeactivated();
        _active = false;
        _document = null;
        _activeLayer = null;
        Deactivated?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnActivated() { }
    protected virtual void OnDeactivated() { }

    public virtual void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers) { }
    public virtual void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers) { }
    public virtual void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers) { }
    public virtual void OnMouseWheel(PointInt32 point, int delta, KeyModifiers modifiers) { }
    public virtual void OnKeyDown(Key key, KeyModifiers modifiers) { }
    public virtual void OnKeyUp(Key key, KeyModifiers modifiers) { }
    public virtual void OnDoubleClick(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers) { }

    protected void ReportStatus(string message) => StatusChanged?.Invoke(this, new ToolEventArgs(message));

    protected ColorBgra PrimaryColor => ColorBgra.Black;
    protected ColorBgra SecondaryColor => ColorBgra.White;

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_active) Deactivate();
            _disposed = true;
        }
    }
}

public enum MouseButtons { None = 0, Left = 1, Right = 2, Middle = 4, XButton1 = 8, XButton2 = 16 }
public enum KeyModifiers { None = 0, Shift = 1, Control = 2, Alt = 4, Windows = 8 }
public enum Key { None, Escape, Enter, Tab, Space, Backspace, Delete, Left, Right, Up, Down, Home, End, PageUp, PageDown, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, D0, D1, D2, D3, D4, D5, D6, D7, D8, D9, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }

[Flags]
public enum ToolCapabilities
{
    None = 0,
    SupportsPressure = 1,
    SupportsTilt = 2,
    RequiresSelection = 4,
    CanCreateSelection = 8,
    ModifiesPixels = 16,
    ModifiesGeometry = 32,
    IsInteractive = 64,
    SupportsAntiAlias = 128,
    SupportsBlendModes = 256
}

public sealed class ToolEventArgs : EventArgs
{
    public string Message { get; }
    public ToolEventArgs(string message) { Message = message; }
}

public abstract class BrushToolBase : Tool
{
    protected float _brushSize = 10;
    protected float _brushHardness = 0.5f;
    protected float _brushOpacity = 1.0f;
    protected BlendMode _blendMode = BlendMode.Normal;
    protected PointInt32? _lastPoint;
    private readonly List<PointInt32> _strokePoints = new();

    public float BrushSize { get => _brushSize; set { _brushSize = Math.Max(0.1f, value); OnBrushChanged(); } }
    public float BrushHardness { get => _brushHardness; set { _brushHardness = Math.Clamp(value, 0, 1); OnBrushChanged(); } }
    public float BrushOpacity { get => _brushOpacity; set { _brushOpacity = Math.Clamp(value, 0, 1); OnBrushChanged(); } }
    public BlendMode BlendMode { get => _blendMode; set { _blendMode = value; OnBrushChanged(); } }

    public event EventHandler? BrushChanged;

    protected BrushToolBase(string name) : base(name) { }

    protected virtual void OnBrushChanged() => BrushChanged?.Invoke(this, EventArgs.Empty);

    protected void BeginStroke(PointInt32 point)
    {
        _strokePoints.Clear();
        _lastPoint = point;
        _strokePoints.Add(point);
    }

    protected void ContinueStroke(PointInt32 point)
    {
        if (_lastPoint.HasValue)
            _strokePoints.Add(point);
        _lastPoint = point;
    }

    protected void EndStroke() => _lastPoint = null;

    protected IEnumerable<PointInt32> GetStrokePoints() => _strokePoints;

    protected void ApplyBrush(PointInt32 center, ColorBgra color)
    {
        if (_activeLayer == null || _document == null) return;

        int radius = (int)Math.Ceiling(_brushSize / 2);
        var bounds = _activeLayer.Bounds;
        var surface = _activeLayer.Surface;
        if (surface == null) return;

        for (int y = -radius; y <= radius; y++)
        {
            int py = center.Y + y;
            if (py < 0 || py >= bounds.Height) continue;

            for (int x = -radius; x <= radius; x++)
            {
                int px = center.X + x;
                if (px < 0 || px >= bounds.Width) continue;

                double dist = Math.Sqrt(x * x + y * y);
                if (dist > radius) continue;

                double falloff = 1.0 - (dist / radius);
                falloff = Math.Pow(falloff, 1.0 / Math.Max(0.01, _brushHardness));
                byte alpha = (byte)Math.Round(255 * falloff * _brushOpacity);

                if (alpha == 0) continue;

                var brushColor = color.WithAlpha(alpha);
                var existing = surface[px, py];
                surface[px, py] = _blendMode.Blend(existing, brushColor, _brushOpacity);
            }
        }

        _activeLayer.InvalidateBounds();
    }

    protected void DrawLine(PointInt32 p1, PointInt32 p2, ColorBgra color)
    {
        int dx = Math.Abs(p2.X - p1.X);
        int dy = Math.Abs(p2.Y - p1.Y);
        int sx = p1.X < p2.X ? 1 : -1;
        int sy = p1.Y < p2.Y ? 1 : -1;
        int err = dx - dy;
        int x = p1.X, y = p1.Y;

        while (true)
        {
            ApplyBrush(new PointInt32(x, y), color);
            if (x == p2.X && y == p2.Y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
}

public sealed class PaintBrushTool : BrushToolBase
{
    public PaintBrushTool() : base("Paint Brush") { CursorName = "Crosshair"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (buttons.HasFlag(MouseButtons.Left) || buttons.HasFlag(MouseButtons.Right))
        {
            var color = buttons.HasFlag(MouseButtons.Left) ? PrimaryColor : SecondaryColor;
            BeginStroke(point);
            ApplyBrush(point, color);
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_lastPoint.HasValue) return;

        if (buttons.HasFlag(MouseButtons.Left) || buttons.HasFlag(MouseButtons.Right))
        {
            var color = buttons.HasFlag(MouseButtons.Left) ? PrimaryColor : SecondaryColor;
            DrawLine(_lastPoint.Value, point, color);
            ContinueStroke(point);
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        EndStroke();
    }
}

public sealed class EraserTool : BrushToolBase
{
    public EraserTool() : base("Eraser") { CursorName = "Crosshair"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (buttons.HasFlag(MouseButtons.Left))
        {
            BeginStroke(point);
            ApplyBrush(point, ColorBgra.Transparent);
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_lastPoint.HasValue) return;

        if (buttons.HasFlag(MouseButtons.Left))
        {
            DrawLine(_lastPoint.Value, point, ColorBgra.Transparent);
            ContinueStroke(point);
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        EndStroke();
    }
}

public sealed class PencilTool : Tool
{
    public PencilTool() : base("Pencil") { CursorName = "Crosshair"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_activeLayer == null || _document == null) return;
        if (buttons.HasFlag(MouseButtons.Left) || buttons.HasFlag(MouseButtons.Right))
        {
            var color = buttons.HasFlag(MouseButtons.Left) ? PrimaryColor : SecondaryColor;
            var surface = _activeLayer.Surface;
            if (surface != null && point.X >= 0 && point.X < surface.Width && point.Y >= 0 && point.Y < surface.Height)
            {
                surface[point.X, point.Y] = color;
                _activeLayer.InvalidateBounds();
            }
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        OnMouseDown(point, buttons, modifiers);
    }
}

public sealed class FillTool : Tool
{
    public FillTool() : base("Paint Bucket") { CursorName = "Crosshair"; }

    public float Tolerance { get; set; } = 0.1f;
    public bool Contiguous { get; set; } = true;
    public bool AllLayers { get; set; } = false;

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_activeLayer == null || _document == null) return;
        if (!buttons.HasFlag(MouseButtons.Left) && !buttons.HasFlag(MouseButtons.Right)) return;

        var color = buttons.HasFlag(MouseButtons.Left) ? PrimaryColor : SecondaryColor;
        var surface = _activeLayer.Surface;
        if (surface == null) return;

        if (point.X < 0 || point.X >= surface.Width || point.Y < 0 || point.Y >= surface.Height) return;

        var targetColor = surface[point.X, point.Y];
        if (ColorSimilar(targetColor, color)) return;

        FloodFill(surface, point, targetColor, color);
        _activeLayer.InvalidateBounds();
    }

    private void FloodFill(Surface surface, PointInt32 start, ColorBgra target, ColorBgra replacement)
    {
        int w = surface.Width, h = surface.Height;
        var visited = new bool[w * h];
        var stack = new Stack<PointInt32>();
        stack.Push(start);

        byte tolR = (byte)(target.R * Tolerance * 255);
        byte tolG = (byte)(target.G * Tolerance * 255);
        byte tolB = (byte)(target.B * Tolerance * 255);
        byte tolA = (byte)(target.A * Tolerance * 255);

        bool Similar(ColorBgra c) =>
            Math.Abs(c.R - target.R) <= tolR &&
            Math.Abs(c.G - target.G) <= tolG &&
            Math.Abs(c.B - target.B) <= tolB &&
            Math.Abs(c.A - target.A) <= tolA;

        while (stack.Count > 0)
        {
            var pt = stack.Pop();
            if (pt.X < 0 || pt.X >= w || pt.Y < 0 || pt.Y >= h) continue;
            int idx = pt.Y * w + pt.X;
            if (visited[idx]) continue;
            visited[idx] = true;

            var current = surface[pt.X, pt.Y];
            if (!Similar(current)) continue;

            surface[pt.X, pt.Y] = replacement;

            stack.Push(new PointInt32(pt.X + 1, pt.Y));
            stack.Push(new PointInt32(pt.X - 1, pt.Y));
            stack.Push(new PointInt32(pt.X, pt.Y + 1));
            stack.Push(new PointInt32(pt.X, pt.Y - 1));
        }
    }

    private bool ColorSimilar(ColorBgra a, ColorBgra b)
    {
        return Math.Abs(a.R - b.R) < 2 && Math.Abs(a.G - b.G) < 2 && Math.Abs(a.B - b.B) < 2 && Math.Abs(a.A - b.A) < 2;
    }
}

public sealed class GradientTool : Tool
{
    public enum GradientType { Linear, Radial, Angular, Reflected, Diamond }

    public GradientType Type { get; set; } = GradientType.Linear;
    public bool Dither { get; set; } = true;

    private PointInt32? _startPoint;
    private PointInt32? _endPoint;

    public GradientTool() : base("Gradient") { CursorName = "Crosshair"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (buttons.HasFlag(MouseButtons.Left))
        {
            _startPoint = point;
            _endPoint = point;
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue) return;
        if (buttons.HasFlag(MouseButtons.Left))
        {
            _endPoint = point;
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue || !_endPoint.HasValue) return;
        if (_activeLayer == null || _document == null) return;

        var surface = _activeLayer.Surface;
        if (surface == null) return;

        var start = _startPoint.Value;
        var end = _endPoint.Value;

        RenderGradient(surface, start, end, PrimaryColor, SecondaryColor);
        _activeLayer.InvalidateBounds();

        _startPoint = null;
        _endPoint = null;
    }

    private void RenderGradient(Surface surface, PointInt32 start, PointInt32 end, ColorBgra c1, ColorBgra c2)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lenSq = dx * dx + dy * dy;

        if (lenSq == 0) return;

        for (int y = 0; y < surface.Height; y++)
        {
            var row = surface.GetRowSpanMutable(y);
            for (int x = 0; x < surface.Width; x++)
            {
                double px = x - start.X;
                double py = y - start.Y;

                double t = Type switch
                {
                    GradientType.Linear => (px * dx + py * dy) / lenSq,
                    GradientType.Radial => Math.Sqrt(px * px + py * py) / Math.Sqrt(lenSq),
                    GradientType.Angular => (Math.Atan2(py, px) - Math.Atan2(dy, dx)) / (2 * Math.PI) + 0.5,
                    _ => (px * dx + py * dy) / lenSq
                };

                t = (t % 1 + 1) % 1;

                if (Dither)
                {
                    int dither = ((x + y) * 17) % 256;
                    t += (dither - 128) / 255.0 / Math.Max(surface.Width, surface.Height);
                }

                var color = ColorBgra.Lerp(c1, c2, t);
                row[x] = color;
            }
        }
    }
}

public sealed class LineTool : Tool
{
    public LineTool() : base("Line") { CursorName = "Crosshair"; }

    private PointInt32? _startPoint;
    private PointInt32? _endPoint;
    private Surface? _previewSurface;

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_activeLayer == null) return;
        if (buttons.HasFlag(MouseButtons.Left))
        {
            _startPoint = point;
            _endPoint = point;
            _previewSurface = _activeLayer.Surface?.Clone();
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue) return;
        _endPoint = point;

        if (_previewSurface != null && _activeLayer != null)
        {
            _activeLayer.Surface?.CopyFrom(_previewSurface);
            DrawLine(_activeLayer.Surface!, _startPoint.Value, _endPoint.Value, PrimaryColor);
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue || !_endPoint.HasValue) return;
        if (_activeLayer == null) return;

        if (_previewSurface != null)
        {
            _previewSurface.Dispose();
            _previewSurface = null;
        }

        DrawLine(_activeLayer.Surface!, _startPoint.Value, _endPoint.Value, PrimaryColor);
        _activeLayer.InvalidateBounds();

        _startPoint = null;
        _endPoint = null;
    }

    private void DrawLine(Surface surface, PointInt32 p1, PointInt32 p2, ColorBgra color)
    {
        int dx = Math.Abs(p2.X - p1.X);
        int dy = Math.Abs(p2.Y - p1.Y);
        int sx = p1.X < p2.X ? 1 : -1;
        int sy = p1.Y < p2.Y ? 1 : -1;
        int err = dx - dy;
        int x = p1.X, y = p1.Y;

        while (true)
        {
            surface.SafeSetPixel(x, y, color);
            if (x == p2.X && y == p2.Y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
}

public sealed class RectangleTool : Tool
{
    public enum RectangleMode { Outline, Filled, FilledOutline }
    public RectangleMode Mode { get; set; } = RectangleMode.FilledOutline;

    private PointInt32? _startPoint;
    private PointInt32? _endPoint;
    private Surface? _previewSurface;

    public RectangleTool() : base("Rectangle") { CursorName = "Crosshair"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_activeLayer == null) return;
        if (buttons.HasFlag(MouseButtons.Left))
        {
            _startPoint = point;
            _endPoint = point;
            _previewSurface = _activeLayer.Surface?.Clone();
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue) return;
        _endPoint = point;

        if (_previewSurface != null && _activeLayer != null)
        {
            _activeLayer.Surface?.CopyFrom(_previewSurface);
            DrawRect(_activeLayer.Surface!, _startPoint.Value, _endPoint.Value, PrimaryColor, SecondaryColor);
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue || !_endPoint.HasValue) return;
        if (_activeLayer == null) return;

        if (_previewSurface != null) _previewSurface.Dispose();
        _previewSurface = null;

        DrawRect(_activeLayer.Surface!, _startPoint.Value, _endPoint.Value, PrimaryColor, SecondaryColor);
        _activeLayer.InvalidateBounds();

        _startPoint = null;
        _endPoint = null;
    }

    private void DrawRect(Surface surface, PointInt32 p1, PointInt32 p2, ColorBgra outline, ColorBgra fill)
    {
        int x1 = Math.Min(p1.X, p2.X);
        int y1 = Math.Min(p1.Y, p2.Y);
        int x2 = Math.Max(p1.X, p2.X);
        int y2 = Math.Max(p1.Y, p2.Y);

        var rect = new RectInt32(x1, y1, x2 - x1, y2 - y1);

        if (Mode == RectangleMode.Filled || Mode == RectangleMode.FilledOutline)
            surface.FillRect(rect, fill);

        if (Mode == RectangleMode.Outline || Mode == RectangleMode.FilledOutline)
            surface.DrawRect(rect, outline);
    }
}

public sealed class EllipseTool : Tool
{
    public enum EllipseMode { Outline, Filled, FilledOutline }
    public EllipseMode Mode { get; set; } = EllipseMode.FilledOutline;

    private PointInt32? _startPoint;
    private PointInt32? _endPoint;
    private Surface? _previewSurface;

    public EllipseTool() : base("Ellipse") { CursorName = "Crosshair"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_activeLayer == null) return;
        if (buttons.HasFlag(MouseButtons.Left))
        {
            _startPoint = point;
            _endPoint = point;
            _previewSurface = _activeLayer.Surface?.Clone();
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue) return;
        _endPoint = point;

        if (_previewSurface != null && _activeLayer != null)
        {
            _activeLayer.Surface?.CopyFrom(_previewSurface);
            DrawEllipse(_activeLayer.Surface!, _startPoint.Value, _endPoint.Value, PrimaryColor, SecondaryColor);
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue || !_endPoint.HasValue) return;
        if (_activeLayer == null) return;

        if (_previewSurface != null) _previewSurface.Dispose();
        _previewSurface = null;

        DrawEllipse(_activeLayer.Surface!, _startPoint.Value, _endPoint.Value, PrimaryColor, SecondaryColor);
        _activeLayer.InvalidateBounds();

        _startPoint = null;
        _endPoint = null;
    }

    private void DrawEllipse(Surface surface, PointInt32 p1, PointInt32 p2, ColorBgra outline, ColorBgra fill)
    {
        int x1 = Math.Min(p1.X, p2.X);
        int y1 = Math.Min(p1.Y, p2.Y);
        int x2 = Math.Max(p1.X, p2.X);
        int y2 = Math.Max(p1.Y, p2.Y);

        int cx = (x1 + x2) / 2;
        int cy = (y1 + y2) / 2;
        int rx = Math.Max(1, (x2 - x1) / 2);
        int ry = Math.Max(1, (y2 - y1) / 2);

        for (int y = -ry; y <= ry; y++)
        {
            int py = cy + y;
            if (py < 0 || py >= surface.Height) continue;

            double dyNorm = (double)y / ry;
            int dx = (int)Math.Round(rx * Math.Sqrt(Math.Max(0, 1 - dyNorm * dyNorm)));

            int left = cx - dx;
            int right = cx + dx;

            if (Mode == EllipseMode.Filled || Mode == EllipseMode.FilledOutline)
            {
                var row = surface.GetRowSpanMutable(py);
                for (int x = Math.Max(0, left); x <= Math.Min(surface.Width - 1, right); x++)
                    row[x] = fill;
            }

            if (Mode == EllipseMode.Outline || Mode == EllipseMode.FilledOutline)
            {
                surface.SafeSetPixel(left, py, outline);
                surface.SafeSetPixel(right, py, outline);
            }
        }
    }
}

public sealed class SelectionTool : Tool
{
    public enum SelectionMode { Rectangle, Ellipse, Lasso, MagicWand }
    public SelectionMode Mode { get; set; } = SelectionMode.Rectangle;
    public float Tolerance { get; set; } = 0.1f;

    private PointInt32? _startPoint;
    private PointInt32? _currentPoint;
    private Surface? _previewSurface;
    private bool[,]? _selectionMask;

    public bool[,]? SelectionMask => _selectionMask;
    public bool HasSelection => _selectionMask != null;

    public SelectionTool() : base("Selection") { CursorName = "Crosshair"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_activeLayer == null || _document == null) return;
        if (!buttons.HasFlag(MouseButtons.Left)) return;

        if (Mode == SelectionMode.MagicWand)
        {
            CreateMagicWandSelection(point);
            return;
        }

        _startPoint = point;
        _currentPoint = point;
        _previewSurface = _activeLayer.Surface?.Clone();
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue) return;
        _currentPoint = point;

        if (_previewSurface != null && _activeLayer != null && Mode != SelectionMode.Lasso)
        {
            _activeLayer.Surface?.CopyFrom(_previewSurface);
            DrawSelectionPreview(_activeLayer.Surface!, _startPoint.Value, _currentPoint.Value);
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_startPoint.HasValue || !_currentPoint.HasValue) return;

        CreateSelection(_startPoint.Value, _currentPoint.Value);

        if (_previewSurface != null) _previewSurface.Dispose();
        _previewSurface = null;
        _startPoint = null;
        _currentPoint = null;
    }

    private void CreateSelection(PointInt32 start, PointInt32 end)
    {
        if (_activeLayer == null) return;

        int w = _activeLayer.Bounds.Width;
        int h = _activeLayer.Bounds.Height;
        _selectionMask = new bool[w, h];

        int x1 = Math.Min(start.X, end.X);
        int y1 = Math.Min(start.Y, end.Y);
        int x2 = Math.Max(start.X, end.X);
        int y2 = Math.Max(start.Y, end.Y);

        for (int y = y1; y <= y2; y++)
            for (int x = x1; x <= x2; x++)
                if (x >= 0 && x < w && y >= 0 && y < h)
                    _selectionMask[x, y] = true;
    }

    private void CreateMagicWandSelection(PointInt32 point)
    {
        if (_activeLayer == null) return;
        var surface = _activeLayer.Surface;
        if (surface == null) return;

        if (point.X < 0 || point.X >= surface.Width || point.Y < 0 || point.Y >= surface.Height) return;

        var target = surface[point.X, point.Y];
        int w = surface.Width, h = surface.Height;
        _selectionMask = new bool[w, h];

        byte tolR = (byte)(target.R * Tolerance * 255);
        byte tolG = (byte)(target.G * Tolerance * 255);
        byte tolB = (byte)(target.B * Tolerance * 255);
        byte tolA = (byte)(target.A * Tolerance * 255);

        var stack = new Stack<PointInt32>();
        stack.Push(point);

        while (stack.Count > 0)
        {
            var pt = stack.Pop();
            if (pt.X < 0 || pt.X >= w || pt.Y < 0 || pt.Y >= h) continue;
            if (_selectionMask[pt.X, pt.Y]) continue;

            var current = surface[pt.X, pt.Y];
            if (Math.Abs(current.R - target.R) > tolR ||
                Math.Abs(current.G - target.G) > tolG ||
                Math.Abs(current.B - target.B) > tolB ||
                Math.Abs(current.A - target.A) > tolA)
                continue;

            _selectionMask[pt.X, pt.Y] = true;

            stack.Push(new PointInt32(pt.X + 1, pt.Y));
            stack.Push(new PointInt32(pt.X - 1, pt.Y));
            stack.Push(new PointInt32(pt.X, pt.Y + 1));
            stack.Push(new PointInt32(pt.X, pt.Y - 1));
        }
    }

    private void DrawSelectionPreview(Surface surface, PointInt32 start, PointInt32 end)
    {
        int x1 = Math.Min(start.X, end.X);
        int y1 = Math.Min(start.Y, end.Y);
        int x2 = Math.Max(start.X, end.X);
        int y2 = Math.Max(start.Y, end.Y);

        var color = new ColorBgra(0, 120, 215, 255);

        for (int x = x1; x <= x2; x++)
        {
            surface.SafeSetPixel(x, y1, color);
            surface.SafeSetPixel(x, y2, color);
        }
        for (int y = y1; y <= y2; y++)
        {
            surface.SafeSetPixel(x1, y, color);
            surface.SafeSetPixel(x2, y, color);
        }
    }

    public void ClearSelection()
    {
        _selectionMask = null;
    }

    public void InvertSelection()
    {
        if (_selectionMask == null) return;
        int w = _selectionMask.GetLength(0);
        int h = _selectionMask.GetLength(1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                _selectionMask[x, y] = !_selectionMask[x, y];
    }
}

public sealed class MoveTool : Tool
{
    private PointInt32? _dragStart;
    private PointInt32? _layerStartPos;
    private Surface? _layerContent;
    private Surface? _previewSurface;

    public MoveTool() : base("Move") { CursorName = "SizeAll"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_activeLayer == null) return;
        if (buttons.HasFlag(MouseButtons.Left))
        {
            _dragStart = point;
            _layerStartPos = new PointInt32(_activeLayer.Bounds.X, _activeLayer.Bounds.Y);
            _layerContent = _activeLayer.Surface?.Clone();
            _previewSurface = _document?.Render();
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_dragStart.HasValue || _activeLayer == null || _document == null) return;
        if (!buttons.HasFlag(MouseButtons.Left)) return;

        int dx = point.X - _dragStart.Value.X;
        int dy = point.Y - _dragStart.Value.Y;

        // For now, just offset the layer content
        // In a full implementation, this would update layer transform
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_dragStart.HasValue) return;

        _layerContent?.Dispose();
        _layerContent = null;
        _previewSurface?.Dispose();
        _previewSurface = null;
        _dragStart = null;
        _layerStartPos = null;
    }
}

public sealed class ZoomTool : Tool
{
    public ZoomTool() : base("Zoom") { CursorName = "ZoomIn"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_document == null) return;
        // Zoom logic would be handled by the view, not the document
        ReportStatus(buttons.HasFlag(MouseButtons.Right) ? "Zoom Out" : "Zoom In");
    }
}

public sealed class PanTool : Tool
{
    private PointInt32? _lastPoint;

    public PanTool() : base("Pan") { CursorName = "Hand"; }

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (buttons.HasFlag(MouseButtons.Left) || buttons.HasFlag(MouseButtons.Middle))
        {
            _lastPoint = point;
        }
    }

    public override void OnMouseMove(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (!_lastPoint.HasValue) return;
        if (buttons.HasFlag(MouseButtons.Left) || buttons.HasFlag(MouseButtons.Middle))
        {
            int dx = point.X - _lastPoint.Value.X;
            int dy = point.Y - _lastPoint.Value.Y;
            _lastPoint = point;
            ReportStatus($"Pan: {dx}, {dy}");
        }
    }

    public override void OnMouseUp(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        _lastPoint = null;
    }
}

public sealed class ColorPickerTool : Tool
{
    public ColorPickerTool() : base("Color Picker") { CursorName = "Crosshair"; }

    public event EventHandler<ColorPickedEventArgs>? ColorPicked;

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (_document == null) return;
        if (!buttons.HasFlag(MouseButtons.Left) && !buttons.HasFlag(MouseButtons.Right)) return;

        var rendered = _document.Render();
        if (point.X >= 0 && point.X < rendered.Width && point.Y >= 0 && point.Y < rendered.Height)
        {
            var color = rendered[point.X, point.Y];
            ColorPicked?.Invoke(this, new ColorPickedEventArgs(color, buttons.HasFlag(MouseButtons.Right)));
            ReportStatus($"Picked: {color}");
        }
        rendered.Dispose();
    }
}

public sealed class ColorPickedEventArgs : EventArgs
{
    public ColorBgra Color { get; }
    public bool IsSecondary { get; }
    public ColorPickedEventArgs(ColorBgra color, bool isSecondary)
    {
        Color = color; IsSecondary = isSecondary;
    }
}

public sealed class TextTool : Tool
{
    public TextTool() : base("Text") { CursorName = "IBeam"; }

    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14;
    public bool Bold { get; set; } = false;
    public bool Italic { get; set; } = false;

    private PointInt32? _textPosition;

    public override void OnMouseDown(PointInt32 point, MouseButtons buttons, KeyModifiers modifiers)
    {
        if (buttons.HasFlag(MouseButtons.Left))
        {
            _textPosition = point;
            ReportStatus($"Text position set at {point}");
        }
    }
}

public static class ToolRegistry
{
    private static readonly Dictionary<string, Func<Tool>> _tools = new()
    {
        ["PaintBrush"] = () => new PaintBrushTool(),
        ["Eraser"] = () => new EraserTool(),
        ["Pencil"] = () => new PencilTool(),
        ["Fill"] = () => new FillTool(),
        ["Gradient"] = () => new GradientTool(),
        ["Line"] = () => new LineTool(),
        ["Rectangle"] = () => new RectangleTool(),
        ["Ellipse"] = () => new EllipseTool(),
        ["Selection"] = () => new SelectionTool(),
        ["Move"] = () => new MoveTool(),
        ["Zoom"] = () => new ZoomTool(),
        ["Pan"] = () => new PanTool(),
        ["ColorPicker"] = () => new ColorPickerTool(),
        ["Text"] = () => new TextTool()
    };

    public static IReadOnlyDictionary<string, Func<Tool>> Tools => _tools;
    public static Tool Create(string name) => _tools.TryGetValue(name, out var factory) ? factory() : throw new ArgumentException($"Unknown tool: {name}");
    public static IEnumerable<string> ToolNames => _tools.Keys;
}