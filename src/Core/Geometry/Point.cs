namespace PaintNetClone.Core.Geometry;

using System;

public readonly struct PointDouble : IEquatable<PointDouble>
{
    public readonly double X;
    public readonly double Y;

    public PointDouble(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static PointDouble Zero => new(0, 0);

    public static PointDouble operator +(PointDouble a, PointDouble b) => new(a.X + b.X, a.Y + b.Y);
    public static PointDouble operator -(PointDouble a, PointDouble b) => new(a.X - b.X, a.Y - b.Y);
    public static PointDouble operator *(PointDouble a, double scalar) => new(a.X * scalar, a.Y * scalar);
    public static PointDouble operator /(PointDouble a, double scalar) => new(a.X / scalar, a.Y / scalar);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public PointDouble Normalized => Length > 0 ? this / Length : Zero;

    public bool Equals(PointDouble other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is PointDouble other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";

    public static bool operator ==(PointDouble left, PointDouble right) => left.Equals(right);
    public static bool operator !=(PointDouble left, PointDouble right) => !left.Equals(right);
}

public readonly struct PointInt32 : IEquatable<PointInt32>
{
    public readonly int X;
    public readonly int Y;

    public PointInt32(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static PointInt32 Zero => new(0, 0);

    public static PointInt32 operator +(PointInt32 a, PointInt32 b) => new(a.X + b.X, a.Y + b.Y);
    public static PointInt32 operator -(PointInt32 a, PointInt32 b) => new(a.X - b.X, a.Y - b.Y);
    public static PointInt32 operator *(PointInt32 a, int scalar) => new(a.X * scalar, a.Y * scalar);

    public bool Equals(PointInt32 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is PointInt32 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";

    public static bool operator ==(PointInt32 left, PointInt32 right) => left.Equals(right);
    public static bool operator !=(PointInt32 left, PointInt32 right) => !left.Equals(right);
}

public readonly struct SizeDouble : IEquatable<SizeDouble>
{
    public readonly double Width;
    public readonly double Height;

    public SizeDouble(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public static SizeDouble Zero => new(0, 0);
    public static SizeDouble Empty => new(double.NaN, double.NaN);

    public bool IsEmpty => double.IsNaN(Width) || double.IsNaN(Height);
    public double Area => Width * Height;

    public bool Equals(SizeDouble other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is SizeDouble other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public override string ToString() => $"{Width}x{Height}";

    public static bool operator ==(SizeDouble left, SizeDouble right) => left.Equals(right);
    public static bool operator !=(SizeDouble left, SizeDouble right) => !left.Equals(right);
}

public readonly struct SizeInt32 : IEquatable<SizeInt32>
{
    public readonly int Width;
    public readonly int Height;

    public SizeInt32(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public static SizeInt32 Zero => new(0, 0);
    public static SizeInt32 Empty => new(-1, -1);

    public bool IsEmpty => Width < 0 || Height < 0;
    public int Area => Width * Height;

    public bool Equals(SizeInt32 other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is SizeInt32 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public override string ToString() => $"{Width}x{Height}";

    public static bool operator ==(SizeInt32 left, SizeInt32 right) => left.Equals(right);
    public static bool operator !=(SizeInt32 left, SizeInt32 right) => !left.Equals(right);
}

public readonly struct RectDouble : IEquatable<RectDouble>
{
    public readonly double X;
    public readonly double Y;
    public readonly double Width;
    public readonly double Height;

    public RectDouble(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public RectDouble(PointDouble location, SizeDouble size) : this(location.X, location.Y, size.Width, size.Height) { }

    public static RectDouble Empty => new(0, 0, 0, 0);

    public PointDouble Location => new(X, Y);
    public SizeDouble Size => new(Width, Height);
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public PointDouble TopLeft => new(X, Y);
    public PointDouble TopRight => new(X + Width, Y);
    public PointDouble BottomLeft => new(X, Y + Height);
    public PointDouble BottomRight => new(X + Width, Y + Height);
    public PointDouble Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(PointDouble point) =>
        point.X >= X && point.X < X + Width && point.Y >= Y && point.Y < Y + Height;

    public bool IntersectsWith(RectDouble other) =>
        X < other.X + other.Width && X + Width > other.X &&
        Y < other.Y + other.Height && Y + Height > other.Y;

    public RectDouble Intersect(RectDouble other)
    {
        double left = Math.Max(X, other.X);
        double top = Math.Max(Y, other.Y);
        double right = Math.Min(X + Width, other.X + other.Width);
        double bottom = Math.Min(Y + Height, other.Y + other.Height);

        if (right < left || bottom < top)
            return Empty;

        return new RectDouble(left, top, right - left, bottom - top);
    }

    public RectDouble Union(RectDouble other)
    {
        if (Width == 0 && Height == 0) return other;
        if (other.Width == 0 && other.Height == 0) return this;

        double left = Math.Min(X, other.X);
        double top = Math.Min(Y, other.Y);
        double right = Math.Max(X + Width, other.X + other.Width);
        double bottom = Math.Max(Y + Height, other.Y + other.Height);

        return new RectDouble(left, top, right - left, bottom - top);
    }

    public bool Equals(RectDouble other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is RectDouble other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"[{X}, {Y}, {Width}, {Height}]";

    public static bool operator ==(RectDouble left, RectDouble right) => left.Equals(right);
    public static bool operator !=(RectDouble left, RectDouble right) => !left.Equals(right);
}

public readonly struct RectInt32 : IEquatable<RectInt32>
{
    public readonly int X;
    public readonly int Y;
    public readonly int Width;
    public readonly int Height;

    public RectInt32(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public RectInt32(PointInt32 location, SizeInt32 size) : this(location.X, location.Y, size.Width, size.Height) { }

    public static RectInt32 Empty => new(0, 0, 0, 0);

    public PointInt32 Location => new(X, Y);
    public SizeInt32 Size => new(Width, Height);
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(PointInt32 point) =>
        point.X >= X && point.X < X + Width && point.Y >= Y && point.Y < Y + Height;

    public bool IntersectsWith(RectInt32 other) =>
        X < other.X + other.Width && X + Width > other.X &&
        Y < other.Y + other.Height && Y + Height > other.Y;

    public RectInt32 Intersect(RectInt32 other)
    {
        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        int right = Math.Min(X + Width, other.X + other.Width);
        int bottom = Math.Min(Y + Height, other.Y + other.Height);

        if (left >= right || top >= bottom)
            return RectInt32.Empty;

        return new RectInt32(left, top, right - left, bottom - top);
    }

    public RectInt32 Union(RectInt32 other)
    {
        if (Width == 0 && Height == 0) return other;
        if (other.Width == 0 && other.Height == 0) return this;

        int left = Math.Min(X, other.X);
        int top = Math.Min(Y, other.Y);
        int right = Math.Max(X + Width, other.X + other.Width);
        int bottom = Math.Max(Y + Height, other.Y + other.Height);

        return new RectInt32(left, top, right - left, bottom - top);
    }

    public bool Equals(RectInt32 other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is RectInt32 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"[{X}, {Y}, {Width}, {Height}]";

    public static bool operator ==(RectInt32 left, RectInt32 right) => left.Equals(right);
    public static bool operator !=(RectInt32 left, RectInt32 right) => !left.Equals(right);
}

public readonly struct Matrix3x2 : IEquatable<Matrix3x2>
{
    public readonly double M11, M12, M21, M22, M31, M32;

    public Matrix3x2(double m11, double m12, double m21, double m22, double m31, double m32)
    {
        M11 = m11; M12 = m12;
        M21 = m21; M22 = m22;
        M31 = m31; M32 = m32;
    }

    public static Matrix3x2 Identity => new(1, 0, 0, 1, 0, 0);

    public static Matrix3x2 Translation(double x, double y) => new(1, 0, 0, 1, x, y);
    public static Matrix3x2 Translation(PointDouble p) => Translation(p.X, p.Y);
    public static Matrix3x2 Scale(double x, double y) => new(x, 0, 0, y, 0, 0);
    public static Matrix3x2 Scale(double uniform) => Scale(uniform, uniform);
    public static Matrix3x2 Rotation(double radians)
    {
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        return new(cos, sin, -sin, cos, 0, 0);
    }

    public PointDouble Transform(PointDouble point) =>
        new(M11 * point.X + M21 * point.Y + M31, M12 * point.X + M22 * point.Y + M32);

    public Matrix3x2 Multiply(Matrix3x2 other) =>
        new(
            M11 * other.M11 + M12 * other.M21,
            M11 * other.M12 + M12 * other.M22,
            M21 * other.M11 + M22 * other.M21,
            M21 * other.M12 + M22 * other.M22,
            M31 * other.M11 + M32 * other.M21 + other.M31,
            M31 * other.M12 + M32 * other.M22 + other.M32
        );

    public bool TryInvert(out Matrix3x2 result)
    {
        double det = M11 * M22 - M12 * M21;
        if (Math.Abs(det) < 1e-10)
        {
            result = Identity;
            return false;
        }

        double invDet = 1.0 / det;
        result = new(
            M22 * invDet, -M12 * invDet,
            -M21 * invDet, M11 * invDet,
            (M21 * M32 - M22 * M31) * invDet,
            (M12 * M31 - M11 * M32) * invDet
        );
        return true;
    }

    public bool Equals(Matrix3x2 other) =>
        M11 == other.M11 && M12 == other.M12 &&
        M21 == other.M21 && M22 == other.M22 &&
        M31 == other.M31 && M32 == other.M32;

    public override bool Equals(object? obj) => obj is Matrix3x2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(M11, M12, M21, M22, M31, M32);
    public override string ToString() => $"[{M11}, {M12}, {M21}, {M22}, {M31}, {M32}]";

    public static bool operator ==(Matrix3x2 left, Matrix3x2 right) => left.Equals(right);
    public static bool operator !=(Matrix3x2 left, Matrix3x2 right) => !left.Equals(right);
    public static Matrix3x2 operator *(Matrix3x2 left, Matrix3x2 right) => left.Multiply(right);
}