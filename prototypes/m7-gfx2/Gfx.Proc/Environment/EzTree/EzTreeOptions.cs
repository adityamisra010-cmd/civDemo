// Port of ez-tree's options (src/lib/options.js, src/lib/enums.js), MIT, Copyright (c) 2024 Daniel Greenheck
// (github.com/dgreenheck/ez-tree @ dcf309bd86bd521083d9c70f01f2de45fdc7c457). PRESENTATION ONLY.
using System.Globalization;
using System.Text.Json;

namespace Gfx.Proc.Environment.EzTree;

public enum EzBillboard { Single, Double }

/// <summary>
/// ez-tree generation options with the JavaScript defaults. Per-level tables are indexed by branch level 0..3 (the
/// JavaScript objects use the keys "0".."3"; <see cref="Angle"/> and <see cref="Start"/> have no level-0 entry).
/// Counts stay doubles because the JavaScript loops compare against plain numbers.
/// </summary>
public sealed class EzTreeOptions
{
    public long Seed;
    /// <summary>"deciduous" or "evergreen" (the JavaScript string, compared exactly as the original does).</summary>
    public string Type = "deciduous";
    public bool Evergreen => Type == "evergreen";

    // bark
    public string BarkType = "Bark001";
    public int BarkTint = 0xffffff;
    public double BarkTextureScaleX = 1, BarkTextureScaleY = 1;

    // branches
    public double Levels = 3;
    public readonly double[] Angle = { 0, 70, 60, 60 };
    public readonly double[] Children = { 7, 7, 5, 0 };
    public Vec3d ForceDirection = new(0, 1, 0);
    public double ForceStrength = 0.01;
    public readonly double[] Gnarliness = { 0.15, 0.2, 0.3, 0.02 };
    public readonly double[] Length = { 20, 20, 10, 1 };
    public readonly double[] Radius = { 1.5, 0.7, 0.7, 0.7 };
    public readonly double[] Sections = { 12, 10, 8, 6 };
    public readonly double[] Segments = { 8, 6, 4, 3 };
    public readonly double[] Start = { 0, 0.4, 0.3, 0.3 };
    public readonly double[] Taper = { 0.7, 0.7, 0.7, 0.7 };
    public readonly double[] Twist = { 0, 0, 0, 0 };

    // leaves
    public string LeafType = "oak";
    public EzBillboard Billboard = EzBillboard.Double;
    public double LeafAngle = 10;
    public double LeafCount = 1;
    public double LeafStart = 0;
    public double LeafSize = 2.5;
    public double LeafSizeVariance = 0.7;
    public int LeafTint = 0xffffff;
    public double AlphaTest = 0.5;
    public bool RoundedNormals = true;

    /// <summary>Trellis growth is not ported (no preset enables it); loading a preset that enables it throws.</summary>
    public bool TrellisEnabled;

    /// <summary>A deep copy (the per-level tables are copied element by element).</summary>
    public EzTreeOptions Clone()
    {
        var d = (EzTreeOptions)MemberwiseClone();
        // MemberwiseClone shares the readonly table arrays: give the copy its own tables
        var fresh = new EzTreeOptions();
        Angle.CopyTo(fresh.Angle, 0); Children.CopyTo(fresh.Children, 0); Gnarliness.CopyTo(fresh.Gnarliness, 0);
        Length.CopyTo(fresh.Length, 0); Radius.CopyTo(fresh.Radius, 0); Sections.CopyTo(fresh.Sections, 0);
        Segments.CopyTo(fresh.Segments, 0); Start.CopyTo(fresh.Start, 0); Taper.CopyTo(fresh.Taper, 0); Twist.CopyTo(fresh.Twist, 0);
        fresh.Seed = d.Seed; fresh.Type = d.Type; fresh.BarkType = d.BarkType; fresh.BarkTint = d.BarkTint;
        fresh.BarkTextureScaleX = d.BarkTextureScaleX; fresh.BarkTextureScaleY = d.BarkTextureScaleY; fresh.Levels = d.Levels;
        fresh.ForceDirection = d.ForceDirection; fresh.ForceStrength = d.ForceStrength; fresh.LeafType = d.LeafType;
        fresh.Billboard = d.Billboard; fresh.LeafAngle = d.LeafAngle; fresh.LeafCount = d.LeafCount; fresh.LeafStart = d.LeafStart;
        fresh.LeafSize = d.LeafSize; fresh.LeafSizeVariance = d.LeafSizeVariance; fresh.LeafTint = d.LeafTint;
        fresh.AlphaTest = d.AlphaTest; fresh.RoundedNormals = d.RoundedNormals; fresh.TrellisEnabled = d.TrellisEnabled;
        return fresh;
    }

    /// <summary>
    /// ez-tree's <c>loadPreset</c> + <c>options.copy(json)</c>: start from the defaults and overwrite every key that
    /// both the defaults and the JSON have (keys the defaults lack are ignored, as in the JavaScript).
    /// </summary>
    public static EzTreeOptions FromJson(string json)
    {
        var o = new EzTreeOptions();
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.TryGetProperty("seed", out var seed)) o.Seed = (long)seed.GetDouble();
        if (r.TryGetProperty("type", out var type)) o.Type = type.GetString() ?? o.Type;
        if (r.TryGetProperty("bark", out var bark))
        {
            if (bark.TryGetProperty("type", out var bt)) o.BarkType = bt.GetString() ?? o.BarkType;
            if (bark.TryGetProperty("tint", out var t)) o.BarkTint = (int)t.GetDouble();
            if (bark.TryGetProperty("textureScale", out var ts))
            {
                if (ts.TryGetProperty("x", out var x)) o.BarkTextureScaleX = x.GetDouble();
                if (ts.TryGetProperty("y", out var y)) o.BarkTextureScaleY = y.GetDouble();
            }
        }
        if (r.TryGetProperty("branch", out var b))
        {
            if (b.TryGetProperty("levels", out var lv)) o.Levels = lv.GetDouble();
            Table(b, "angle", o.Angle, 1); Table(b, "children", o.Children, 0, 2); Table(b, "gnarliness", o.Gnarliness, 0);
            Table(b, "length", o.Length, 0); Table(b, "radius", o.Radius, 0); Table(b, "sections", o.Sections, 0);
            Table(b, "segments", o.Segments, 0); Table(b, "start", o.Start, 1); Table(b, "taper", o.Taper, 0); Table(b, "twist", o.Twist, 0);
            if (b.TryGetProperty("force", out var f))
            {
                if (f.TryGetProperty("direction", out var d))
                    o.ForceDirection = new Vec3d(d.GetProperty("x").GetDouble(), d.GetProperty("y").GetDouble(), d.GetProperty("z").GetDouble());
                if (f.TryGetProperty("strength", out var s)) o.ForceStrength = s.GetDouble();
            }
        }
        if (r.TryGetProperty("leaves", out var l))
        {
            if (l.TryGetProperty("type", out var lt)) o.LeafType = lt.GetString() ?? o.LeafType;
            if (l.TryGetProperty("billboard", out var bb)) o.Billboard = bb.GetString() == "single" ? EzBillboard.Single : EzBillboard.Double;
            if (l.TryGetProperty("angle", out var a)) o.LeafAngle = a.GetDouble();
            if (l.TryGetProperty("count", out var c)) o.LeafCount = c.GetDouble();
            if (l.TryGetProperty("start", out var st)) o.LeafStart = st.GetDouble();
            if (l.TryGetProperty("size", out var sz)) o.LeafSize = sz.GetDouble();
            if (l.TryGetProperty("sizeVariance", out var sv)) o.LeafSizeVariance = sv.GetDouble();
            if (l.TryGetProperty("tint", out var lti)) o.LeafTint = (int)lti.GetDouble();
            if (l.TryGetProperty("alphaTest", out var at)) o.AlphaTest = at.GetDouble();
            if (l.TryGetProperty("roundedNormals", out var rn)) o.RoundedNormals = rn.GetBoolean();
        }
        if (r.TryGetProperty("trellis", out var tr) && tr.TryGetProperty("enabled", out var te)) o.TrellisEnabled = te.GetBoolean();
        return o;
    }

    /// <summary>Copies the keys <paramref name="first"/>..<paramref name="last"/> (the default object's keys) when present.</summary>
    static void Table(JsonElement parent, string name, double[] dst, int first, int last = 3)
    {
        if (!parent.TryGetProperty(name, out var t)) return;
        for (int k = first; k <= last; k++)
            if (t.TryGetProperty(k.ToString(CultureInfo.InvariantCulture), out var v)) dst[k] = v.GetDouble();
    }
}

/// <summary>The fifteen ez-tree presets, embedded as unmodified copies of the pinned files (MIT).</summary>
public static class EzTreePresets
{
    public static readonly string[] Names =
    {
        "ash_large", "ash_medium", "ash_small", "aspen_large", "aspen_medium", "aspen_small", "bush_1", "bush_2", "bush_3",
        "oak_large", "oak_medium", "oak_small", "pine_large", "pine_medium", "pine_small",
    };

    /// <summary>The preset file's bytes exactly as pinned (for the manifest check).</summary>
    public static byte[] Bytes(string name)
    {
        using var s = typeof(EzTreePresets).Assembly.GetManifestResourceStream("ez-tree/" + name + ".json")
            ?? throw new ArgumentException($"no embedded ez-tree preset '{name}'");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static EzTreeOptions Load(string name) => EzTreeOptions.FromJson(System.Text.Encoding.UTF8.GetString(Bytes(name)));
}
