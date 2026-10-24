namespace PaintNetClone.Effects;

using System;
using System.Collections.Generic;
using PaintNetClone.Core.Rendering;
using PaintNetClone.Core.Color;
using PaintNetClone.Core.Geometry;

public abstract class Effect : IDisposable
{
    private bool _disposed;

    public string Name { get; }
    public string Category { get; }
    public string Submenu { get; }
    public bool IsConfigurable { get; protected set; } = true;

    protected Effect(string name, string category, string submenu = "")
    {
        Name = name;
        Category = category;
        Submenu = submenu;
    }

    public abstract void Render(Surface source, Surface destination, RectInt32[] rois, int startIndex, int length);

    public virtual EffectConfigDialog? CreateConfigDialog() => null;

    public void Dispose()
    {
        if (!_disposed) _disposed = true;
    }
}

public abstract class EffectConfigDialog
{
    public Effect Effect { get; }
    public event EventHandler? SettingsChanged;

    protected EffectConfigDialog(Effect effect) { Effect = effect; }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
    public abstract void ResetToDefaults();
}

public abstract class SingleThreadedEffect : Effect
{
    protected SingleThreadedEffect(string name, string category, string submenu = "") : base(name, category, submenu) { }

    public sealed override void Render(Surface source, Surface destination, RectInt32[] rois, int startIndex, int length)
    {
        for (int i = startIndex; i < startIndex + length; i++)
        {
            var roi = rois[i];
            RenderRect(source, destination, roi);
        }
    }

    protected abstract void RenderRect(Surface source, Surface destination, RectInt32 roi);
}

public abstract class GaussianBlurEffectBase : SingleThreadedEffect
{
    protected double _radius = 5.0;

    public double Radius { get => _radius; set { _radius = Math.Max(0, value); OnSettingsChanged(); } }

    public event EventHandler? SettingsChanged;

    protected GaussianBlurEffectBase(string name) : base(name, "Blurs", "Gaussian Blur") { }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class GaussianBlurEffect : GaussianBlurEffectBase
{
    public GaussianBlurEffect() : base("Gaussian Blur") { }

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        if (_radius <= 0)
        {
            source.CopyFrom(source, roi, new PointInt32(roi.X, roi.Y));
            return;
        }

        int radius = (int)Math.Ceiling(_radius * 3);
        double sigma = _radius;
        var kernel = CreateGaussianKernel(radius, sigma);

        var temp = new Surface(source.Width, source.Height);
        HorizontalPass(source, temp, roi, kernel);
        VerticalPass(temp, destination, roi, kernel);
        temp.Dispose();
    }

    private static double[] CreateGaussianKernel(int radius, double sigma)
    {
        var kernel = new double[radius * 2 + 1];
        double sum = 0;
        for (int i = -radius; i <= radius; i++)
        {
            double x = i / sigma;
            kernel[i + radius] = Math.Exp(-x * x / 2);
            sum += kernel[i + radius];
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
        return kernel;
    }

    private void HorizontalPass(Surface src, Surface dst, RectInt32 roi, double[] kernel)
    {
        int radius = kernel.Length / 2;

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var srcRow = src.GetRowSpan(y);
            var dstRow = dst.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                double r = 0, g = 0, b = 0, a = 0;
                double weightSum = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int kpx = Math.Clamp(px + k, 0, src.Width - 1);
                    var pixel = srcRow[kpx];
                    double w = kernel[k + radius];

                    r += pixel.R * w;
                    g += pixel.G * w;
                    b += pixel.B * w;
                    a += pixel.A * w;
                    weightSum += w;
                }

                dstRow[px] = new ColorBgra(
                    (byte)Math.Round(r / weightSum),
                    (byte)Math.Round(g / weightSum),
                    (byte)Math.Round(b / weightSum),
                    (byte)Math.Round(a / weightSum)
                );
            }
        }
    }

    private void VerticalPass(Surface src, Surface dst, RectInt32 roi, double[] kernel)
    {
        int radius = kernel.Length / 2;

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var dstRow = dst.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                double r = 0, g = 0, b = 0, a = 0;
                double weightSum = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int py = Math.Clamp(y + k, 0, src.Height - 1);
                    var pixel = src.GetRowSpan(py)[px];
                    double w = kernel[k + radius];

                    r += pixel.R * w;
                    g += pixel.G * w;
                    b += pixel.B * w;
                    a += pixel.A * w;
                    weightSum += w;
                }

                dstRow[px] = new ColorBgra(
                    (byte)Math.Round(r / weightSum),
                    (byte)Math.Round(g / weightSum),
                    (byte)Math.Round(b / weightSum),
                    (byte)Math.Round(a / weightSum)
                );
            }
        }
    }

    public override EffectConfigDialog CreateConfigDialog() => new GaussianBlurDialog(this);
}

public sealed class GaussianBlurDialog : EffectConfigDialog
{
    public double Radius { get => ((GaussianBlurEffect)Effect).Radius; set { ((GaussianBlurEffect)Effect).Radius = value; OnSettingsChanged(); } }

    public GaussianBlurDialog(Effect effect) : base(effect) { }
    public override void ResetToDefaults() => Radius = 5.0;
}

public sealed class SharpenEffect : SingleThreadedEffect
{
    private double _amount = 1.0;

    public double Amount { get => _amount; set { _amount = Math.Max(0, value); OnSettingsChanged(); } }
    public event EventHandler? SettingsChanged;

    public SharpenEffect() : base("Sharpen", "Sharpen", "Sharpen") { }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        if (_amount <= 0)
        {
            source.CopyFrom(source, roi, new PointInt32(roi.X, roi.Y));
            return;
        }

        double[,] kernel = {
            { -_amount, -_amount, -_amount },
            { -_amount, 1 + 8 * _amount, -_amount },
            { -_amount, -_amount, -_amount }
        };

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            for (int px = roi.X; px < roi.Right; px++)
            {
                double r = 0, g = 0, b = 0, a = 0;

                for (int ky = -1; ky <= 1; ky++)
                {
                    int py = Math.Clamp(y + ky, 0, source.Height - 1);
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int kpx = Math.Clamp(px + kx, 0, source.Width - 1);
                        var pixel = source[px, py];
                        double w = kernel[ky + 1, kx + 1];

                        r += pixel.R * w;
                        g += pixel.G * w;
                        b += pixel.B * w;
                        a += pixel.A * w;
                    }
                }

                destination[px, y] = new ColorBgra(
                    (byte)Math.Clamp(Math.Round(r), 0, 255),
                    (byte)Math.Clamp(Math.Round(g), 0, 255),
                    (byte)Math.Clamp(Math.Round(b), 0, 255),
                    (byte)Math.Clamp(Math.Round(a), 0, 255)
                );
            }
        }
    }

    public override EffectConfigDialog CreateConfigDialog() => new SharpenDialog(this);
}

public sealed class SharpenDialog : EffectConfigDialog
{
    public double Amount { get => ((SharpenEffect)Effect).Amount; set { ((SharpenEffect)Effect).Amount = value; OnSettingsChanged(); } }

    public SharpenDialog(Effect effect) : base(effect) { }
    public override void ResetToDefaults() => Amount = 1.0;
}

public sealed class BrightnessContrastEffect : SingleThreadedEffect
{
    private double _brightness = 0;
    private double _contrast = 0;

    public double Brightness { get => _brightness; set { _brightness = Math.Clamp(value, -100, 100); OnSettingsChanged(); } }
    public double Contrast { get => _contrast; set { _contrast = Math.Clamp(value, -100, 100); OnSettingsChanged(); } }
    public event EventHandler? SettingsChanged;

    public BrightnessContrastEffect() : base("Brightness / Contrast", "Adjustments", "Brightness and Contrast") { }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        double b = _brightness / 100.0;
        double c = _contrast / 100.0;
        double contrastFactor = (259 * (c * 255 + 255)) / (255 * (259 - c * 255));

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var srcRow = source.GetRowSpan(y);
            var dstRow = destination.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                var pixel = srcRow[px];

                double rr = pixel.R / 255.0;
                double gg = pixel.G / 255.0;
                double bb = pixel.B / 255.0;

                rr = (rr - 0.5) * contrastFactor + 0.5 + b;
                gg = (gg - 0.5) * contrastFactor + 0.5 + b;
                bb = (bb - 0.5) * contrastFactor + 0.5 + b;

                dstRow[px] = new ColorBgra(
                    (byte)Math.Clamp(Math.Round(rr * 255), 0, 255),
                    (byte)Math.Clamp(Math.Round(gg * 255), 0, 255),
                    (byte)Math.Clamp(Math.Round(bb * 255), 0, 255),
                    pixel.A
                );
            }
        }
    }

    public override EffectConfigDialog CreateConfigDialog() => new BrightnessContrastDialog(this);
}

public sealed class BrightnessContrastDialog : EffectConfigDialog
{
    public double Brightness { get => ((BrightnessContrastEffect)Effect).Brightness; set { ((BrightnessContrastEffect)Effect).Brightness = value; OnSettingsChanged(); } }
    public double Contrast { get => ((BrightnessContrastEffect)Effect).Contrast; set { ((BrightnessContrastEffect)Effect).Contrast = value; OnSettingsChanged(); } }

    public BrightnessContrastDialog(Effect effect) : base(effect) { }
    public override void ResetToDefaults() { Brightness = 0; Contrast = 0; }
}

public sealed class HueSaturationLightnessEffect : SingleThreadedEffect
{
    private double _hue = 0;
    private double _saturation = 0;
    private double _lightness = 0;

    public double Hue { get => _hue; set { _hue = ((value % 360) + 360) % 360; OnSettingsChanged(); } }
    public double Saturation { get => _saturation; set { _saturation = Math.Clamp(value, -100, 100); OnSettingsChanged(); } }
    public double Lightness { get => _lightness; set { _lightness = Math.Clamp(value, -100, 100); OnSettingsChanged(); } }
    public event EventHandler? SettingsChanged;

    public HueSaturationLightnessEffect() : base("Hue / Saturation / Lightness", "Adjustments", "Hue and Saturation") { }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        double hueShift = _hue * Math.PI / 180.0;
        double satFactor = 1.0 + _saturation / 100.0;
        double lightFactor = _lightness / 100.0;

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var srcRow = source.GetRowSpan(y);
            var dstRow = destination.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                var pixel = srcRow[px];

                double r = pixel.R / 255.0;
                double g = pixel.G / 255.0;
                double b = pixel.B / 255.0;

                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                double delta = max - min;

                double h = 0, s = 0, l = (max + min) / 2;

                if (delta > 0)
                {
                    s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);
                    if (max == r) h = (g - b) / delta + (g < b ? 6 : 0);
                    else if (max == g) h = (b - r) / delta + 2;
                    else h = (r - g) / delta + 4;
                    h *= 60;
                }

                h = (h + _hue) % 360;
                s = Math.Clamp(s * satFactor, 0, 1);
                l = Math.Clamp(l + lightFactor, 0, 1);

                double c = (1 - Math.Abs(2 * l - 1)) * s;
                double hx = c * (1 - Math.Abs((h / 60) % 2 - 1));
                double m = l - c / 2;

                double rr, gg, bb;
                if (h < 60) { rr = c; gg = hx; bb = 0; }
                else if (h < 120) { rr = hx; gg = c; bb = 0; }
                else if (h < 180) { rr = 0; gg = c; bb = hx; }
                else if (h < 240) { rr = 0; gg = hx; bb = c; }
                else if (h < 300) { rr = hx; gg = 0; bb = c; }
                else { rr = c; gg = 0; bb = hx; }

                dstRow[px] = new ColorBgra(
                    (byte)Math.Round((rr + m) * 255),
                    (byte)Math.Round((gg + m) * 255),
                    (byte)Math.Round((bb + m) * 255),
                    pixel.A
                );
            }
        }
    }

    public override EffectConfigDialog CreateConfigDialog() => new HslDialog(this);
}

public sealed class HslDialog : EffectConfigDialog
{
    public double Hue { get => ((HueSaturationLightnessEffect)Effect).Hue; set { ((HueSaturationLightnessEffect)Effect).Hue = value; OnSettingsChanged(); } }
    public double Saturation { get => ((HueSaturationLightnessEffect)Effect).Saturation; set { ((HueSaturationLightnessEffect)Effect).Saturation = value; OnSettingsChanged(); } }
    public double Lightness { get => ((HueSaturationLightnessEffect)Effect).Lightness; set { ((HueSaturationLightnessEffect)Effect).Lightness = value; OnSettingsChanged(); } }

    public HslDialog(Effect effect) : base(effect) { }
    public override void ResetToDefaults() { Hue = 0; Saturation = 0; Lightness = 0; }
}

public sealed class LevelsEffect : SingleThreadedEffect
{
    private int _inputBlack = 0;
    private int _inputWhite = 255;
    private double _inputGamma = 1.0;
    private int _outputBlack = 0;
    private int _outputWhite = 255;

    public int InputBlack { get => _inputBlack; set { _inputBlack = Math.Clamp(value, 0, 254); OnSettingsChanged(); } }
    public int InputWhite { get => _inputWhite; set { _inputWhite = Math.Clamp(value, 1, 255); OnSettingsChanged(); } }
    public double InputGamma { get => _inputGamma; set { _inputGamma = Math.Max(0.01, value); OnSettingsChanged(); } }
    public int OutputBlack { get => _outputBlack; set { _outputBlack = Math.Clamp(value, 0, 254); OnSettingsChanged(); } }
    public int OutputWhite { get => _outputWhite; set { _outputWhite = Math.Clamp(value, 1, 255); OnSettingsChanged(); } }
    public event EventHandler? SettingsChanged;

    public LevelsEffect() : base("Levels", "Adjustments", "Levels") { }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        double inputRange = _inputWhite - _inputBlack;
        double outputRange = _outputWhite - _outputBlack;
        double invGamma = 1.0 / _inputGamma;

        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double v = (i - _inputBlack) / inputRange;
            v = Math.Clamp(v, 0, 1);
            v = Math.Pow(v, invGamma);
            v = v * outputRange + _outputBlack;
            lut[i] = (byte)Math.Clamp(Math.Round(v), 0, 255);
        }

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var srcRow = source.GetRowSpan(y);
            var dstRow = destination.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                var p = srcRow[px];
                dstRow[px] = new ColorBgra(lut[p.R], lut[p.G], lut[p.B], p.A);
            }
        }
    }

    public override EffectConfigDialog CreateConfigDialog() => new LevelsDialog(this);
}

public sealed class LevelsDialog : EffectConfigDialog
{
    public int InputBlack { get => ((LevelsEffect)Effect).InputBlack; set { ((LevelsEffect)Effect).InputBlack = value; OnSettingsChanged(); } }
    public int InputWhite { get => ((LevelsEffect)Effect).InputWhite; set { ((LevelsEffect)Effect).InputWhite = value; OnSettingsChanged(); } }
    public double InputGamma { get => ((LevelsEffect)Effect).InputGamma; set { ((LevelsEffect)Effect).InputGamma = value; OnSettingsChanged(); } }
    public int OutputBlack { get => ((LevelsEffect)Effect).OutputBlack; set { ((LevelsEffect)Effect).OutputBlack = value; OnSettingsChanged(); } }
    public int OutputWhite { get => ((LevelsEffect)Effect).OutputWhite; set { ((LevelsEffect)Effect).OutputWhite = value; OnSettingsChanged(); } }

    public LevelsDialog(Effect effect) : base(effect) { }
    public override void ResetToDefaults() { InputBlack = 0; InputWhite = 255; InputGamma = 1.0; OutputBlack = 0; OutputWhite = 255; }
}

public sealed class InvertColorsEffect : SingleThreadedEffect
{
    public InvertColorsEffect() : base("Invert Colors", "Adjustments", "Invert Colors") { IsConfigurable = false; }

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var srcRow = source.GetRowSpan(y);
            var dstRow = destination.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                var p = srcRow[px];
                dstRow[px] = new ColorBgra((byte)(255 - p.R), (byte)(255 - p.G), (byte)(255 - p.B), p.A);
            }
        }
    }
}

public sealed class GrayscaleEffect : SingleThreadedEffect
{
    public GrayscaleEffect() : base("Grayscale", "Adjustments", "Grayscale") { IsConfigurable = false; }

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var srcRow = source.GetRowSpan(y);
            var dstRow = destination.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                var p = srcRow[px];
                byte gray = (byte)(0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
                dstRow[px] = new ColorBgra(gray, gray, gray, p.A);
            }
        }
    }
}

public sealed class SepiaEffect : SingleThreadedEffect
{
    public SepiaEffect() : base("Sepia", "Adjustments", "Sepia") { IsConfigurable = false; }

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            var srcRow = source.GetRowSpan(y);
            var dstRow = destination.GetRowSpanMutable(y);

            for (int px = roi.X; px < roi.Right; px++)
            {
                var p = srcRow[px];
                int tr = (int)(0.393 * p.R + 0.769 * p.G + 0.189 * p.B);
                int tg = (int)(0.349 * p.R + 0.686 * p.G + 0.168 * p.B);
                int tb = (int)(0.272 * p.R + 0.534 * p.G + 0.131 * p.B);
                dstRow[px] = new ColorBgra(
                    (byte)Math.Clamp(tr, 0, 255),
                    (byte)Math.Clamp(tg, 0, 255),
                    (byte)Math.Clamp(tb, 0, 255),
                    p.A
                );
            }
        }
    }
}

public sealed class EmbossEffect : SingleThreadedEffect
{
    private double _angle = 135;
    private double _amount = 1.0;

    public double Angle { get => _angle; set { _angle = ((value % 360) + 360) % 360; OnSettingsChanged(); } }
    public double Amount { get => _amount; set { _amount = Math.Max(0, value); OnSettingsChanged(); } }
    public event EventHandler? SettingsChanged;

    public EmbossEffect() : base("Emboss", "Stylize", "Emboss") { }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        double rad = _angle * Math.PI / 180.0;
        int dx = (int)Math.Round(Math.Cos(rad));
        int dy = (int)Math.Round(Math.Sin(rad));

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            for (int px = roi.X; px < roi.Right; px++)
            {
                int px1 = Math.Clamp(px - dx, 0, source.Width - 1);
                int py1 = Math.Clamp(y - dy, 0, source.Height - 1);
                int px2 = Math.Clamp(px + dx, 0, source.Width - 1);
                int py2 = Math.Clamp(y + dy, 0, source.Height - 1);

                var p1 = source[px1, py1];
                var p2 = source[px2, py2];

                int dr = (p2.R - p1.R);
                int dg = (p2.G - p1.G);
                int db = (p2.B - p1.B);

                int gray = (int)(128 + _amount * (0.299 * dr + 0.587 * dg + 0.114 * db));
                gray = Math.Clamp(gray, 0, 255);

                destination[px, y] = new ColorBgra((byte)gray, (byte)gray, (byte)gray, source[px, y].A);
            }
        }
    }

    public override EffectConfigDialog CreateConfigDialog() => new EmbossDialog(this);
}

public sealed class EmbossDialog : EffectConfigDialog
{
    public double Angle { get => ((EmbossEffect)Effect).Angle; set { ((EmbossEffect)Effect).Angle = value; OnSettingsChanged(); } }
    public double Amount { get => ((EmbossEffect)Effect).Amount; set { ((EmbossEffect)Effect).Amount = value; OnSettingsChanged(); } }

    public EmbossDialog(Effect effect) : base(effect) { }
    public override void ResetToDefaults() { Angle = 135; Amount = 1.0; }
}

public sealed class OilPaintingEffect : SingleThreadedEffect
{
    private int _brushSize = 5;
    private int _coarseness = 10;

    public int BrushSize { get => _brushSize; set { _brushSize = Math.Clamp(value, 1, 20); OnSettingsChanged(); } }
    public int Coarseness { get => _coarseness; set { _coarseness = Math.Clamp(value, 1, 50); OnSettingsChanged(); } }
    public event EventHandler? SettingsChanged;

    public OilPaintingEffect() : base("Oil Painting", "Artistic", "Oil Painting") { }

    protected void OnSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    protected override void RenderRect(Surface source, Surface destination, RectInt32 roi)
    {
        int radius = _brushSize;

        for (int y = roi.Y; y < roi.Bottom; y++)
        {
            for (int px = roi.X; px < roi.Right; px++)
            {
                var histogram = new int[256];
                int maxCount = 0, maxIndex = 0;
                long rSum = 0, gSum = 0, bSum = 0;

                for (int ky = -radius; ky <= radius; ky++)
                {
                    int py = Math.Clamp(y + ky, 0, source.Height - 1);
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        int kpx = Math.Clamp(px + kx, 0, source.Width - 1);
                        var p = source[kpx, py];
                        int intensity = (p.R + p.G + p.B) / 3;
                        int bucket = intensity * 255 / 255;
                        histogram[bucket]++;
                        if (histogram[bucket] > maxCount)
                        {
                            maxCount = histogram[bucket];
                            maxIndex = bucket;
                        }
                    }
                }

                int count = 0;
                for (int ky = -radius; ky <= radius; ky++)
                {
                    int py = Math.Clamp(y + ky, 0, source.Height - 1);
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        int kpx = Math.Clamp(px + kx, 0, source.Width - 1);
                        var p = source[kpx, py];
                        int intensity = (p.R + p.G + p.B) / 3;
                        int bucket = intensity * 255 / 255;
                        if (bucket == maxIndex)
                        {
                            rSum += p.R;
                            gSum += p.G;
                            bSum += p.B;
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    destination[px, y] = new ColorBgra(
                        (byte)(rSum / count),
                        (byte)(gSum / count),
                        (byte)(bSum / count),
                        source[px, y].A
                    );
                }
            }
        }
    }

    public override EffectConfigDialog CreateConfigDialog() => new OilPaintingDialog(this);
}

public sealed class OilPaintingDialog : EffectConfigDialog
{
    public int BrushSize { get => ((OilPaintingEffect)Effect).BrushSize; set { ((OilPaintingEffect)Effect).BrushSize = value; OnSettingsChanged(); } }
    public int Coarseness { get => ((OilPaintingEffect)Effect).Coarseness; set { ((OilPaintingEffect)Effect).Coarseness = value; OnSettingsChanged(); } }

    public OilPaintingDialog(Effect effect) : base(effect) { }
    public override void ResetToDefaults() { BrushSize = 5; Coarseness = 10; }
}

public static class EffectRegistry
{
    private static readonly List<Effect> _effects = new()
    {
        new GaussianBlurEffect(),
        new SharpenEffect(),
        new BrightnessContrastEffect(),
        new HueSaturationLightnessEffect(),
        new LevelsEffect(),
        new InvertColorsEffect(),
        new GrayscaleEffect(),
        new SepiaEffect(),
        new EmbossEffect(),
        new OilPaintingEffect()
    };

    public static IReadOnlyList<Effect> Effects => _effects;
    public static IEnumerable<Effect> GetByCategory(string category) => _effects.Where(e => e.Category == category);
    public static IEnumerable<string> Categories => _effects.Select(e => e.Category).Distinct().OrderBy(c => c);
}