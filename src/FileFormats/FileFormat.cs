namespace PaintNetClone.FileFormats;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using PaintNetClone.Core.Document;
using PaintNetClone.Core.Rendering;
using PaintNetClone.Core.Geometry;
using PaintNetClone.Core.Color;
using PaintNetClone.Layers;

public abstract class FileFormat : IDisposable
{
    public string Name { get; }
    public string Extension { get; }
    public string Description { get; }
    public bool SupportsLayers { get; }
    public bool SupportsTransparency { get; }
    public bool SupportsAnimation { get; }

    protected FileFormat(string name, string extension, string description, bool supportsLayers, bool supportsTransparency, bool supportsAnimation = false)
    {
        Name = name;
        Extension = extension;
        Description = description;
        SupportsLayers = supportsLayers;
        SupportsTransparency = supportsTransparency;
        SupportsAnimation = supportsAnimation;
    }

    public abstract Document Load(Stream stream);
    public abstract void Save(Document document, Stream stream);
    public virtual void Dispose() { }

    public static Document LoadFromFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var format = GetByExtension(ext);
        if (format == null) throw new NotSupportedException($"Unsupported file format: {ext}");

        using var stream = File.OpenRead(path);
        return format.Load(stream);
    }

    public static void SaveToFile(Document document, string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var format = GetByExtension(ext);
        if (format == null) throw new NotSupportedException($"Unsupported file format: {ext}");

        using var stream = File.Create(path);
        format.Save(document, stream);
    }

    private static FileFormat? GetByExtension(string ext)
    {
        return ext switch
        {
            ".png" => PngFormat.Instance,
            ".jpg" or ".jpeg" => JpegFormat.Instance,
            ".bmp" => BmpFormat.Instance,
            ".tiff" or ".tif" => TiffFormat.Instance,
            ".gif" => GifFormat.Instance,
            ".pdnc" => PdncFormat.Instance,
            _ => null
        };
    }
}

public sealed class PngFormat : FileFormat
{
    public static readonly PngFormat Instance = new();

    private PngFormat() : base("PNG", ".png", "Portable Network Graphics", false, true) { }

    public override Document Load(Stream stream)
    {
        using var image = System.Drawing.Image.FromStream(stream);
        using var bitmap = new System.Drawing.Bitmap(image);
        return LoadFromBitmap(bitmap);
    }

    public override void Save(Document document, Stream stream)
    {
        var bitmap = document.Render().ToBitmap();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        bitmap.Dispose();
    }

    private static Document LoadFromBitmap(System.Drawing.Bitmap bitmap)
    {
        var doc = new Document(bitmap.Width, bitmap.Height);
        var layer = doc.AddBitmapLayer("Background");
        var surface = layer.Surface!;

        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* row = ptr + y * stride;
                var surfaceRow = surface.GetRowSpanMutable(y);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    surfaceRow[x] = new ColorBgra(row[x * 4], row[x * 4 + 1], row[x * 4 + 2], row[x * 4 + 3]);
                }
            }
        }

        bitmap.UnlockBits(data);
        return doc;
    }
}

public sealed class JpegFormat : FileFormat
{
    public static readonly JpegFormat Instance = new();

    private JpegFormat() : base("JPEG", ".jpg", "Joint Photographic Experts Group", false, false) { }

    public int Quality { get; set; } = 90;

    public override Document Load(Stream stream)
    {
        using var image = System.Drawing.Image.FromStream(stream);
        using var bitmap = new System.Drawing.Bitmap(image);
        return LoadFromBitmap(bitmap);
    }

    public override void Save(Document document, Stream stream)
    {
        var bitmap = document.Render().ToBitmap();
        var encoder = GetEncoder(System.Drawing.Imaging.ImageFormat.Jpeg);
        var params_ = new System.Drawing.Imaging.EncoderParameters(1);
        params_.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, Quality);
        bitmap.Save(stream, encoder, params_);
        bitmap.Dispose();
    }

    private static Document LoadFromBitmap(System.Drawing.Bitmap bitmap)
    {
        var doc = new Document(bitmap.Width, bitmap.Height);
        var layer = doc.AddBitmapLayer("Background");
        var surface = layer.Surface!;

        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* row = ptr + y * stride;
                var surfaceRow = surface.GetRowSpanMutable(y);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    surfaceRow[x] = new ColorBgra(row[x * 4], row[x * 4 + 1], row[x * 4 + 2], row[x * 4 + 3]);
                }
            }
        }

        bitmap.UnlockBits(data);
        return doc;
    }

    private static System.Drawing.Imaging.ImageCodecInfo GetEncoder(System.Drawing.Imaging.ImageFormat format)
    {
        foreach (var codec in System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders())
            if (codec.FormatID == format.Guid) return codec;
        throw new InvalidOperationException("No encoder found");
    }
}

public sealed class BmpFormat : FileFormat
{
    public static readonly BmpFormat Instance = new();

    private BmpFormat() : base("BMP", ".bmp", "Windows Bitmap", false, false) { }

    public override Document Load(Stream stream)
    {
        using var image = System.Drawing.Image.FromStream(stream);
        using var bitmap = new System.Drawing.Bitmap(image);
        return LoadFromBitmap(bitmap);
    }

    public override void Save(Document document, Stream stream)
    {
        var bitmap = document.Render().ToBitmap();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
        bitmap.Dispose();
    }

    private static Document LoadFromBitmap(System.Drawing.Bitmap bitmap)
    {
        var doc = new Document(bitmap.Width, bitmap.Height);
        var layer = doc.AddBitmapLayer("Background");
        var surface = layer.Surface!;

        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* row = ptr + y * stride;
                var surfaceRow = surface.GetRowSpanMutable(y);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    surfaceRow[x] = new ColorBgra(row[x * 4], row[x * 4 + 1], row[x * 4 + 2], row[x * 4 + 3]);
                }
            }
        }

        bitmap.UnlockBits(data);
        return doc;
    }
}

public sealed class TiffFormat : FileFormat
{
    public static readonly TiffFormat Instance = new();

    private TiffFormat() : base("TIFF", ".tiff", "Tagged Image File Format", true, true) { }

    public override Document Load(Stream stream)
    {
        using var image = System.Drawing.Image.FromStream(stream);
        using var bitmap = new System.Drawing.Bitmap(image);
        return LoadFromBitmap(bitmap);
    }

    public override void Save(Document document, Stream stream)
    {
        var bitmap = document.Render().ToBitmap();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Tiff);
        bitmap.Dispose();
    }

    private static Document LoadFromBitmap(System.Drawing.Bitmap bitmap)
    {
        var doc = new Document(bitmap.Width, bitmap.Height);
        var layer = doc.AddBitmapLayer("Background");
        var surface = layer.Surface!;

        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* row = ptr + y * stride;
                var surfaceRow = surface.GetRowSpanMutable(y);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    surfaceRow[x] = new ColorBgra(row[x * 4], row[x * 4 + 1], row[x * 4 + 2], row[x * 4 + 3]);
                }
            }
        }

        bitmap.UnlockBits(data);
        return doc;
    }
}

public sealed class GifFormat : FileFormat
{
    public static readonly GifFormat Instance = new();

    private GifFormat() : base("GIF", ".gif", "Graphics Interchange Format", false, true, true) { }

    public override Document Load(Stream stream)
    {
        using var image = System.Drawing.Image.FromStream(stream);
        using var bitmap = new System.Drawing.Bitmap(image);
        return LoadFromBitmap(bitmap);
    }

    public override void Save(Document document, Stream stream)
    {
        var bitmap = document.Render().ToBitmap();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Gif);
        bitmap.Dispose();
    }

    private static Document LoadFromBitmap(System.Drawing.Bitmap bitmap)
    {
        var doc = new Document(bitmap.Width, bitmap.Height);
        var layer = doc.AddBitmapLayer("Background");
        var surface = layer.Surface!;

        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* row = ptr + y * stride;
                var surfaceRow = surface.GetRowSpanMutable(y);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    surfaceRow[x] = new ColorBgra(row[x * 4], row[x * 4 + 1], row[x * 4 + 2], row[x * 4 + 3]);
                }
            }
        }

        bitmap.UnlockBits(data);
        return doc;
    }
}

public sealed class PdncFormat : FileFormat
{
    public static readonly PdncFormat Instance = new();

    private PdncFormat() : base("Paint.NET Clone", ".pdnc", "Paint.NET Clone Native Format", true, true) { }

    public override Document Load(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        string magic = new string(reader.ReadChars(8));
        if (magic != "PDNC0001") throw new InvalidDataException("Invalid PDNC file");

        int version = reader.ReadInt32();
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int layerCount = reader.ReadInt32();

        var doc = new Document(width, height);

        for (int i = 0; i < layerCount; i++)
        {
            string name = ReadString(reader);
            bool visible = reader.ReadBoolean();
            double opacity = reader.ReadDouble();
            int blendMode = reader.ReadInt32();
            string type = ReadString(reader);

            Layer layer = type switch
            {
                "Bitmap" => LoadBitmapLayer(reader, name, visible, opacity, (BlendMode)blendMode),
                "Group" => LoadGroupLayer(reader, name, visible, opacity, (BlendMode)blendMode),
                _ => throw new InvalidDataException($"Unknown layer type: {type}")
            };

            doc.AddLayer(layer);
        }

        return doc;
    }

    public override void Save(Document document, Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write("PDNC0001".ToCharArray());
        writer.Write(1);
        writer.Write(document.Width);
        writer.Write(document.Height);
        writer.Write(document.LayerCount);

        foreach (var layer in document.Layers)
        {
            WriteString(writer, layer.Name);
            writer.Write(layer.Visible);
            writer.Write(layer.Opacity);
            writer.Write((int)layer.BlendMode);
            WriteString(writer, layer.GetType().Name.Replace("Layer", ""));

            if (layer is BitmapLayer bmp)
                SaveBitmapLayer(writer, bmp);
            else if (layer is GroupLayer group)
                SaveGroupLayer(writer, group);
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        int len = reader.ReadInt32();
        var bytes = reader.ReadBytes(len);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void SaveBitmapLayer(BinaryWriter writer, BitmapLayer layer)
    {
        var surface = layer.Surface;
        if (surface == null)
        {
            writer.Write(0);
            return;
        }

        writer.Write(surface.Width);
        writer.Write(surface.Height);
        writer.Write(surface.Pixels.Length);

        foreach (var pixel in surface.Pixels)
        {
            writer.Write(pixel.B);
            writer.Write(pixel.G);
            writer.Write(pixel.R);
            writer.Write(pixel.A);
        }
    }

    private static BitmapLayer LoadBitmapLayer(BinaryReader reader, string name, bool visible, double opacity, BlendMode blendMode)
    {
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int pixelCount = reader.ReadInt32();

        var layer = new BitmapLayer(name, width, height);
        layer.Visible = visible;
        layer.Opacity = opacity;
        layer.BlendMode = blendMode;

        var surface = layer.Surface!;
        for (int i = 0; i < pixelCount; i++)
        {
            byte b = reader.ReadByte();
            byte g = reader.ReadByte();
            byte r = reader.ReadByte();
            byte a = reader.ReadByte();
            surface.Pixels[i] = new ColorBgra(b, g, r, a);
        }

        return layer;
    }

    private static void SaveGroupLayer(BinaryWriter writer, GroupLayer layer)
    {
        writer.Write(layer.Children.Count);
        foreach (var child in layer.Children)
        {
            WriteString(writer, child.Name);
            writer.Write(child.Visible);
            writer.Write(child.Opacity);
            writer.Write((int)child.BlendMode);
            WriteString(writer, child.GetType().Name.Replace("Layer", ""));

            if (child is BitmapLayer bmp)
                SaveBitmapLayer(writer, bmp);
            else if (child is GroupLayer g)
                SaveGroupLayer(writer, g);
        }
    }

    private static GroupLayer LoadGroupLayer(BinaryReader reader, string name, bool visible, double opacity, BlendMode blendMode)
    {
        int childCount = reader.ReadInt32();
        var group = new GroupLayer(name);
        group.Visible = visible;
        group.Opacity = opacity;
        group.BlendMode = blendMode;

        for (int i = 0; i < childCount; i++)
        {
            string childName = ReadString(reader);
            bool childVisible = reader.ReadBoolean();
            double childOpacity = reader.ReadDouble();
            int childBlendMode = reader.ReadInt32();
            string childType = ReadString(reader);

            Layer child = childType switch
            {
                "Bitmap" => LoadBitmapLayer(reader, childName, childVisible, childOpacity, (BlendMode)childBlendMode),
                "Group" => LoadGroupLayer(reader, childName, childVisible, childOpacity, (BlendMode)childBlendMode),
                _ => throw new InvalidDataException($"Unknown layer type: {childType}")
            };

            group.AddChild(child);
        }

        return group;
    }
}

public static class SurfaceExtensions
{
    public static System.Drawing.Bitmap ToBitmap(this Surface surface)
    {
        var bitmap = new System.Drawing.Bitmap(surface.Width, surface.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, surface.Width, surface.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < surface.Height; y++)
            {
                byte* row = ptr + y * stride;
                var surfaceRow = surface.GetRowSpan(y);
                for (int x = 0; x < surface.Width; x++)
                {
                    var p = surfaceRow[x];
                    row[x * 4] = p.B;
                    row[x * 4 + 1] = p.G;
                    row[x * 4 + 2] = p.R;
                    row[x * 4 + 3] = p.A;
                }
            }
        }

        bitmap.UnlockBits(data);
        return bitmap;
    }
}