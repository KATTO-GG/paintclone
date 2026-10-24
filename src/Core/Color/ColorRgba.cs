namespace PaintNetClone.Core.Color;

using System;
using PaintNetClone.Core.Geometry;

public readonly struct ColorRgba : IEquatable<ColorRgba>
{
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    public readonly byte A;

    public ColorRgba(byte r, byte g, byte b, byte a = 255)
    {
        R = r; G = g; B = b; A = a;
    }

    public ColorRgba(int r, int g, int b, int a = 255)
        : this((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255), (byte)Math.Clamp(a, 0, 255)) { }

    public ColorRgba(double r, double g, double b, double a = 1.0)
        : this((byte)Math.Round(Math.Clamp(r, 0, 1) * 255), (byte)Math.Round(Math.Clamp(g, 0, 1) * 255), (byte)Math.Round(Math.Clamp(b, 0, 1) * 255), (byte)Math.Round(Math.Clamp(a, 0, 1) * 255)) { }

    public static ColorRgba FromArgb(int argb) => new((argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF, (argb >> 24) & 0xFF);
    public static ColorRgba FromRgb(int rgb) => new((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 255);
    public static ColorRgba FromHsv(double h, double s, double v, double a = 1.0)
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
            0 => new ColorRgba(v, t, p, a),
            1 => new ColorRgba(q, v, p, a),
            2 => new ColorRgba(p, v, t, a),
            3 => new ColorRgba(p, q, v, a),
            4 => new ColorRgba(t, p, v, a),
            _ => new ColorRgba(v, p, q, a),
        };
    }

    public int ToArgb() => (A << 24) | (R << 16) | (G << 8) | B;
    public int ToRgb() => (R << 16) | (G << 8) | B;

    public (double H, double S, double V) ToHsv()
    {
        double r = R / 255.0;
        double g = G / 255.0;
        double b = B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * ((g - b) / delta % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;
        }

        double s = max == 0 ? 0 : delta / max;
        double v = max;

        return (h, s, v);
    }

    public ColorRgba WithAlpha(byte a) => new(R, G, B, a);
    public ColorRgba WithAlpha(double a) => WithAlpha((byte)Math.Round(Math.Clamp(a, 0, 1) * 255));

    public ColorBgra ToBgra() => new(B, G, R, A);

    public double Luminance => 0.2126 * R + 0.7152 * G + 0.0722 * B;
    public double PerceivedBrightness => Math.Sqrt(0.299 * R * R + 0.587 * G * G + 0.114 * B * B);

    public static ColorRgba operator +(ColorRgba a, ColorRgba b) =>
        new(Math.Clamp(a.R + b.R, 0, 255), Math.Clamp(a.G + b.G, 0, 255), Math.Clamp(a.B + b.B, 0, 255), Math.Clamp(a.A + b.A, 0, 255));

    public static ColorRgba operator -(ColorRgba a, ColorRgba b) =>
        new(Math.Clamp(a.R - b.R, 0, 255), Math.Clamp(a.G - b.G, 0, 255), Math.Clamp(a.B - b.B, 0, 255), Math.Clamp(a.A - b.A, 0, 255));

    public static ColorRgba operator *(ColorRgba a, double scalar) =>
        new((byte)Math.Clamp(a.R * scalar, 0, 255), (byte)Math.Clamp(a.G * scalar, 0, 255), (byte)Math.Clamp(a.B * scalar, 0, 255), (byte)Math.Clamp(a.A * scalar, 0, 255));

    public static ColorRgba Lerp(ColorRgba a, ColorRgba b, double t) =>
        a + (b - a) * Math.Clamp(t, 0, 1);

    public bool Equals(ColorRgba other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is ColorRgba other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{(A == 255 ? "" : A.ToString("X2"))}";

    public static bool operator ==(ColorRgba left, ColorRgba right) => left.Equals(right);
    public static bool operator !=(ColorRgba left, ColorRgba right) => !left.Equals(right);

    public static readonly ColorRgba Transparent = new(0, 0, 0, 0);
    public static readonly ColorRgba Black = new(0, 0, 0);
    public static readonly ColorRgba White = new(255, 255, 255);
    public static readonly ColorRgba Red = new(255, 0, 0);
    public static readonly ColorRgba Green = new(0, 255, 0);
    public static readonly ColorRgba Blue = new(0, 0, 255);
    public static readonly ColorRgba Yellow = new(255, 255, 0);
    public static readonly ColorRgba Cyan = new(0, 255, 255);
    public static readonly ColorRgba Magenta = new(255, 0, 255);
}

public readonly struct ColorFloat : IEquatable<ColorFloat>
{
    public readonly float R;
    public readonly float G;
    public readonly float B;
    public readonly float A;

    public ColorFloat(float r, float g, float b, float a = 1.0f)
    {
        R = r; G = g; B = b; A = a;
    }

    public ColorFloat(ColorRgba c)
        : this(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f) { }

    public ColorRgba ToRgba() => new(R, G, B, A);

    public static ColorFloat operator +(ColorFloat a, ColorFloat b) => new(a.R + b.R, a.G + b.G, a.B + b.B, a.A + b.A);
    public static ColorFloat operator -(ColorFloat a, ColorFloat b) => new(a.R - b.R, a.G - b.G, a.B - b.B, a.A - b.A);
    public static ColorFloat operator *(ColorFloat a, float scalar) => new(a.R * scalar, a.G * scalar, a.B * scalar, a.A * scalar);
    public static ColorFloat Lerp(ColorFloat a, ColorFloat b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);

    public bool Equals(ColorFloat other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is ColorFloat other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public override string ToString() => $"({R:F3}, {G:F3}, {B:F3}, {A:F3})";

    public static bool operator ==(ColorFloat left, ColorFloat right) => left.Equals(right);
    public static bool operator !=(ColorFloat left, ColorFloat right) => !left.Equals(right);
}