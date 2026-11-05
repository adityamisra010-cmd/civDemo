using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Diagnostics;

/// <summary>Render target → PNG and pixel statistics for headless tests.</summary>
public static class Capture
{
    public static Color[] ReadPixels(RenderTarget2D rt)
    {
        var px = new Color[rt.Width * rt.Height]; rt.GetData(px); return px;
    }

    public static void SavePng(RenderTarget2D rt, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        rt.SaveAsPng(fs, rt.Width, rt.Height);
    }

    /// <summary>Pixels whose any channel differs from <paramref name="background"/> by more than <paramref name="tolerance"/>.</summary>
    public static int CountDifferentFrom(ReadOnlySpan<Color> px, Color background, int tolerance = 0)
    {
        int n = 0;
        foreach (var c in px)
            if (Math.Abs(c.R - background.R) > tolerance || Math.Abs(c.G - background.G) > tolerance || Math.Abs(c.B - background.B) > tolerance) n++;
        return n;
    }

    /// <summary>Pixels that differ between two equally sized images by more than the tolerance in any channel.</summary>
    public static int CountChanged(ReadOnlySpan<Color> a, ReadOnlySpan<Color> b, int tolerance = 0)
    {
        if (a.Length != b.Length) throw new ArgumentException("size mismatch");
        int n = 0;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i].R - b[i].R) > tolerance || Math.Abs(a[i].G - b[i].G) > tolerance || Math.Abs(a[i].B - b[i].B) > tolerance) n++;
        return n;
    }

    /// <summary>Number of distinct colours (cheap "is this more than a flat fill" check).</summary>
    public static int DistinctColors(ReadOnlySpan<Color> px)
    {
        var set = new HashSet<uint>();
        foreach (var c in px) set.Add(c.PackedValue);
        return set.Count;
    }
}
