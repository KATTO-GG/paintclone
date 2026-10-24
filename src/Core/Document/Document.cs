namespace PaintNetClone.Core.Document;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using PaintNetClone.Layers;
using PaintNetClone.Core.Geometry;
using PaintNetClone.Core.Rendering;
using PaintNetClone.Core.Color;

public sealed class Document : IDisposable
{
    private readonly List<Layer> _layers = new();
    private readonly Stack<DocumentMemento> _undoStack = new();
    private readonly Stack<DocumentMemento> _redoStack = new();
    private int _maxUndoSteps = 100;
    private bool _disposed;
    private SizeInt32 _size;
    private ColorBgra _backgroundColor = ColorBgra.White;

    public SizeInt32 Size => _size;
    public int Width => _size.Width;
    public int Height => _size.Height;
    public ColorBgra BackgroundColor { get => _backgroundColor; set { _backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); } }
    public IReadOnlyList<Layer> Layers => _layers;
    public int LayerCount => _layers.Count;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int MaxUndoSteps { get => _maxUndoSteps; set => _maxUndoSteps = Math.Max(1, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<LayerEventArgs>? LayerAdded;
    public event EventHandler<LayerEventArgs>? LayerRemoved;
    public event EventHandler<LayerEventArgs>? LayerChanged;
    public event EventHandler? UndoStackChanged;
    public event EventHandler? RedoStackChanged;

    public Document(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Size must be positive");
        _size = new SizeInt32(width, height);
    }

    public Layer AddLayer(Layer layer, int? index = null)
    {
        if (layer == null) throw new ArgumentNullException(nameof(layer));

        layer.PropertyChanged += OnLayerPropertyChanged;
        layer.Invalidated += OnLayerInvalidated;

        int insertIndex = index ?? _layers.Count;
        insertIndex = Math.Clamp(insertIndex, 0, _layers.Count);
        _layers.Insert(insertIndex, layer);

        if (layer is GroupLayer group)
        {
            foreach (var child in group.Children)
                RegisterChildLayer(child);
        }

        SaveUndoState();
        LayerAdded?.Invoke(this, new LayerEventArgs(layer));
        OnPropertyChanged(nameof(Layers));
        OnPropertyChanged(nameof(LayerCount));
        return layer;
    }

    public BitmapLayer AddBitmapLayer(string name, int? index = null)
    {
        var layer = new BitmapLayer(name, Width, Height);
        return (BitmapLayer)AddLayer(layer, index);
    }

    public GroupLayer AddGroupLayer(string name, int? index = null)
    {
        var layer = new GroupLayer(name);
        return (GroupLayer)AddLayer(layer, index);
    }

    private void RegisterChildLayer(Layer child)
    {
        child.PropertyChanged += OnLayerPropertyChanged;
        child.Invalidated += OnLayerInvalidated;
        if (child is GroupLayer g)
            foreach (var c in g.Children) RegisterChildLayer(c);
    }

    public bool RemoveLayer(Layer layer)
    {
        if (!_layers.Remove(layer)) return false;

        layer.PropertyChanged -= OnLayerPropertyChanged;
        layer.Invalidated -= OnLayerInvalidated;
        layer.Dispose();

        SaveUndoState();
        LayerRemoved?.Invoke(this, new LayerEventArgs(layer));
        OnPropertyChanged(nameof(Layers));
        OnPropertyChanged(nameof(LayerCount));
        return true;
    }

    public void MoveLayer(Layer layer, int newIndex)
    {
        int oldIndex = _layers.IndexOf(layer);
        if (oldIndex < 0) throw new ArgumentException("Layer not in document");
        if (oldIndex == newIndex) return;

        newIndex = Math.Clamp(newIndex, 0, _layers.Count - 1);
        _layers.RemoveAt(oldIndex);
        if (newIndex > oldIndex) newIndex--;
        _layers.Insert(newIndex, layer);

        SaveUndoState();
        LayerChanged?.Invoke(this, new LayerEventArgs(layer));
    }

    public void SwapLayers(int index1, int index2)
    {
        if (index1 < 0 || index1 >= _layers.Count || index2 < 0 || index2 >= _layers.Count)
            throw new ArgumentOutOfRangeException();
        if (index1 == index2) return;

        (_layers[index1], _layers[index2]) = (_layers[index2], _layers[index1]);
        SaveUndoState();
        LayerChanged?.Invoke(this, new LayerEventArgs(_layers[index1]));
        LayerChanged?.Invoke(this, new LayerEventArgs(_layers[index2]));
    }

    public void Resize(int width, int height)
    {
        if (width == Width && height == Height) return;
        if (width <= 0 || height <= 0) throw new ArgumentException("Size must be positive");

        _size = new SizeInt32(width, height);
        foreach (var layer in _layers)
        {
            if (layer is BitmapLayer bmp)
                bmp.Resize(width, height);
        }

        SaveUndoState();
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
    }

    public void Crop(RectInt32 rect)
    {
        rect = rect.Intersect(new RectInt32(0, 0, Width, Height));
        if (rect.Width <= 0 || rect.Height <= 0) return;

        foreach (var layer in _layers)
        {
            if (layer is BitmapLayer bmp)
                bmp.Crop(rect);
        }

        _size = new SizeInt32(rect.Width, rect.Height);
        SaveUndoState();
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
    }

    public Surface Render(RectInt32? region = null)
    {
        var bounds = region ?? new RectInt32(0, 0, Width, Height);
        var result = new Surface(bounds.Width, bounds.Height, _backgroundColor);

        foreach (var layer in _layers)
        {
            if (!layer.Visible) continue;
            var surface = layer.GetSurface();
            if (surface == null) continue;

            var layerBounds = layer.Bounds;
            var drawRect = layerBounds.Intersect(bounds);
            if (drawRect.Width <= 0 || drawRect.Height <= 0) continue;

            var destPt = new PointInt32(drawRect.X - bounds.X, drawRect.Y - bounds.Y);
            result.CopyFrom(surface, drawRect, destPt);
        }

        return result;
    }

    public Surface RenderToSurface(Surface target, RectInt32? region = null)
    {
        var rendered = Render(region);
        var bounds = region ?? new RectInt32(0, 0, Width, Height);
        var destPt = new PointInt32(bounds.X, bounds.Y);
        target.CopyFrom(rendered, rendered.Bounds, destPt);
        rendered.Dispose();
        return target;
    }

    private void OnLayerPropertyChanged(object? sender, EventArgs e)
    {
        if (sender is Layer layer)
        {
            SaveUndoState();
            LayerChanged?.Invoke(this, new LayerEventArgs(layer));
        }
    }

    private void OnLayerInvalidated(object? sender, EventArgs e)
    {
        SaveUndoState();
    }

    private void SaveUndoState()
    {
        _undoStack.Push(new DocumentMemento(this));
        if (_undoStack.Count > _maxUndoSteps)
        {
            var overflow = _undoStack.Count - _maxUndoSteps;
            var temp = new Stack<DocumentMemento>();
            for (int i = 0; i < overflow; i++) temp.Push(_undoStack.Pop());
            _undoStack.Clear();
            while (temp.Count > 0) _undoStack.Push(temp.Pop());
        }
        _redoStack.Clear();
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        RedoStackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redoStack.Push(new DocumentMemento(this));
        RestoreMemento(_undoStack.Pop());
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        RedoStackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undoStack.Push(new DocumentMemento(this));
        RestoreMemento(_redoStack.Pop());
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        RedoStackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreMemento(DocumentMemento memento)
    {
        foreach (var layer in _layers) layer.Dispose();
        _layers.Clear();

        foreach (var layerData in memento.Layers)
        {
            Layer layer = layerData.Type switch
            {
                "Bitmap" => new BitmapLayer(layerData.Name, layerData.Width, layerData.Height),
                "Group" => new GroupLayer(layerData.Name),
                _ => throw new NotSupportedException($"Unknown layer type: {layerData.Type}")
            };

            layer.Visible = layerData.Visible;
            layer.Opacity = layerData.Opacity;
            layer.BlendMode = layerData.BlendMode;
            layer.SetParent(null);

            if (layer is BitmapLayer bmp && layerData.Pixels != null)
            {
                bmp.SetSurface(new Surface(layerData.Pixels, layerData.Width, layerData.Height));
            }

            _layers.Add(layer);
        }

        _size = memento.Size;
        _backgroundColor = memento.BackgroundColor;
        OnPropertyChanged(nameof(Layers));
        OnPropertyChanged(nameof(LayerCount));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(BackgroundColor));
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var layer in _layers) layer.Dispose();
            _layers.Clear();
            _disposed = true;
        }
    }
}

public sealed class DocumentMemento
{
    public List<LayerData> Layers { get; } = new();
    public SizeInt32 Size { get; private set; }
    public ColorBgra BackgroundColor { get; private set; }

    public DocumentMemento(Document doc)
    {
        Size = doc.Size;
        BackgroundColor = doc.BackgroundColor;
        foreach (var layer in doc.Layers) CaptureLayer(layer);
    }

    private void CaptureLayer(Layer layer)
    {
        var data = new LayerData
        {
            Name = layer.Name,
            Visible = layer.Visible,
            Opacity = layer.Opacity,
            BlendMode = layer.BlendMode,
            Type = layer.GetType().Name.Replace("Layer", "")
        };

        if (layer is BitmapLayer bmp)
        {
            var surface = bmp.Surface;
            if (surface != null)
            {
                data.Width = surface.Width;
                data.Height = surface.Height;
                data.Pixels = (ColorBgra[])surface.Pixels.Clone();
            }
        }
        else if (layer is GroupLayer group)
        {
            foreach (var child in group.Children) CaptureLayer(child);
        }

        Layers.Add(data);
    }
}

public sealed class LayerData
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Visible { get; set; }
    public double Opacity { get; set; }
    public BlendMode BlendMode { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ColorBgra[]? Pixels { get; set; }
}

public sealed class LayerEventArgs : EventArgs
{
    public Layer Layer { get; }
    public LayerEventArgs(Layer layer) { Layer = layer; }
}