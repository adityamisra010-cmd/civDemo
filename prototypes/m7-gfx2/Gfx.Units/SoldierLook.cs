using Microsoft.Xna.Framework;

namespace Gfx.Units;

/// <summary>Equipment kit of a formation (from the unit identity; the fixture's profile name maps to one).</summary>
public enum Kit { Spearman = 0, Swordsman = 1, Archer = 2, Musketeer = 3 }

/// <summary>
/// Per-soldier appearance, PRESENTATION ONLY, derived deterministically from (entityId, kit) with an integer
/// SplitMix64 hash — integer fields so the derivation is platform-exact (floats appear only when rendering).
/// Never feeds back into any simulation input.
/// </summary>
public readonly record struct SoldierLook(Kit Kit, int Helmet, int HeightPermille, int WidthPermille, int SkinTone, int ClothShade, int ClothJitterPermille, int AnimOffsetMs)
{
    /// <summary>Helmet index: −1 none, else (int)Part of a helmet part.</summary>
    public const int NoHelmet = -1;

    public static ulong Mix(ulong z)
    {
        unchecked
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    static int Pick(ulong h, int salt, int n) => (int)(Mix(h ^ (ulong)salt * 0xD1B54A32D192ED03UL) % (ulong)n);

    static readonly int[][] HelmetsByKit =
    {
        new[] { (int)Part.HelmetCone, (int)Part.HelmetCrested, (int)Part.HelmetCone },
        new[] { (int)Part.HelmetCrested, (int)Part.HelmetCone },
        new[] { NoHelmet, (int)Part.HelmetCone, NoHelmet, (int)Part.HelmetKettle },
        new[] { (int)Part.HelmetKettle, (int)Part.HelmetKettle, NoHelmet },
    };

    /// <param name="skinBase">0..255 centre of the population's skin-tone range (culture input; presentation).</param>
    public static SoldierLook For(long entityId, Kit kit, int skinBase = 110)
    {
        ulong h = Mix(unchecked((ulong)entityId) * 0x2545F4914F6CDD1DUL + (ulong)kit);
        var helmets = HelmetsByKit[(int)kit];
        int tone = skinBase - 40 + Pick(h, 5, 81);
        return new SoldierLook(kit,
            helmets[Pick(h, 1, helmets.Length)],
            930 + Pick(h, 2, 141),            // 0.93 .. 1.07 height
            920 + Pick(h, 3, 181),            // 0.92 .. 1.10 width
            Math.Clamp(tone, 0, 255),
            Pick(h, 4, 4),
            940 + Pick(h, 6, 121),            // ±6 % cloth brightness
            Pick(h, 7, 2000));                // loop phase offset
    }

    /// <summary>Parts carried (besides the helmet). LOD1 keeps only the large silhouette parts.</summary>
    public static ReadOnlySpan<Part> KitParts(Kit k) => k switch
    {
        Kit.Spearman => new[] { Part.ShieldRound, Part.Spear },
        Kit.Swordsman => new[] { Part.ShieldRect, Part.Sword },
        Kit.Archer => new[] { Part.Bow, Part.Quiver },
        _ => new[] { Part.Musket },
    };

    public static bool InLod1(Part p) => p is Part.ShieldRound or Part.ShieldRect or Part.Spear or Part.Bow or Part.Musket;

    // ---- presentation (float) mapping

    static readonly Vector3[][] SidePalettes =
    {
        new[] { new Vector3(0.62f, 0.12f, 0.10f), new Vector3(0.72f, 0.22f, 0.12f), new Vector3(0.52f, 0.10f, 0.14f), new Vector3(0.78f, 0.32f, 0.18f) },
        new[] { new Vector3(0.14f, 0.24f, 0.55f), new Vector3(0.20f, 0.33f, 0.62f), new Vector3(0.10f, 0.19f, 0.44f), new Vector3(0.26f, 0.40f, 0.60f) },
        new[] { new Vector3(0.80f, 0.74f, 0.55f), new Vector3(0.70f, 0.64f, 0.48f), new Vector3(0.85f, 0.82f, 0.70f), new Vector3(0.62f, 0.58f, 0.40f) },
    };

    /// <summary>Instance tint: rgb = sRGB cloth colour, a = skin tone 0..1 (shader mixes light→dark skin).</summary>
    public Vector4 Tint(int side)
    {
        var c = SidePalettes[Math.Clamp(side, 0, SidePalettes.Length - 1)][ClothShade] * (ClothJitterPermille / 1000f);
        return new Vector4(Vector3.Clamp(c, Vector3.Zero, Vector3.One), SkinTone / 255f);
    }

    public Matrix Scale => Matrix.CreateScale(WidthPermille / 1000f, HeightPermille / 1000f, WidthPermille / 1000f);
}
