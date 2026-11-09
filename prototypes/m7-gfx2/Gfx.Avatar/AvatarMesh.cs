using Gfx.Engine.Rendering;
using Gfx.Units;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Gfx.Avatar;

/// <summary>
/// A built avatar: one skinned mesh on the canonical <see cref="Skeleton"/> (rigid 1-joint parts, placeholder grade).
/// Vertex layout: [0, IdentityVertexEnd) = the physical identity (head with face, eyes, ears, hair, body);
/// [IdentityVertexEnd, end) = era outfit parts and props. Identity POSITIONS depend only on the profile's identity
/// fields; outfits recolour clothed body regions and add parts, but never move identity vertices.
/// </summary>
public sealed class AvatarModel
{
    public required MeshData<VertexSkinned> Mesh { get; init; }
    public int HeadVertexStart { get; init; }
    public int HeadVertexEnd { get; init; }
    public int IdentityVertexEnd { get; init; }
    /// <summary>Uniform world scale: profile height / modelled height (1.808 m).</summary>
    public float Scale { get; init; }
    public Outfit? Outfit { get; init; }
}

public sealed record AvatarBuildOptions(bool HeadOnly = false, bool Sword = false);

/// <summary>
/// Procedural parametric avatar mesh. The head is a deformed ellipsoid with LINEAR BLEND SHAPES: final vertex =
/// base + Σ wᵢ·Δᵢ (wᵢ = morph permille / 1000, Δᵢ = <see cref="MorphDelta"/>); eyes and ears are parts whose
/// placement/scale are blend channels too. Presentation-only floats (never hashed; the hashed input is the integer profile).
/// </summary>
public static class AvatarMesh
{
    public const float ModelHeight = 1.808f;
    public static readonly Vector3 HeadCentre = new(0, 1.685f, 0.012f);
    public static readonly Vector3 HeadRadii = new(0.086f, 0.123f, 0.108f);
    const int Lat = 24, Lon = 32;

    static float G(float x, float c, float s) { float t = (x - c) / s; return MathF.Exp(-t * t); }
    static float Sm(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3 - 2 * t); }
    static float Front(Vector3 d) => Sm(0.2f, 0.7f, d.Z);
    static float NoseMask(Vector3 d) => G(d.X, 0, 0.12f) * G(d.Y, -0.15f, 0.14f) * Front(d);
    static float EyeMask(Vector3 d) => G(MathF.Abs(d.X), 0.36f, 0.13f) * G(d.Y, 0.12f, 0.10f) * Front(d);
    static float LipMask(Vector3 d) => G(d.X, 0, 0.25f) * G(d.Y, -0.45f, 0.07f) * Front(d);

    /// <summary>Base (neutral) head surface point for unit direction d, head-local metres.</summary>
    public static Vector3 BaseHead(Vector3 d)
    {
        var p = d * HeadRadii;
        if (d.Y < 0) p.X *= 1 - 0.22f * Sm(0, 1, -d.Y);
        p.Z += 0.022f * NoseMask(d) - 0.008f * EyeMask(d) + 0.004f * G(d.Y, 0.28f, 0.08f) * Front(d) + 0.005f * LipMask(d);
        return p;
    }

    /// <summary>Blend-shape delta of morph <paramref name="m"/> at weight 1 for head direction d (metres).</summary>
    public static Vector3 MorphDelta(FaceMorph m, Vector3 d) => m switch
    {
        FaceMorph.JawWidth => new(d.X * 0.020f * Sm(0.1f, 0.7f, -d.Y), 0, 0),
        FaceMorph.Cheekbones => new(d.X * 0.016f * G(d.Y, -0.02f, 0.14f) * Sm(0, 0.6f, d.Z), 0, 0.004f * G(d.Y, -0.02f, 0.14f) * Front(d)),
        FaceMorph.BrowRidge => new(0, 0, 0.009f * G(d.Y, 0.28f, 0.08f) * Front(d) * G(d.X, 0, 0.5f)),
        FaceMorph.NoseLength => new(0, -0.012f * NoseMask(d), 0.006f * NoseMask(d)),
        FaceMorph.NoseWidth => new(d.X * 0.10f * G(d.X, 0, 0.18f) * G(d.Y, -0.2f, 0.12f) * Front(d), 0, 0),
        FaceMorph.NoseBridge => new(0, 0, 0.008f * G(d.X, 0, 0.08f) * G(d.Y, 0.05f, 0.10f) * Front(d)),
        FaceMorph.EyeSize => new(0, 0, -0.004f * EyeMask(d)),
        FaceMorph.EyeSpacing => new(MathF.Sign(d.X) * 0.006f * EyeMask(d), 0, 0),
        FaceMorph.LipFullness => new(0, 0, 0.006f * LipMask(d)),
        FaceMorph.ChinLength => new(0, -0.016f * Sm(0.6f, 0.95f, -d.Y) * G(d.X, 0, 0.45f), 0.004f * Sm(0.6f, 0.95f, -d.Y) * Front(d)),
        FaceMorph.EarSize => Vector3.Zero, // ear parts only (see ears)
        FaceMorph.AgeLines => new(0, -0.004f * G(d.Y, -0.25f, 0.2f) * Front(d), 0),
        FaceMorph.ForeheadHeight => new(0, 0.014f * Sm(0.3f, 0.9f, d.Y), 0),
        FaceMorph.HeadWidth => new(d.X * 0.010f, 0, 0),
        _ => Vector3.Zero,
    };

    static float W(AvatarProfile p, FaceMorph m) => p.Morph(m) / 1000f;

    public static AvatarModel Build(AvatarProfile p, Outfit? outfit, AvatarBuildOptions? options = null)
    {
        options ??= new AvatarBuildOptions();
        var b = new SkinnedBuilder();
        Color skin = Colour.Parse(p.SkinTone), hair = Colour.Parse(p.HairColour);
        float bw = 0.85f + 0.35f * p.Body.BuildPermille / 1000f, sw = 0.9f + 0.25f * p.Body.ShoulderPermille / 1000f;

        // ---------------- head (identity)
        int headStart = b.Count;
        BuildHead(b, p, skin, hair);
        b.Tube(new Vector3(0, options.HeadOnly ? 1.53f : 1.45f, 0), new Vector3(0, 1.61f, 0.005f), new(0.055f * bw, 0.052f * bw), new(0.05f, 0.048f), Skeleton.Neck, skin);
        int headEnd = b.Count;

        // ---------------- body (identity positions; clothed regions coloured by the outfit)
        if (!options.HeadOnly)
        {
            var o = outfit;
            Color main = o?.Main ?? new Color(180, 170, 150), legs = o?.Legs ?? main, boots = o?.Boots ?? new Color(80, 60, 40);
            Color sleeve = o?.Garment == Garment.Mail ? o.Trim : main;
            b.Tube(new(0, 0.86f, 0), new(0, 1.04f, 0), new(0.15f * bw, 0.105f * bw), new(0.145f * bw, 0.10f * bw), Skeleton.Pelvis, legs, capA: true);
            b.Tube(new(0, 1.02f, 0), new(0, 1.24f, 0.004f), new(0.145f * bw, 0.10f * bw), new(0.155f * bw, 0.105f * bw), Skeleton.Spine, main);
            b.Tube(new(0, 1.22f, 0.004f), new(0, 1.47f, 0), new(0.155f * bw, 0.105f * bw), new(0.19f * sw, 0.10f * bw), Skeleton.Chest, main, capB: true);
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? 0.20f : -0.20f; int o0 = s == 0 ? 0 : 4;
                b.Ellipsoid(new(x, 1.44f, 0), new(0.06f * bw, 0.055f * bw, 0.055f * bw), Skeleton.LUpperArm + o0, sleeve, 8, 10);
                b.Tube(new(x, 1.45f, 0), new(x, 1.17f, 0), new(0.05f * bw, 0.05f * bw), new(0.042f * bw, 0.042f * bw), Skeleton.LUpperArm + o0, sleeve);
                b.Ellipsoid(new(x, 1.17f, 0), new Vector3(0.042f * bw), Skeleton.LForearm + o0, sleeve, 6, 8);
                b.Tube(new(x, 1.17f, 0), new(x, 0.93f, 0), new(0.042f * bw, 0.042f * bw), new(0.033f, 0.033f), Skeleton.LForearm + o0, sleeve);
                b.Ellipsoid(new(x, 0.875f, 0.005f), new(0.028f, 0.055f, 0.04f), Skeleton.LHand + o0, skin, 8, 10);
                float lx = s == 0 ? 0.10f : -0.10f; int l0 = s == 0 ? 0 : 3;
                b.Tube(new(lx, 0.95f, 0), new(lx, 0.51f, 0), new(0.075f * bw, 0.075f * bw), new(0.055f * bw, 0.055f * bw), Skeleton.LThigh + l0, legs);
                b.Ellipsoid(new(lx, 0.50f, 0), new Vector3(0.053f * bw), Skeleton.LShin + l0, legs, 6, 8);
                b.Tube(new(lx, 0.50f, 0), new(lx, 0.32f, 0), new(0.05f * bw, 0.05f * bw), new(0.044f, 0.044f), Skeleton.LShin + l0, legs);
                b.Tube(new(lx, 0.33f, 0), new(lx, 0.09f, 0), new(0.047f, 0.047f), new(0.04f, 0.04f), Skeleton.LShin + l0, boots, capA: true);
                b.Box(new(lx, 0.045f, 0.045f), new(0.045f, 0.045f, 0.11f), Skeleton.LFoot + l0, boots);
            }
        }
        int identityEnd = b.Count;

        if (!options.HeadOnly && outfit != null) AddOutfit(b, outfit, bw, sw);
        if (outfit != null) AddHeadgear(b, outfit);
        if (options.Sword && !options.HeadOnly)
        {
            var steel = new Color(190, 194, 200);
            b.Box(new(-0.20f, 0.88f, 0.005f), new(0.012f, 0.05f, 0.012f), Skeleton.RHand, new Color(70, 45, 30));
            b.Box(new(-0.20f, 0.83f, 0.005f), new(0.014f, 0.008f, 0.075f), Skeleton.RHand, new Color(200, 160, 70));
            b.Box(new(-0.20f, 0.45f, 0.005f), new(0.005f, 0.37f, 0.022f), Skeleton.RHand, steel);
        }

        return new AvatarModel
        {
            Mesh = b.ToMesh(), HeadVertexStart = headStart, HeadVertexEnd = headEnd, IdentityVertexEnd = identityEnd,
            Scale = p.Body.HeightMm / 1000f / ModelHeight, Outfit = outfit,
        };
    }

    // ------------------------------------------------------------------ head

    static void BuildHead(SkinnedBuilder b, AvatarProfile p, Color skin, Color hair)
    {
        int n = (Lat + 1) * (Lon + 1);
        var dir = new Vector3[n]; var pos = new Vector3[n]; var col = new Color[n];
        float age = Math.Max(0, W(p, FaceMorph.AgeLines));
        var morphs = Enum.GetValues<FaceMorph>();
        Color lips = Color.Lerp(skin, new Color(150, 60, 60), 0.35f);
        for (int i = 0; i <= Lat; i++)
            for (int k = 0; k <= Lon; k++)
            {
                float th = MathF.PI * i / Lat, ph = MathHelper.TwoPi * k / Lon;
                var d = new Vector3(-MathF.Sin(th) * MathF.Sin(ph), MathF.Cos(th), -MathF.Sin(th) * MathF.Cos(ph));
                var q = BaseHead(d);
                foreach (var m in morphs) { float w = W(p, m); if (w != 0) q += w * MorphDelta(m, d); }
                int v = i * (Lon + 1) + k; dir[v] = d; pos[v] = HeadCentre + q;
                var c = skin;
                c = Color.Lerp(c, lips, Math.Clamp(LipMask(d) * 1.6f, 0, 1));
                float brow = G(d.Y, 0.24f, 0.04f) * Sm(0.12f, 0.2f, MathF.Abs(d.X)) * (1 - Sm(0.5f, 0.6f, MathF.Abs(d.X))) * Front(d);
                c = Color.Lerp(c, hair, Math.Clamp(brow * 1.4f, 0, 0.9f));
                float lines = age * (0.5f * G(d.Y, 0.42f, 0.05f) * Front(d) + 0.6f * G(MathF.Abs(d.X), 0.55f, 0.08f) * G(d.Y, 0.1f, 0.08f) + 0.4f * G(d.Y, -0.32f, 0.04f) * G(MathF.Abs(d.X), 0.3f, 0.1f) * Front(d));
                c = Color.Lerp(c, Color.Lerp(skin, Color.Black, 0.45f), Math.Clamp(lines, 0, 0.8f));
                if (p.FacialHair == FacialHair.Stubble) c = Color.Lerp(c, hair, 0.35f * Sm(0.1f, 0.35f, -d.Y) * (1 - LipMask(d)) * Sm(-0.2f, 0.2f, d.Z));
                col[v] = c;
            }
        var idx = new List<int>(Lat * Lon * 6);
        for (int i = 0; i < Lat; i++)
            for (int k = 0; k < Lon; k++)
            {
                int a = i * (Lon + 1) + k, bb = a + 1, c = a + Lon + 1, d = c + 1;
                idx.Add(a); idx.Add(c); idx.Add(bb); idx.Add(bb); idx.Add(c); idx.Add(d);
            }
        var nrm = SmoothNormals(pos, idx);
        b.AddRaw(pos, nrm, col, idx, Skeleton.Head);

        // hair cap / beard / moustache shells (selected triangles of the head grid, pushed out along the normal)
        bool HairAt(Vector3 d) => d.Y > MathHelper.Lerp(-0.35f, 0.5f, Sm(-0.6f, 0.8f, d.Z)) && !(MathF.Abs(d.X) > 0.75f && d.Y < 0.35f && d.Z > -0.2f);
        bool BeardAt(Vector3 d) => d.Y < -0.22f && d.Z > 0.05f && LipMask(d) < 0.3f && NoseMask(d) < 0.3f && !(d.Y > -0.45f && MathF.Abs(d.X) > 0.85f);
        bool MoustacheAt(Vector3 d) => G(d.Y, -0.33f, 0.05f) * G(d.X, 0, 0.3f) * Front(d) > 0.35f;
        switch (p.Hair)
        {
            case HairStyle.Short: b.Shell(pos, nrm, dir, idx, HairAt, 0.006f, hair, Skeleton.Head); break;
            case HairStyle.Curly: b.Shell(pos, nrm, dir, idx, HairAt, 0.013f, hair, Skeleton.Head, lumpy: true); break;
            case HairStyle.Long:
                b.Shell(pos, nrm, dir, idx, HairAt, 0.008f, hair, Skeleton.Head);
                b.Box(HeadCentre + new Vector3(0, -0.10f, -0.07f), new(0.075f, 0.11f, 0.02f), Skeleton.Head, hair);
                break;
            case HairStyle.TiedBack:
                b.Shell(pos, nrm, dir, idx, HairAt, 0.005f, hair, Skeleton.Head);
                b.Ellipsoid(HeadCentre + new Vector3(0, -0.02f, -0.115f), new(0.025f, 0.05f, 0.025f), Skeleton.Head, hair, 6, 8);
                break;
        }
        if (p.FacialHair == FacialHair.FullBeard) b.Shell(pos, nrm, dir, idx, BeardAt, 0.008f, hair, Skeleton.Head, lumpy: true);
        if (p.FacialHair is FacialHair.FullBeard or FacialHair.Moustache) b.Shell(pos, nrm, dir, idx, MoustacheAt, 0.006f, hair, Skeleton.Head);

        // eyes (placement and size are blend channels)
        float eyeR = 0.0115f * (1 + 0.15f * W(p, FaceMorph.EyeSize));
        float ex = 0.031f * (1 + 0.12f * W(p, FaceMorph.EyeSpacing)) + 0.002f * W(p, FaceMorph.HeadWidth);
        var eyeCol = Colour.Parse(p.EyeColour);
        for (int s = -1; s <= 1; s += 2)
        {
            var dEye = Vector3.Normalize(new Vector3(s * ex / HeadRadii.X, 0.12f, 0.93f));
            var surf = BaseHead(dEye) + W(p, FaceMorph.EyeSize) * MorphDelta(FaceMorph.EyeSize, dEye) + W(p, FaceMorph.EyeSpacing) * MorphDelta(FaceMorph.EyeSpacing, dEye);
            var c = HeadCentre + new Vector3(s * ex, surf.Y, surf.Z - 0.6f * eyeR);
            b.Ellipsoid(c, new Vector3(eyeR), Skeleton.Head, new Color(235, 232, 225), 8, 10);
            b.Ellipsoid(c + new Vector3(0, 0, 0.6f * eyeR), new Vector3(0.55f * eyeR, 0.55f * eyeR, 0.5f * eyeR), Skeleton.Head, eyeCol, 6, 8);
            b.Ellipsoid(c + new Vector3(0, 0, 0.9f * eyeR), new Vector3(0.24f * eyeR), Skeleton.Head, new Color(12, 10, 10), 4, 6);
        }
        // ears
        float earS = 1 + 0.3f * W(p, FaceMorph.EarSize);
        float earX = HeadRadii.X * 0.97f + 0.010f * W(p, FaceMorph.HeadWidth);
        for (int s = -1; s <= 1; s += 2)
            b.Ellipsoid(HeadCentre + new Vector3(s * earX, -0.005f, -0.006f), new Vector3(0.010f, 0.026f, 0.017f) * earS, Skeleton.Head, Color.Lerp(skin, Color.Black, 0.06f), 6, 8);
    }

    static Vector3[] SmoothNormals(Vector3[] pos, List<int> idx)
    {
        var n = new Vector3[pos.Length];
        for (int t = 0; t < idx.Count; t += 3)
        {
            int a = idx[t], b = idx[t + 1], c = idx[t + 2];
            var fn = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
            n[a] += fn; n[b] += fn; n[c] += fn;
        }
        for (int i = 0; i < n.Length; i++)
            n[i] = n[i].LengthSquared() > 1e-20f ? Vector3.Normalize(n[i]) : Vector3.Normalize(pos[i] - HeadCentre);
        // winding check: make normals point away from the head centre
        for (int i = 0; i < n.Length; i++) if (Vector3.Dot(n[i], pos[i] - HeadCentre) < 0) n[i] = -n[i];
        return n;
    }

    // ------------------------------------------------------------------ outfit

    static void AddOutfit(SkinnedBuilder b, Outfit o, float bw, float sw)
    {
        // skirts / coat tails / tunic hem (open tubes, rigid to the pelvis)
        float hem = o.Garment switch { Garment.Robe => 0.14f, Garment.Tunic => 0.58f, Garment.Mail => 0.62f, Garment.Coat => 0.52f, _ => -1f };
        if (o.LongSkirt) hem = 0.14f;
        if (hem > 0)
        {
            float flare = o.LongSkirt ? 0.25f : 0.21f;
            Color skirt = o.Garment == Garment.Mail ? o.Trim : o.Main;
            b.Tube(new(0, 1.08f, 0), new(0, hem, -0.01f), new(0.165f * bw, 0.115f * bw), new(flare * bw, (flare - 0.04f) * bw), Skeleton.Pelvis, skirt);
            if (o.Garment == Garment.Mail) // heraldic surcoat over mail
                b.Tube(new(0, 1.40f, 0), new(0, 0.70f, -0.005f), new(0.175f * bw, 0.12f * bw), new(0.205f * bw, 0.15f * bw), Skeleton.Spine, o.Main);
        }
        if (o.Cuirass)
            b.Tube(new(0, 0.98f, 0.004f), new(0, 1.47f, 0), new(0.165f * bw, 0.118f * bw), new(0.20f * sw, 0.115f * bw), Skeleton.Chest, o.Trim, capB: true);
        if (o.Cloak)
        {
            var rot = Matrix.CreateRotationX(-0.12f);
            float len = o.LongSkirt ? 0.62f : 0.50f;
            b.Box(new(0, 1.45f - len, -0.135f * bw - 0.03f), new(0.22f * sw, len, 0.012f), Skeleton.Chest, Color.Lerp(o.Main, Color.Black, 0.3f), rot, pivot: new(0, 1.45f, 0));
            b.Box(new(0, 1.46f, 0), new(0.20f * sw, 0.018f, 0.12f * bw), Skeleton.Chest, o.Accent); // mantle / collar
        }
        switch (o.Garment)
        {
            case Garment.Suit:
                b.Box(new(0, 1.40f, 0.105f * bw), new(0.035f, 0.05f, 0.006f), Skeleton.Chest, new Color(235, 235, 230));
                b.Box(new(0, 1.30f, 0.11f * bw), new(0.016f, 0.10f, 0.006f), Skeleton.Chest, o.Accent);
                break;
            case Garment.Uniform:
                b.Tube(new(0, 1.03f, 0), new(0, 1.07f, 0), new(0.152f * bw, 0.108f * bw), new(0.152f * bw, 0.108f * bw), Skeleton.Spine, new Color(40, 30, 22));
                for (int s = -1; s <= 1; s += 2) b.Box(new(s * 0.17f * sw, 1.475f, 0), new(0.045f, 0.008f, 0.04f), Skeleton.Chest, o.Trim);
                for (int k = 0; k < 4; k++) b.Box(new(0, 1.40f - k * 0.08f, 0.108f * bw), new(0.008f), Skeleton.Chest, o.Trim);
                break;
            case Garment.Coat:
                for (int s = -1; s <= 1; s += 2) b.Box(new(s * 0.05f, 1.33f, 0.106f * bw), new(0.022f, 0.12f, 0.006f), Skeleton.Chest, o.Accent);
                break;
        }
        if (o.Role == AvatarRole.Royal) b.Box(new(0, 1.43f, 0.10f * bw), new(0.10f, 0.012f, 0.008f), Skeleton.Chest, o.Trim);
    }

    static void AddHeadgear(SkinnedBuilder b, Outfit o)
    {
        var C = HeadCentre; var R = HeadRadii;
        Color steel = new(150, 154, 160), dark = new(30, 28, 30), gold = new(222, 182, 72);
        switch (o.Headgear)
        {
            case Headgear.BronzeHelm:
                b.Ellipsoid(C + new Vector3(0, 0.012f, -0.004f), R * 1.16f, Skeleton.Head, o.Trim, 10, 16, thetaMax: 0.52f);
                b.Box(C + new Vector3(0, R.Y + 0.05f, -0.01f), new(0.012f, 0.03f, 0.10f), Skeleton.Head, o.Main);
                break;
            case Headgear.KettleHelm:
                b.Ellipsoid(C + new Vector3(0, 0.02f, 0), R * 1.14f, Skeleton.Head, steel, 10, 16, thetaMax: 0.5f);
                b.Tube(C + new Vector3(0, 0.025f, 0), C + new Vector3(0, 0.035f, 0), new(0.15f, 0.16f), new(0.15f, 0.16f), Skeleton.Head, steel, capA: true, capB: true);
                break;
            case Headgear.Laurel:
                for (int k = 0; k < 14; k++)
                {
                    float a = MathHelper.TwoPi * k / 14;
                    b.Ellipsoid(C + new Vector3(MathF.Sin(a) * R.X * 1.04f, 0.055f, MathF.Cos(a) * R.Z * 1.04f), new(0.012f, 0.008f, 0.012f), Skeleton.Head, new Color(70, 120, 50), 4, 6);
                }
                break;
            case Headgear.Crown:
                b.Tube(C + new Vector3(0, 0.045f, 0), C + new Vector3(0, 0.085f, 0), new(R.X * 1.08f, R.Z * 1.08f), new(R.X * 1.1f, R.Z * 1.1f), Skeleton.Head, gold);
                for (int k = 0; k < 8; k++)
                {
                    float a = MathHelper.TwoPi * k / 8;
                    b.Box(C + new Vector3(MathF.Sin(a) * R.X * 1.1f, 0.10f, MathF.Cos(a) * R.Z * 1.1f), new(0.007f, 0.016f, 0.007f), Skeleton.Head, gold);
                }
                b.Ellipsoid(C + new Vector3(0, 0.065f, R.Z * 1.1f), new Vector3(0.008f), Skeleton.Head, o.Accent, 4, 6);
                break;
            case Headgear.Tricorne:
                b.Tube(C + new Vector3(0, 0.065f, 0), C + new Vector3(0, 0.13f, -0.005f), new(0.16f, 0.15f), new(0.085f, 0.09f), Skeleton.Head, dark, capA: true, capB: true);
                b.Tube(C + new Vector3(0, 0.063f, 0), C + new Vector3(0, 0.07f, 0), new(0.162f, 0.152f), new(0.162f, 0.152f), Skeleton.Head, o.Trim);
                break;
            case Headgear.PeakedCap:
                b.Tube(C + new Vector3(0, 0.05f, 0), C + new Vector3(0, 0.11f, -0.005f), new(0.088f, 0.10f), new(0.105f, 0.115f), Skeleton.Head, o.Main, capB: true);
                b.Box(C + new Vector3(0, 0.055f, 0.105f), new(0.07f, 0.005f, 0.035f), Skeleton.Head, dark);
                b.Box(C + new Vector3(0, 0.075f, 0.098f), new(0.012f), Skeleton.Head, o.Trim);
                break;
        }
    }
}

/// <summary>Accumulates rigid (1-joint) skinned geometry.</summary>
internal sealed class SkinnedBuilder
{
    readonly List<VertexSkinned> _v = new(); readonly List<int> _i = new();
    public int Count => _v.Count;

    VertexSkinned V(Vector3 p, Vector3 n, Color c, int joint) => new(p, n, c, new Byte4(joint, 0, 0, 0), new Vector4(1, 0, 0, 0));

    public MeshData<VertexSkinned> ToMesh() => new(_v.ToArray(), _i.ToArray());

    public void AddRaw(Vector3[] pos, Vector3[] nrm, Color[] col, List<int> idx, int joint)
    {
        int b = _v.Count;
        for (int i = 0; i < pos.Length; i++) _v.Add(V(pos[i], nrm[i], col[i], joint));
        foreach (var x in idx) _i.Add(b + x);
    }

    /// <summary>Copies the triangles whose three directions pass <paramref name="mask"/>, offset along the normal.</summary>
    public void Shell(Vector3[] pos, Vector3[] nrm, Vector3[] dir, List<int> idx, Func<Vector3, bool> mask, float offset, Color col, int joint, bool lumpy = false)
    {
        for (int t = 0; t < idx.Count; t += 3)
        {
            int a = idx[t], b = idx[t + 1], c = idx[t + 2];
            if (!mask(dir[a]) || !mask(dir[b]) || !mask(dir[c])) continue;
            int s = _v.Count;
            foreach (var k in new[] { a, b, c })
            {
                float off = offset;
                if (lumpy) off *= 0.7f + 0.6f * (0.5f + 0.5f * MathF.Sin(dir[k].X * 37f) * MathF.Sin(dir[k].Y * 29f + dir[k].Z * 17f));
                var shade = Color.Lerp(col, Color.Black, lumpy ? 0.1f * (1 + MathF.Sin(dir[k].Y * 61f)) : 0);
                _v.Add(V(pos[k] + nrm[k] * off, nrm[k], shade, joint));
            }
            _i.Add(s); _i.Add(s + 1); _i.Add(s + 2);
        }
    }

    public void Ellipsoid(Vector3 c, Vector3 r, int joint, Color col, int lat, int lon, float thetaMax = 1f)
    {
        int b = _v.Count;
        for (int i = 0; i <= lat; i++)
            for (int k = 0; k <= lon; k++)
            {
                float th = MathF.PI * thetaMax * i / lat, ph = MathHelper.TwoPi * k / lon;
                var d = new Vector3(MathF.Sin(th) * MathF.Sin(ph), MathF.Cos(th), MathF.Sin(th) * MathF.Cos(ph));
                _v.Add(V(c + d * r, Vector3.Normalize(d / r), col, joint));
            }
        for (int i = 0; i < lat; i++)
            for (int k = 0; k < lon; k++)
            {
                int a = b + i * (lon + 1) + k, bb = a + 1, cc = a + lon + 1, d = cc + 1;
                _i.Add(a); _i.Add(cc); _i.Add(bb); _i.Add(bb); _i.Add(cc); _i.Add(d);
            }
    }

    /// <summary>Elliptical tube a→b (radii x,z at each end), optional end caps. Renderer culls nothing, winding is free.</summary>
    public void Tube(Vector3 a, Vector3 bEnd, Vector2 rA, Vector2 rB, int joint, Color col, bool capA = false, bool capB = false, int sides = 12)
    {
        int b0 = _v.Count;
        var axis = Vector3.Normalize(bEnd - a);
        for (int r = 0; r < 2; r++)
            for (int k = 0; k <= sides; k++)
            {
                float ang = MathHelper.TwoPi * k / sides;
                var rr = r == 0 ? rA : rB; var cen = r == 0 ? a : bEnd;
                var off = new Vector3(MathF.Sin(ang) * rr.X, 0, MathF.Cos(ang) * rr.Y);
                var n = Vector3.Normalize(new Vector3(MathF.Sin(ang) / Math.Max(rr.X, 1e-4f), 0, MathF.Cos(ang) / Math.Max(rr.Y, 1e-4f)));
                n = Vector3.Normalize(n - Vector3.Dot(n, axis) * axis * 0.5f);
                _v.Add(V(cen + off, n, col, joint));
            }
        for (int k = 0; k < sides; k++) { int p = b0 + k, q = p + sides + 1; _i.Add(p); _i.Add(q); _i.Add(p + 1); _i.Add(p + 1); _i.Add(q); _i.Add(q + 1); }
        if (capA) Cap(a, rA, -axis, joint, col, sides);
        if (capB) Cap(bEnd, rB, axis, joint, col, sides);
    }

    void Cap(Vector3 c, Vector2 r, Vector3 n, int joint, Color col, int sides)
    {
        int b = _v.Count; _v.Add(V(c, n, col, joint));
        for (int k = 0; k <= sides; k++) { float a = MathHelper.TwoPi * k / sides; _v.Add(V(c + new Vector3(MathF.Sin(a) * r.X, 0, MathF.Cos(a) * r.Y), n, col, joint)); }
        for (int k = 0; k < sides; k++) { _i.Add(b); _i.Add(b + 1 + k); _i.Add(b + 2 + k); }
    }

    public void Box(Vector3 c, Vector3 e, int joint, Color col, Matrix? rot = null, Vector3? pivot = null)
    {
        var tmpV = new List<Gfx.Engine.Rendering.VertexPNC>(24); var tmpI = new List<int>(36);
        Primitives.AddBox(tmpV, tmpI, c, e, col);
        int b = _v.Count;
        foreach (var v in tmpV)
        {
            var p = v.Position; var n = v.Normal;
            if (rot is { } m) { var pv = pivot ?? c; p = Vector3.Transform(p - pv, m) + pv; n = Vector3.TransformNormal(n, m); }
            _v.Add(V(p, n, col, joint));
        }
        foreach (var x in tmpI) _i.Add(b + x);
    }
}
