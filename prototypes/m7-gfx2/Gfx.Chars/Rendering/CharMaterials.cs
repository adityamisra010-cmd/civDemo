using Gfx.Chars.Format;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Rendering;

/// <summary>Material name → effect and per-draw parameters. Tunables are public for A/B captures.</summary>
public sealed class CharMaterials
{
    /// <summary>x subsurface strength, y base roughness, z unused, w micro-normal strength.</summary>
    public Vector4 SkinParams = new(1.0f, 0.50f, 0f, 0.55f);
    /// <summary>x micro-normal UV scale, y cavity (albedo alpha) strength.</summary>
    public Vector4 SkinParams2 = new(46f, 0.6f, 0f, 0f);
    /// <summary>y iris parallax depth (uv units), z cornea roughness, w sclera roughness (x is filled per asset: eye radius).</summary>
    public Vector4 EyeParams = new(0f, 0.012f, 0.045f, 0.28f);

    readonly Dictionary<CharAsset, (Vector4 eyeL, Vector4 eyeR, float radius, Vector4 mouth)> _geo = new();

    public bool CastsShadow(string material) => material is not ("eye" or "mouth" or "lash" or "cornea" or "brow" or "fur" or "hairshell");
    /// <summary>Fur texture tiles per UV unit: one 256-texel tile per centimetre of face (the head's UV density is about 1.17 per metre).</summary>
    public float FurTiling = 85f;
    public static bool AlphaTested(string material) => material is "hair";
    public static bool IsAdditive(string material) => material == "cornea";

    (Vector4 eyeL, Vector4 eyeR, float radius, Vector4 mouth) Geo(CharAsset a)
    {
        if (_geo.TryGetValue(a, out var g)) return g;
        var s = a.Skeleton;
        int il = s.TryIndex("eye_l", out int x) ? x : 0, ir = s.TryIndex("eye_r", out int y) ? y : 0;
        float rad = 0.012f;
        var eyes = a.File.Mesh("eyes");
        if (eyes != null)
        {
            float m = 0; var c = s.Head[il];
            foreach (var p in eyes.Positions) if (p.X > 0) m = MathF.Max(m, (p - c).Length());
            rad = m > 0 ? m : rad;
        }
        float front = 0.1f, back = 0f;
        var tu = a.File.Mesh("teeth_upper"); var tg = a.File.Mesh("tongue");
        if (tu != null && tg != null) { front = tu.Positions.Max(p => p.Z); back = tg.Positions.Min(p => p.Z); }
        g = (new Vector4(s.Head[il], il), new Vector4(s.Head[ir], ir), rad, new Vector4(front, back, 1f, 0f));
        _geo[a] = g;
        return g;
    }

    /// <summary>Field wear: mud and dust on the lower garments (0 = clean), unless a material sets its own "mud".</summary>
    public float Mud = 0.35f;

    /// <summary>Hashed (stochastic) alpha test for hair; false = fixed 0.5 threshold (A/B).</summary>
    public bool HashedAlpha = true;
    readonly Dictionary<CharAsset, Vector3> _hair = new();

    /// <summary>Linear hair colour from the identity's INFO hair_colour (sRGB hex).</summary>
    public Vector3 HairColour(CharAsset a)
    {
        if (_hair.TryGetValue(a, out var c)) return c;
        string hex = a.File.Info["hair_colour"]?.GetValue<string>() ?? "#3B2A1E";
        uint v = uint.Parse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        static float Lin(uint b) { float s = b / 255f; return s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f); }
        c = new Vector3(Lin((v >> 16) & 255), Lin((v >> 8) & 255), Lin(v & 255));
        _hair[a] = c;
        return c;
    }

    static Texture2D Tex(CharAsset a, string key, Texture2D fallback) => a.Textures.TryGetValue(key, out var t) ? t : fallback;

    static Vector3 Lin(string hex)
    {
        uint v = uint.Parse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        static float L(uint b) { float s = b / 255f; return s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f); }
        return new Vector3(L((v >> 16) & 255), L((v >> 8) & 255), L(v & 255));
    }

    static System.Text.Json.Nodes.JsonObject? Mat(CharAsset owner, string part)
        => owner.File.Info["materials"]?[part] as System.Text.Json.Nodes.JsonObject;

    static string S(System.Text.Json.Nodes.JsonObject m, string k, string d) => m[k]?.GetValue<string>() ?? d;
    static float F(System.Text.Json.Nodes.JsonObject m, string k, float d) => m[k] is { } n ? (float)n.GetValue<double>() : d;

    /// <summary>Metal base reflectance (linear F0): forged iron, polished steel, aged bronze, gilt.</summary>
    public static Vector3 MetalF0(string kind) => kind switch
    {
        "iron" => new Vector3(0.46f, 0.45f, 0.44f), "mail" => new Vector3(0.38f, 0.37f, 0.36f), "steel" => new Vector3(0.56f, 0.56f, 0.55f),
        "bronze" => new Vector3(0.72f, 0.47f, 0.26f), "gold" => new Vector3(0.95f, 0.74f, 0.33f), _ => new Vector3(0.5f),
    };

    /// <summary>Margin (m) of the skirt push-out capsules over the outermost point of the leg layers.</summary>
    public float LegMargin = 0.004f;
    readonly Dictionary<(CharAsset, CharAsset), (float thigh, float calf)> _legs = new();

    /// <summary>
    /// Thigh and calf push-out radii (m) for an identity in an outfit: the largest distance from the bone (over the middle
    /// 70 % of its length) of the skin and of every leg layer the outfit puts on it (hose and braies, which stand up to
    /// about 26 mm off the skin, boot shafts, greaves; skirts themselves excluded), plus <see cref="LegMargin"/>.
    /// Capsules taken from the skin alone let a forward thigh push the hose through a skirt.
    /// </summary>
    public (float thigh, float calf) LegRadii(CharAsset identity, CharAsset outfit)
    {
        if (_legs.TryGetValue((identity, outfit), out var r)) return r;
        var s = identity.Skeleton;
        var meshes = new List<MeshDef>();
        if (identity.File.Mesh("body") is { } body) meshes.Add(body);
        if (!ReferenceEquals(outfit, identity))
            foreach (var p in outfit.Parts)
                if ((p.Def.Flags & 1) == 0 && !CharInstance.IsLodPart(p.Name)) meshes.Add(p.Def);
        float Radius(string bone, string child)
        {
            int b = s[bone]; var A = s.Head[b]; var ab = s.Head[s[child]] - A;
            float best = 0f;
            foreach (var m in meshes)
            {
                var d = new List<float>();
                for (int i = 0; i < m.VertexCount; i++)
                {
                    if (m.Joints[4 * i] != b || m.Weights[4 * i] < 180) continue;
                    float t = Math.Clamp(Vector3.Dot(m.Positions[i] - A, ab) / ab.LengthSquared(), 0f, 1f);
                    if (t < 0.15f || t > 0.85f) continue;
                    d.Add((m.Positions[i] - (A + ab * t)).Length());
                }
                if (d.Count < 8) continue;
                best = Math.Max(best, d.Max());
            }
            return (best > 0f ? best : 0.07f) + LegMargin;
        }
        r = (Radius("thigh_l", "calf_l"), Radius("calf_l", "foot_l"));
        _legs[(identity, outfit)] = r;
        return r;
    }

    /// <summary>Extra push-out radius per skirt layer (m): each skirt further out keeps this much clear of the one under it.</summary>
    public const float SkirtLayerStep = 0.006f;

    /// <summary>
    /// Layer of a skirt part among its outfit's skirts (mesh flag 1), counted from the innermost: the bake writes an
    /// outfit's parts from the inside out (under-tunic, mail, surcoat; tunic, leather strips), so file order is layer
    /// order. Pushed out to one shared capsule, an inner skirt would land on the outer one and show through it.
    /// </summary>
    public static int SkirtLayer(CharInstance inst, GpuPart part)
    {
        int k = 0;
        foreach (var p in inst.OwnerOf(part).Parts)
        {
            if (ReferenceEquals(p, part)) return k;
            if ((p.Def.Flags & 1) != 0) k++;
        }
        return 0;
    }

    void Capsules(Effect fx, CharInstance inst, GpuPart part)
    {
        bool on = (part.Def.Flags & 1) != 0;
        fx.Parameters["CapsuleOn"].SetValue(new Vector4(on ? 1 : 0, 0, 0, 0));
        if (!on) return;
        var s = inst.Asset.Skeleton; var (rt, rc) = LegRadii(inst.Asset, inst.OwnerOf(part));
        float extra = SkirtLayerStep * SkirtLayer(inst, part);
        Vector3 W(string b) => Vector3.Transform(inst.Heads[s[b]], inst.World);
        string[] side = { "l", "r" };
        for (int k = 0; k < 2; k++)
        {
            fx.Parameters["LegA" + (2 * k)].SetValue(new Vector4(W("thigh_" + side[k]), rt + extra));
            fx.Parameters["LegB" + (2 * k)].SetValue(new Vector4(W("calf_" + side[k]), rc * 1.15f + extra));
            fx.Parameters["LegA" + (2 * k + 1)].SetValue(new Vector4(W("calf_" + side[k]), rc * 1.15f + extra));
            fx.Parameters["LegB" + (2 * k + 1)].SetValue(new Vector4(W("foot_" + side[k]), rc * 0.7f + extra));
        }
    }

    public Effect? Setup(CharRenderer r, RenderContext rc, CharInstance inst, GpuPart part)
    {
        var a = inst.Asset;
        var owner = inst.OwnerOf(part);
        var m = Mat(owner, part.Name);
        switch (part.Material)
        {
            case "cloth" when m != null:
                {
                    var fx = rc.Effects.Get(CharEffectNames.Cloth);
                    string fab = S(m, "fabric", "linen");
                    (float id, Vector4 mask, float scale, float bump, float sheen) f = fab switch
                    {
                        "wool" => (1f, new Vector4(0, 1, 0, 0), 12f, 0.0004f, 0.6f),
                        "leather" => (2f, new Vector4(0, 0, 0, 1), 6f, 0.00025f, 0.1f),
                        "felt" => (3f, new Vector4(0, 0, 0, 1), 10f, 0.00015f, 0.8f),
                        "quilted" => (4f, new Vector4(1, 0, 0, 0), 16f, 0.005f, 0.4f),
                        _ => (0f, new Vector4(1, 0, 0, 0), 16f, 0.0003f, 0.3f),
                    };
                    fx.Parameters["ClothColour"].SetValue(new Vector4(Lin(S(m, "colour", "#808080")), F(m, "rough", 0.85f)));
                    string? c2 = m["colour2"]?.GetValue<string>();
                    fx.Parameters["ClothColour2"].SetValue(new Vector4(c2 != null ? Lin(c2) : Vector3.One, c2 != null ? 1f : 0f));
                    fx.Parameters["ClothParams"].SetValue(new Vector4(f.id, F(m, "scale", f.scale), F(m, "bump", f.bump), f.sheen));
                    fx.Parameters["ClothParams2"].SetValue(new Vector4(F(m, "pattern", 0f), F(m, "mud", Mud), 0f, 0f));
                    string? c3 = m["colour3"]?.GetValue<string>();
                    fx.Parameters["ClothColour3"].SetValue(new Vector4(c3 != null ? Lin(c3) : Vector3.One, c3 != null ? 1f : 0f));
                    fx.Parameters["ChannelMask"].SetValue(f.mask);
                    fx.Parameters["Fabric"].SetValue(Tex(owner, "fabric", r.White));
                    fx.Parameters["Trim"].SetValue(Tex(owner, "trim", r.White));
                    string? em = m["emblem"]?.GetValue<string>();
                    if (em != null && m["emblem_box"] is System.Text.Json.Nodes.JsonArray box)
                    {
                        fx.Parameters["Emblem"].SetValue(Tex(owner, "emblem:" + em, r.White));
                        fx.Parameters["EmblemParams"].SetValue(new Vector4((float)box[0]!.GetValue<double>(), (float)box[1]!.GetValue<double>(), (float)box[2]!.GetValue<double>(), 1f));
                    }
                    else { fx.Parameters["Emblem"].SetValue(r.White); fx.Parameters["EmblemParams"].SetValue(Vector4.Zero); }
                    Capsules(fx, inst, part);
                    return fx;
                }
            case "metal" when m != null:
                {
                    var fx = rc.Effects.Get(CharEffectNames.Metal);
                    string kind = S(m, "metal", "iron");
                    fx.Parameters["MetalColour"].SetValue(new Vector4(MetalF0(kind), F(m, "rough", 0.38f)));
                    fx.Parameters["MetalParams"].SetValue(new Vector4(F(m, "wear", 0.4f), 0.5f, kind == "mail" ? 1f : 0f, 7.5f));
                    fx.Parameters["Fabric"].SetValue(Tex(owner, "fabric", r.White));
                    Capsules(fx, inst, part);
                    return fx;
                }
            case "wood" when m != null:
            case "paint" when m != null:
                {
                    var fx = rc.Effects.Get(CharEffectNames.Paint);
                    bool paint = part.Material == "paint";
                    var wood = paint ? Lin("#6A4A30") : Lin(S(m, "colour", "#7A5A3A"));
                    fx.Parameters["PaintColour"].SetValue(new Vector4(wood, F(m, "rough", 0.7f)));
                    fx.Parameters["PaintParams"].SetValue(new Vector4(paint ? 0f : 1f, 0.0008f, 0.6f, paint ? 1f : 1f));
                    fx.Parameters["Wood"].SetValue(Tex(owner, "wood", r.White));
                    string? em = m["emblem"]?.GetValue<string>();
                    fx.Parameters["Emblem"].SetValue(em != null ? Tex(owner, "emblem:" + em, r.White) : r.White);
                    return fx;
                }
            case "hair" when m != null:
                {
                    var fx = rc.Effects.Get(CharEffectNames.Hair);
                    fx.Parameters["HairTex"].SetValue(Tex(a, "hair", r.White));
                    fx.Parameters["HairColour"].SetValue(new Vector4(Lin(S(m, "colour", "#8C1C1C")), r.FrameSeed));
                    fx.Parameters["HairParams"].SetValue(new Vector4(HashedAlpha ? 0f : 1f, 0.12f, 80f, 1f));
                    return fx;
                }
            case "skin":
                {
                    var fx = rc.Effects.Get(CharEffectNames.Skin);
                    fx.Parameters["SkinTex"].SetValue(Tex(a, "skin", r.White));
                    fx.Parameters["SkinDetail"].SetValue(Tex(a, "skin_detail", r.FlatNormal));
                    fx.Parameters["SkinMask"].SetValue(Tex(a, "skin_mask", r.Black));
                    fx.Parameters["SkinParams"].SetValue(SkinParams);
                    fx.Parameters["SkinParams2"].SetValue(SkinParams2);
                    return fx;
                }
            case "eye":
                {
                    var fx = rc.Effects.Get(CharEffectNames.Eye);
                    var g = Geo(a);
                    fx.Parameters["EyeTex"].SetValue(Tex(a, "eye", r.White));
                    fx.Parameters["EyeParams"].SetValue(new Vector4(g.radius, EyeParams.Y, EyeParams.Z, EyeParams.W));
                    fx.Parameters["EyeCentreL"].SetValue(g.eyeL);
                    fx.Parameters["EyeCentreR"].SetValue(g.eyeR);
                    return fx;
                }
            case "hair":
            case "brow":
            case "lash":
                {
                    var fx = rc.Effects.Get(CharEffectNames.Hair);
                    fx.Parameters["HairTex"].SetValue(Tex(a, "hair", r.White));
                    var col = HairColour(a);
                    if (part.Material == "brow") col *= 0.8f;
                    if (part.Material == "lash") col = col * 0.35f + new Vector3(0.006f);
                    fx.Parameters["HairColour"].SetValue(new Vector4(col, r.FrameSeed));
                    float opacity = part.Material switch { "brow" => 0.7f, "lash" => 0.9f, _ => 1f };
                    fx.Parameters["HairParams"].SetValue(new Vector4(HashedAlpha ? 0f : 1f, 0.12f, 80f, opacity));
                    return fx;
                }
            case "lod":
                {
                    var fx = rc.Effects.Get(CharEffectNames.Lod);
                    fx.Parameters["SkinTex"].SetValue(Tex(a, "skin", r.White));
                    string? em = owner.File.Info["lod_emblem"]?.GetValue<string>();
                    fx.Parameters["Emblem"].SetValue(em != null ? Tex(owner, "emblem:" + em, r.White) : r.White);
                    return fx;
                }
            case "fur":
            case "hairshell":
                {
                    var fx = rc.Effects.Get(CharEffectNames.Fur);
                    bool scalp = part.Material == "hairshell";
                    fx.Parameters["FurTex"].SetValue(Tex(a, scalp ? "hairshell" : "fur", r.White));
                    fx.Parameters["HairColour"].SetValue(new Vector4(HairColour(a), r.FrameSeed));
                    fx.Parameters["FurParams"].SetValue(new Vector4(scalp ? 1f : FurTiling, scalp ? 1f : 0.75f, 0f, 0f));
                    return fx;
                }
            case "cornea":
                {
                    var fx = rc.Effects.Get(CharEffectNames.Cornea);
                    fx.Parameters["EyeParams"].SetValue(new Vector4(Geo(a).radius, EyeParams.Y, EyeParams.Z, EyeParams.W));
                    return fx;
                }
            case "mouth":
                {
                    var fx = rc.Effects.Get(CharEffectNames.Mouth);
                    fx.Parameters["MouthParams"].SetValue(Geo(a).mouth);
                    return fx;
                }
            default:
                return Extension?.Invoke(r, rc, inst, part, owner);
        }
    }

    /// <summary>
    /// Effects for material kinds this class does not know (stage kit: "kit", the PBR-mapped era kit). Called with the
    /// renderer, the render context, the instance, the part and the asset that owns the part; returns the configured
    /// effect or null (the part is skipped).
    /// </summary>
    public Func<CharRenderer, RenderContext, CharInstance, GpuPart, CharAsset, Effect?>? Extension { get; set; }
}
