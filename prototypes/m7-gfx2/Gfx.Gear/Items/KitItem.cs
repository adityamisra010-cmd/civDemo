using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Items;

/// <summary>One drawable part of an item: a mesh in item space and the map set it is shaded with.</summary>
public sealed class KitPart
{
    public required string Name { get; init; }
    public required KitMesh Mesh { get; init; }
    /// <summary>Key of the item's map set (<see cref="KitItem.Maps"/>), for material "kit".</summary>
    public string Maps { get; init; } = "";
    /// <summary>"kit" (PBR maps) or "hair" (the character hair-card shader, e.g. a horsehair crest).</summary>
    public string Material { get; init; } = "kit";
    public float NormalStrength { get; init; } = 1f;
    public float Sheen { get; init; }
    public bool AlphaTest { get; init; }
    /// <summary>For material "hair": the dyed colour (sRGB hex).</summary>
    public string? HairColour { get; init; }
}

/// <summary>
/// A generated item of the era kit: parts, the map sets they use, and named frames in item space (4x4, XNA row-vector
/// convention: p_item = p_frame * Frame). Frames say how the item is held or worn:
/// <list type="bullet">
/// <item><c>grip</c>: the main hand's fist. Origin on the grip's axis where the fist closes, +Y along the grip from the
/// little-finger side to the thumb side, +X from the wrist towards the knuckles, +Z = X cross Y (the palm's normal side),
/// matching the character sockets <c>grip_r</c>/<c>grip_l</c>.</item>
/// <item><c>grip2</c>: the off hand on a two-handed weapon (same convention).</item>
/// <item><c>forearm</c>: strap shields: origin where the forearm's back lies in the arm band, +Y along the forearm (elbow
/// to wrist), +Z out of the shield's face, matching the socket <c>forearm_l</c>.</item>
/// <item><c>hang</c>: carried, not in hand (a sheathed sword or a slung shield), matching the socket it hangs from.</item>
/// <item><c>head</c>: helmets: origin at the head's centre between the eyes' height and the skull's middle, +Y up, +Z forward.</item>
/// </list>
/// PRESENTATION ONLY: nothing here reads or writes simulation state.
/// </summary>
public sealed class KitItem
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    /// <summary>Era label (ancient, medieval, early_modern) and a short description for legends.</summary>
    public string Era { get; init; } = "";
    public string Label { get; set; } = "";
    public List<KitPart> Parts { get; } = new();
    public Dictionary<string, MapSet> Maps { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Matrix> Frames { get; } = new(StringComparer.Ordinal);
    /// <summary>Grip radius (m) of the main grip (finger wrap), and of the off-hand grip.</summary>
    public float GripRadius { get; set; } = 0.015f;
    public float Grip2Radius { get; set; } = 0.015f;
    /// <summary>Centre of mass in item space (dropped items fall and settle about it).</summary>
    public Vector3 CentreOfMass { get; set; }
    /// <summary>Bounding radius about the origin (culling).</summary>
    public float Radius => Parts.Count == 0 ? 0.1f : Parts.Max(p => p.Mesh.Positions.Count == 0 ? 0f : p.Mesh.Positions.Max(q => q.Length())) + 0.05f;
    public int TriangleCount => Parts.Sum(p => p.Mesh.TriangleCount);

    public Matrix Frame(string name) => Frames.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException($"{Id}: no frame '{name}'");

    /// <summary>A frame from an origin and two axes (y and an approximate z; x completes a right-handed basis).</summary>
    public static Matrix MakeFrame(Vector3 origin, Vector3 y, Vector3 zApprox)
    {
        y = Vector3.Normalize(y);
        var z = zApprox - y * Vector3.Dot(zApprox, y);
        z = Vector3.Normalize(z);
        var x = Vector3.Cross(y, z);
        return new Matrix(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, origin.X, origin.Y, origin.Z, 1);
    }

    /// <summary>SHA-256 over every part's mesh hash and every map set's hash, in name order (determinism fingerprint).</summary>
    public string Hash()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in Parts.OrderBy(p => p.Name, StringComparer.Ordinal)) sb.Append(p.Name).Append(':').Append(p.Mesh.Hash()).Append(';');
        foreach (var (k, m) in Maps.OrderBy(k => k.Key, StringComparer.Ordinal)) sb.Append(k).Append(':').Append(m.Hash()).Append(';');
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
