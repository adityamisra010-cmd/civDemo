using Microsoft.Xna.Framework;

namespace Gfx.Gear.Geometry;

/// <summary>Double-precision 2D vector for generator maths (profiles, outlines, UV-space work).</summary>
public readonly record struct Vector2d(double X, double Y)
{
    public static readonly Vector2d Zero = new(0, 0);
    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator -(Vector2d a) => new(-a.X, -a.Y);
    public static Vector2d operator *(Vector2d a, double s) => new(a.X * s, a.Y * s);
    public static Vector2d operator *(double s, Vector2d a) => new(a.X * s, a.Y * s);
    public static Vector2d operator /(Vector2d a, double s) => new(a.X / s, a.Y / s);
    public double Dot(Vector2d b) => X * b.X + Y * b.Y;
    public double Cross(Vector2d b) => X * b.Y - Y * b.X;
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vector2d Normalized() { double l = Length; return l > 1e-300 ? this / l : new Vector2d(1, 0); }
    public Vector2d Perp => new(-Y, X);
    public static Vector2d Lerp(Vector2d a, Vector2d b, double t) => a + (b - a) * t;
    public Vector2 ToVector2() => new((float)X, (float)Y);
}

/// <summary>Double-precision 3D vector for generator maths.</summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public static readonly Vector3d Zero = new(0, 0, 0);
    public static readonly Vector3d UnitX = new(1, 0, 0), UnitY = new(0, 1, 0), UnitZ = new(0, 0, 1);
    public Vector3d(Vector3 v) : this(v.X, v.Y, v.Z) { }
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3d operator *(Vector3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator *(double s, Vector3d a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator /(Vector3d a, double s) => new(a.X / s, a.Y / s, a.Z / s);
    public double Dot(Vector3d b) => X * b.X + Y * b.Y + Z * b.Z;
    public static Vector3d Cross(Vector3d a, Vector3d b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public Vector3d Normalized() { double l = Length; return l > 1e-300 ? this / l : new Vector3d(0, 1, 0); }
    public static Vector3d Lerp(Vector3d a, Vector3d b, double t) => a + (b - a) * t;
    public Vector3 ToVector3() => new((float)X, (float)Y, (float)Z);
    /// <summary>Rotates about a unit axis by angle a (radians), Rodrigues.</summary>
    public Vector3d Rotate(Vector3d axis, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return this * c + Cross(axis, this) * s + axis * (axis.Dot(this) * (1 - c));
    }
}
