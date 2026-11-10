// Double-precision equivalents of the three.js (r180, MIT, Copyright 2010-2025 three.js authors) math that ez-tree's
// generator uses: Vector3, Quaternion and Euler (order XYZ). Each operation follows the three.js source expression
// for expression (operand order included), so the port tracks the JavaScript original to rounding level; the only
// expected differences come from Math.Sin/Cos/Atan2/Asin implementations (V8 vs the C runtime), measured in
// reports/env.md. PRESENTATION ONLY.
namespace Gfx.Proc.Environment.EzTree;

public struct Vec3d
{
    public double X, Y, Z;
    public Vec3d(double x, double y, double z) { X = x; Y = y; Z = z; }

    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>three.js <c>multiplyScalar</c>.</summary>
    public readonly Vec3d Mul(double s) => new(X * s, Y * s, Z * s);
    public readonly double Dot(Vec3d v) => X * v.X + Y * v.Y + Z * v.Z;
    public readonly double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>three.js <c>normalize</c>: <c>divideScalar(length || 1)</c>, i.e. multiply by 1/length.</summary>
    public readonly Vec3d Normalize()
    {
        double l = Length();
        if (l == 0) l = 1;
        return Mul(1 / l);
    }

    /// <summary>three.js <c>divideScalar</c> (multiplies by 1/s).</summary>
    public readonly Vec3d Div(double s) => Mul(1 / s);

    /// <summary>three.js <c>crossVectors(a, b)</c>.</summary>
    public static Vec3d Cross(Vec3d a, Vec3d b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>three.js <c>lerpVectors(v1, v2, alpha)</c>.</summary>
    public static Vec3d Lerp(Vec3d v1, Vec3d v2, double alpha) =>
        new(v1.X + (v2.X - v1.X) * alpha, v1.Y + (v2.Y - v1.Y) * alpha, v1.Z + (v2.Z - v1.Z) * alpha);

    /// <summary>three.js <c>applyQuaternion</c> (unit quaternion).</summary>
    public readonly Vec3d ApplyQuaternion(Quatd q)
    {
        double vx = X, vy = Y, vz = Z;
        double qx = q.X, qy = q.Y, qz = q.Z, qw = q.W;
        double tx = 2 * (qy * vz - qz * vy);
        double ty = 2 * (qz * vx - qx * vz);
        double tz = 2 * (qx * vy - qy * vx);
        return new Vec3d(vx + qw * tx + qy * tz - qz * ty, vy + qw * ty + qz * tx - qx * tz, vz + qw * tz + qx * ty - qy * tx);
    }

    /// <summary>three.js <c>applyEuler</c>: <c>applyQuaternion(setFromEuler(e))</c>.</summary>
    public readonly Vec3d ApplyEuler(EulerXyz e) => ApplyQuaternion(Quatd.FromEuler(e));

    public override readonly string ToString() => FormattableString.Invariant($"({X}, {Y}, {Z})");
}

/// <summary>Euler angles in three.js's default order 'XYZ'.</summary>
public struct EulerXyz
{
    public double X, Y, Z;
    public EulerXyz(double x, double y, double z) { X = x; Y = y; Z = z; }

    /// <summary>three.js <c>setFromQuaternion(q)</c>: rotation matrix from the quaternion, then <c>setFromRotationMatrix</c> (XYZ).</summary>
    public static EulerXyz FromQuaternion(Quatd q)
    {
        // Matrix4.compose(zero, q, one): only the rotation terms are needed
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        double x2 = x + x, y2 = y + y, z2 = z + z;
        double xx = x * x2, xy = x * y2, xz = x * z2;
        double yy = y * y2, yz = y * z2, zz = z * z2;
        double wx = w * x2, wy = w * y2, wz = w * z2;
        double m11 = (1 - (yy + zz)) * 1;
        double m12 = (xy - wz) * 1;
        double m13 = (xz + wy) * 1;
        double m22 = (1 - (xx + zz)) * 1;
        double m23 = (yz - wx) * 1;
        double m32 = (yz + wx) * 1;
        double m33 = (1 - (xx + yy)) * 1;
        var e = new EulerXyz { Y = Math.Asin(Math.Max(-1, Math.Min(1, m13))) };
        if (Math.Abs(m13) < 0.9999999)
        {
            e.X = Math.Atan2(-m23, m33);
            e.Z = Math.Atan2(-m12, m11);
        }
        else
        {
            e.X = Math.Atan2(m32, m22);
            e.Z = 0;
        }
        return e;
    }
}

/// <summary>three.js Quaternion (x, y, z, w).</summary>
public struct Quatd
{
    public double X, Y, Z, W;
    public Quatd(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }
    public static Quatd Identity => new(0, 0, 0, 1);
    /// <summary>JavaScript <c>Number.EPSILON</c> (2^-52).</summary>
    public const double NumberEpsilon = 2.220446049250313e-16;

    /// <summary>three.js <c>setFromEuler</c>, order XYZ.</summary>
    public static Quatd FromEuler(EulerXyz e)
    {
        double c1 = Math.Cos(e.X / 2), c2 = Math.Cos(e.Y / 2), c3 = Math.Cos(e.Z / 2);
        double s1 = Math.Sin(e.X / 2), s2 = Math.Sin(e.Y / 2), s3 = Math.Sin(e.Z / 2);
        return new Quatd(
            s1 * c2 * c3 + c1 * s2 * s3,
            c1 * s2 * c3 - s1 * c2 * s3,
            c1 * c2 * s3 + s1 * s2 * c3,
            c1 * c2 * c3 - s1 * s2 * s3);
    }

    /// <summary>three.js <c>setFromAxisAngle</c> (unit axis).</summary>
    public static Quatd FromAxisAngle(Vec3d axis, double angle)
    {
        double halfAngle = angle / 2, s = Math.Sin(halfAngle);
        return new Quatd(axis.X * s, axis.Y * s, axis.Z * s, Math.Cos(halfAngle));
    }

    /// <summary>three.js <c>multiplyQuaternions(a, b)</c>: a·b.</summary>
    public static Quatd Multiply(Quatd a, Quatd b)
    {
        double qax = a.X, qay = a.Y, qaz = a.Z, qaw = a.W;
        double qbx = b.X, qby = b.Y, qbz = b.Z, qbw = b.W;
        return new Quatd(
            qax * qbw + qaw * qbx + qay * qbz - qaz * qby,
            qay * qbw + qaw * qby + qaz * qbx - qax * qbz,
            qaz * qbw + qaw * qbz + qax * qby - qay * qbx,
            qaw * qbw - qax * qbx - qay * qby - qaz * qbz);
    }

    public readonly double Length() => Math.Sqrt(X * X + Y * Y + Z * Z + W * W);

    /// <summary>three.js <c>normalize</c>.</summary>
    public readonly Quatd Normalize()
    {
        double l = Length();
        if (l == 0) return new Quatd(0, 0, 0, 1);
        l = 1 / l;
        return new Quatd(X * l, Y * l, Z * l, W * l);
    }

    /// <summary>three.js <c>this.slerp(qb, t)</c>: returns the new value of <c>this</c>.</summary>
    public readonly Quatd Slerp(Quatd qb, double t)
    {
        if (t == 0) return this;
        if (t == 1) return qb;
        double x = X, y = Y, z = Z, w = W;
        double cosHalfTheta = w * qb.W + x * qb.X + y * qb.Y + z * qb.Z;
        Quatd r;
        if (cosHalfTheta < 0)
        {
            r = new Quatd(-qb.X, -qb.Y, -qb.Z, -qb.W);
            cosHalfTheta = -cosHalfTheta;
        }
        else r = qb;
        if (cosHalfTheta >= 1.0) return new Quatd(x, y, z, w);
        double sqrSinHalfTheta = 1.0 - cosHalfTheta * cosHalfTheta;
        if (sqrSinHalfTheta <= NumberEpsilon)
        {
            double s = 1 - t;
            return new Quatd(s * x + t * r.X, s * y + t * r.Y, s * z + t * r.Z, s * w + t * r.W).Normalize();
        }
        double sinHalfTheta = Math.Sqrt(sqrSinHalfTheta);
        double halfTheta = Math.Atan2(sinHalfTheta, cosHalfTheta);
        double ratioA = Math.Sin((1 - t) * halfTheta) / sinHalfTheta, ratioB = Math.Sin(t * halfTheta) / sinHalfTheta;
        return new Quatd(x * ratioA + r.X * ratioB, y * ratioA + r.Y * ratioB, z * ratioA + r.Z * ratioB, w * ratioA + r.W * ratioB);
    }
}
