using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tests.MultiDomain;

public class FixedTrigTests
{
    /// <summary>GOLDEN (Linux x64, .NET 10, measured 2026-11-05): SHA-256 of Sin over all 65,536 angles. Integer-only
    /// construction => expected identical on every platform (cross-platform equality untested, O-17).</summary>
    public const string SinTableHash = "B1DE0D3E0C565AEED0B5B0C6B7A889082FE6BB9F24D3262D5743ADE3810C3323";

    [Fact]
    public void ExactEndpointsAndSymmetriesOverEveryAngle()
    {
        Assert.Equal(0, FixedTrig.Sin(0));
        Assert.Equal(FixedTrig.One, FixedTrig.Sin(FixedTrig.Quarter));
        Assert.Equal(0, FixedTrig.Sin(FixedTrig.HalfTurn));
        Assert.Equal(-FixedTrig.One, FixedTrig.Sin(3 * FixedTrig.Quarter));
        Assert.Equal(FixedTrig.One, FixedTrig.Cos(0));
        for (int a = 0; a < FixedTrig.Turn; a++)
        {
            Assert.Equal(-FixedTrig.Sin(a), FixedTrig.Sin(-a));                          // odd
            Assert.Equal(FixedTrig.Sin(a), FixedTrig.Sin(FixedTrig.HalfTurn - a));       // supplementary
            Assert.Equal(FixedTrig.Sin(a), FixedTrig.Sin(a + FixedTrig.Turn));           // periodic
            long s = FixedTrig.Sin(a), c = FixedTrig.Cos(a);
            Int128 err = (Int128)s * s + (Int128)c * c - ((Int128)1 << 60);
            Assert.True(err <= ((Int128)1 << 32) && err >= -((Int128)1 << 32), $"sin^2+cos^2 off at {a}: {err}");
        }
        for (int a = 0; a < FixedTrig.Quarter; a++) Assert.True(FixedTrig.Sin(a + 1) >= FixedTrig.Sin(a));     // monotone rise
    }

    [Fact]
    public void Atan2IsTheNearestTableAngleOverEveryAngle()
    {
        foreach (long R in new long[] { 1_000_000, 50_000_000 })
            for (int a = 0; a < FixedTrig.Turn; a++)
            {
                long dx = FixedTrig.ScaleCos(R, a), dy = FixedTrig.ScaleSin(R, a);
                int back = FixedTrig.Atan2(dy, dx);
                Assert.True(FixedTrig.Separation(back, a) <= 1, $"atan2 of angle {a} (R={R}) gave {back}");
            }
        Assert.Equal(0, FixedTrig.Atan2(0, 5));
        Assert.Equal(FixedTrig.Quarter, FixedTrig.Atan2(5, 0));
        Assert.Equal(FixedTrig.HalfTurn, FixedTrig.Atan2(0, -5));
        Assert.Equal(3 * FixedTrig.Quarter, FixedTrig.Atan2(-5, 0));
        Assert.Equal(FixedTrig.Eighth, FixedTrig.Atan2(7, 7));
        Assert.Equal(0, FixedTrig.Atan2(0, 0));
    }

    [Fact]
    public void DegreesDiffAndTurnTowardRules()
    {
        Assert.Equal(FixedTrig.Quarter, FixedTrig.FromDegrees(90));
        Assert.Equal(FixedTrig.Eighth, FixedTrig.FromDegrees(45));
        Assert.Equal(0, FixedTrig.FromDegrees(360));
        Assert.Equal(-FixedTrig.HalfTurn, FixedTrig.Diff(0, FixedTrig.HalfTurn));       // tie: exactly opposite = -HalfTurn
        Assert.Equal(10, FixedTrig.Diff(FixedTrig.Turn - 5, 5));                         // across the wrap
        // TurnToward never exceeds the bound and reaches the target exactly when within it.
        for (int h = 0; h < FixedTrig.Turn; h += 997)
            for (int w = 0; w < FixedTrig.Turn; w += 1013)
            {
                int n = HeadingMotion.TurnToward(h, w, 300);
                Assert.True(FixedTrig.Separation(h, n) <= 300);
                if (FixedTrig.Separation(h, w) <= 300) Assert.Equal(FixedTrig.Norm(w), n);
            }
        Assert.Equal(FixedTrig.Turn - 300, HeadingMotion.TurnToward(0, FixedTrig.HalfTurn, 300));   // opposite: clockwise
    }

    [Fact]
    public void TableIsBitStableGolden()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        for (int a = 0; a < FixedTrig.Turn; a++) { BinaryPrimitives.WriteInt64LittleEndian(b, FixedTrig.Sin(a)); h.AppendData(b); }
        Assert.Equal(SinTableHash, Convert.ToHexString(h.GetHashAndReset()));
    }

    [Fact]
    public void HeadingMotionIsDtCorrect()
    {
        // Rates are per second: the per-step bound scales with the step (law 3).
        Assert.Equal(2 * HeadingMotion.MaxTurn(1000, 1000), HeadingMotion.MaxTurn(1000, 2000));
        Assert.Equal(HeadingMotion.Distance(5000, 1000) * 4, HeadingMotion.Distance(5000, 4000));
        Assert.Equal(1500, HeadingMotion.Approach(1000, 9000, 250, 2000, 0, 9000));
        Assert.Equal(0, HeadingMotion.Approach(1000, 0, 500, 2000, 0, 9000));
        Assert.Equal(400, HeadingMotion.Approach(1000, 0, 300, 2000, 400, 9000));       // floor (minimum speed) holds
        // Mirror-image offsets are exact mirror images (toward-zero rounding).
        for (int a = 0; a < FixedTrig.Turn; a += 37)
        {
            var p = HeadingMotion.Offset(a, 12_345);
            var q = HeadingMotion.Offset(FixedTrig.HalfTurn - a, 12_345);
            Assert.Equal(-p.X, q.X); Assert.Equal(p.Y, q.Y);
        }
        Assert.Equal(1000, HeadingMotion.BrakingSpeed(1000, 500));           // sqrt(2*500*1000)
    }
}
