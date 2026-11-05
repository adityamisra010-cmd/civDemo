using Microsoft.Xna.Framework;

namespace Gfx.Avatar;

public enum Era { Ancient, Medieval, EarlyModern, Modern }
public enum Garment { Tunic, Robe, Mail, Coat, Suit, Uniform }
public enum Headgear { None, BronzeHelm, Laurel, KettleHelm, Crown, Tricorne, PeakedCap }

/// <summary>The computed outfit for one render. Never stored in the profile (no era field exists there).</summary>
public sealed record Outfit(Era Era, AvatarRole Role, Garment Garment, Headgear Headgear, bool Cloak, bool Cuirass,
    Color Main, Color Accent, Color Trim, Color Legs, Color Boots, bool LongSkirt, int Variant);

/// <summary>
/// Era dress = f(era capability tier, role, profile preference). Law 4: the input is an integer CAPABILITY TIER
/// derived from realized state (in Sim.Core this would be <c>AgeQuery.CurrentAge</c>, cf. ADR-033 <c>UiEra</c>),
/// never a date. In this prototype the tier is a LABELLED FIXTURE INPUT.
/// </summary>
public static class EraDress
{
    /// <summary>Placeholder tier→era map (tiers 0..7 ≈ Age index): 0–2 ancient, 3–4 medieval, 5 early-modern, ≥6 modern.</summary>
    public static Era EraFromCapabilityTier(int tier) => tier switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(tier)),
        <= 2 => Era.Ancient,
        <= 4 => Era.Medieval,
        5 => Era.EarlyModern,
        _ => Era.Modern,
    };

    public static Outfit Select(AvatarProfile p, int capabilityTier, AvatarRole role) => Select(p, EraFromCapabilityTier(capabilityTier), role);

    /// <summary>Pure function of the profile's PREFERENCES + era + role. Reads the profile, never writes it.</summary>
    public static Outfit Select(AvatarProfile p, Era era, AvatarRole role)
    {
        var pref = p.Clothing.For(role);
        int v = Math.Clamp(pref.Variant, 0, 2);
        Color main = pref.HeraldicColours ? Colour.Parse(p.Heraldry.Primary) : Colour.Parse(pref.Accent);
        Color accent = pref.HeraldicColours ? Colour.Parse(p.Heraldry.Secondary) : Color.Lerp(Colour.Parse(pref.Accent), Color.White, 0.35f);
        Color bronze = new(176, 128, 62), steel = new(150, 154, 160), gold = new(222, 182, 72), leather = new(92, 62, 40), dark = new(38, 36, 40);
        Color linen = new(214, 204, 180), wool = new(96, 84, 70);

        return (era, role) switch
        {
            (Era.Ancient, AvatarRole.Military) => new(era, role, Garment.Tunic, Headgear.BronzeHelm, Cloak: v != 2, Cuirass: true,
                main, accent, bronze, Legs: leather, Boots: leather, LongSkirt: false, v),
            (Era.Ancient, AvatarRole.Civil) => new(era, role, Garment.Robe, Headgear.None, Cloak: v == 1, Cuirass: false,
                linen, main, accent, Legs: linen, Boots: leather, LongSkirt: true, v),
            (Era.Ancient, AvatarRole.Royal) => new(era, role, Garment.Robe, Headgear.Laurel, Cloak: true, Cuirass: false,
                linen, main, gold, Legs: linen, Boots: leather, LongSkirt: true, v),
            (Era.Medieval, AvatarRole.Military) => new(era, role, Garment.Mail, Headgear.KettleHelm, Cloak: v == 1, Cuirass: false,
                main, accent, steel, Legs: steel, Boots: leather, LongSkirt: false, v),
            (Era.Medieval, AvatarRole.Civil) => new(era, role, Garment.Robe, Headgear.None, Cloak: v != 0, Cuirass: false,
                main, accent, wool, Legs: wool, Boots: leather, LongSkirt: true, v),
            (Era.Medieval, AvatarRole.Royal) => new(era, role, Garment.Robe, Headgear.Crown, Cloak: true, Cuirass: false,
                main, accent, gold, Legs: main, Boots: leather, LongSkirt: true, v),
            (Era.EarlyModern, AvatarRole.Military) => new(era, role, Garment.Coat, Headgear.Tricorne, Cloak: false, Cuirass: v == 2,
                main, accent, gold, Legs: linen, Boots: dark, LongSkirt: false, v),
            (Era.EarlyModern, AvatarRole.Civil) => new(era, role, Garment.Coat, Headgear.Tricorne, Cloak: false, Cuirass: false,
                main, accent, linen, Legs: dark, Boots: dark, LongSkirt: false, v),
            (Era.EarlyModern, AvatarRole.Royal) => new(era, role, Garment.Coat, Headgear.Crown, Cloak: true, Cuirass: false,
                main, accent, gold, Legs: linen, Boots: dark, LongSkirt: false, v),
            (Era.Modern, AvatarRole.Military) => new(era, role, Garment.Uniform, Headgear.PeakedCap, Cloak: false, Cuirass: false,
                Color.Lerp(main, new Color(70, 78, 56), 0.6f), accent, gold, Legs: Color.Lerp(main, new Color(70, 78, 56), 0.6f), Boots: dark, LongSkirt: false, v),
            (Era.Modern, AvatarRole.Civil) => new(era, role, Garment.Suit, Headgear.None, Cloak: false, Cuirass: false,
                new Color(44, 48, 58), main, linen, Legs: new Color(44, 48, 58), Boots: dark, LongSkirt: false, v),
            _ /* Modern Royal */ => new(era, role, Garment.Uniform, Headgear.None, Cloak: false, Cuirass: false,
                main, accent, gold, Legs: dark, Boots: dark, LongSkirt: false, v),
        };
    }
}
