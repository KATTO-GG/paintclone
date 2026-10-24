namespace PaintNetClone.Core.Color;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Blue-Green-Red-Alpha color struct (32-bit, BGRA byte order).
/// Compatible with paint.net's ColorBgra.
/// </summary>
public readonly struct ColorBgra : IEquatable<ColorBgra>
{
    public readonly byte B;
    public readonly byte G;
    public readonly byte R;
    public readonly byte A;

    public ColorBgra(byte b, byte g, byte r, byte a = 255)
    {
        B = b; G = g; R = r; A = a;
    }

    public ColorBgra(int r, int g, int b, int a = 255)
        : this((byte)Math.Clamp(b, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(a, 0, 255)) { }

    public ColorBgra(double r, double g, double b, double a = 1.0)
        : this((byte)Math.Round(Math.Clamp(b, 0, 1) * 255), (byte)Math.Round(Math.Clamp(g, 0, 1) * 255), (byte)Math.Round(Math.Clamp(r, 0, 1) * 255), (byte)Math.Round(Math.Clamp(a, 0, 1) * 255)) { }

    public static ColorBgra FromArgb(int argb) => new((argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF, (argb >> 24) & 0xFF);
    public static ColorBgra FromRgb(int rgb) => new(rgb & 0xFF, (rgb >> 8) & 0xFF, (rgb >> 16) & 0xFF, 255);

    public static ColorBgra FromHsv(double h, double s, double v, double a = 1.0)
    {
        h = (h % 360 + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        a = Math.Clamp(a, 0, 1);

        int hi = (int)(h / 60) % 6;
        double f = h / 60 - hi;
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);

        return hi switch
        {
            0 => new ColorBgra((byte)Math.Round(p * 255), (byte)Math.Round(t * 255), (byte)Math.Round(v * 255), (byte)Math.Round(a * 255)),
            1 => new ColorBgra((byte)Math.Round(p * 255), (byte)Math.Round(v * 255), (byte)Math.Round(q * 255), (byte)Math.Round(a * 255)),
            2 => new ColorBgra((byte)Math.Round(t * 255), (byte)Math.Round(v * 255), (byte)Math.Round(p * 255), (byte)Math.Round(a * 255)),
            3 => new ColorBgra((byte)Math.Round(q * 255), (byte)Math.Round(v * 255), (byte)Math.Round(p * 255), (byte)Math.Round(a * 255)),
            4 => new ColorBgra((byte)Math.Round(v * 255), (byte)Math.Round(p * 255), (byte)Math.Round(q * 255), (byte)Math.Round(a * 255)),
            _ => new ColorBgra((byte)Math.Round(v * 255), (byte)Math.Round(q * 255), (byte)Math.Round(p * 255), (byte)Math.Round(a * 255)),
        };
    }

    public int ToArgb() => (A << 24) | (R << 16) | (G << 8) | B;
    public int ToRgb() => (R << 16) | (G << 8) | B;

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    public ColorBgra WithAlpha(byte alpha) => new(B, G, R, alpha);

    public ColorRgba ToRgba() => new(R, G, B, A);

    public static ColorBgra FromRgba(ColorRgba c) => new(c.B, c.G, c.R, c.A);

    public (double H, double S, double V) ToHsv()
    {
        double r = R / 255.0;
        double g = G / 255.0;
        double b = B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0, s = 0, v = max;
        if (delta > 0)
        {
            if (max == r) h = (g - b) / delta;
            else if (max == g) h = 2 + (b - r) / delta;
            else h = 4 + (r - g) / delta;
            h *= 60;
            if (h < 0) h += 360;
            s = max > 0 ? delta / max : 0;
        }
        return (h, s, v);
    }

    public static ColorBgra operator +(ColorBgra a, ColorBgra b) => new(Math.Clamp(a.B + b.B, 0, 255), Math.Clamp(a.G + b.G, 0, 255), Math.Clamp(a.R + b.R, 0, 255), Math.Clamp(a.A + b.A, 0, 255));
    public static ColorBgra operator -(ColorBgra a, ColorBgra b) => new(Math.Clamp(a.B - b.B, 0, 255), Math.Clamp(a.G - b.G, 0, 255), Math.Clamp(a.R - b.R, 0, 255), Math.Clamp(a.A - b.A, 0, 255));
    public static ColorBgra operator *(ColorBgra a, float scalar) => new((byte)Math.Clamp(a.B * scalar, 0, 255), (byte)Math.Clamp(a.G * scalar, 0, 255), (byte)Math.Clamp(a.R * scalar, 0, 255), (byte)Math.Clamp(a.A * scalar, 0, 255));
    public static ColorBgra Lerp(ColorBgra a, ColorBgra b, double t) => a + (b - a) * Math.Clamp((float)t, 0, 1);

    // Predefined colors
    public static ColorBgra Transparent => new(0, 0, 0, 0);
    public static ColorBgra Black => new(0, 0, 0, 255);
    public static ColorBgra White => new(255, 255, 255, 255);
    public static ColorBgra Red => new(0, 0, 255, 255);
    public static ColorBgra Green => new(0, 255, 0, 255);
    public static ColorBgra Blue => new(255, 0, 0, 255);
    public static ColorBgra Yellow => new(0, 255, 255, 255);
    public static ColorBgra Cyan => new(255, 255, 0, 255);
    public static ColorBgra Magenta => new(255, 0, 255, 255);

    public bool Equals(ColorBgra other) => B == other.B && G == other.G && R == other.R && A == other.A;
    public override bool Equals(object? obj) => obj is ColorBgra other && Equals(other);
    public override int GetHashCode() => (B << 24) | (G << 16) | (R << 8) | A;

    public static bool operator ==(ColorBgra left, ColorBgra right) => left.Equals(right);
    public static bool operator !=(ColorBgra left, ColorBgra right) => !left.Equals(right);
}