using System.Reflection;
using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// THE GLYPH GRAMMAR'S TEST CONTRACT — docs/architecture/pre-m5-visual-system.md
/// §8. Inherited from HeaderRuleBakerTests (visibility, palette-exact,
/// deterministic) and extended per axis. Headless, against the generator, never
/// against a committed PNG. Every bar below is a FLOOR the measured value clears,
/// not a limit it rests on.
/// </summary>
public class GlyphGrammarTests
{
    private static readonly GlyphState[] States =
    [
        GlyphState.Locked, GlyphState.Available, GlyphState.InProgress,
        GlyphState.Complete, GlyphState.Stalled, GlyphState.Decayed,
    ];

    private static IEnumerable<GlyphBase> Bases()
    {
        foreach (GlyphBase b in Enum.GetValues<GlyphBase>()) yield return b;
    }

    private static IEnumerable<GlyphSpec> Exemplars(SizeClass size)
    {
        foreach (GlyphBase b in Bases())
            foreach (GlyphState s in States)
                yield return GlyphSpec.Exemplar(b, s, size);
    }

    private static bool IsBibleColor(byte r, byte g, byte b)
    {
        foreach (ParchmentPalette.Rgba c in ParchmentPalette.BibleColors)
            if (c.R == r && c.G == g && c.B == b) return true;
        return false;
    }

    // --- §8.1 palette-exact ----------------------------------------------------

    [Fact]
    public void PaletteExact_EveryVisiblePixelOfEveryGlyph_IsABibleColor()
    {
        // The single-cartographer rule made exact: no blends, antialiasing only
        // in alpha. Swept over every base × state at every size, plus the rows
        // that exercise the other axes.
        int checkedGlyphs = 0;
        foreach (SizeClass size in Enum.GetValues<SizeClass>())
        {
            foreach (GlyphSpec spec in Exemplars(size))
            {
                AssertPaletteExact(GlyphBaker.Bake(spec), spec);
                checkedGlyphs++;
            }
        }
        foreach (GlyphDomain d in Enum.GetValues<GlyphDomain>())
        {
            AssertPaletteExact(GlyphBaker.Bake(new GlyphSpec(GlyphBase.Node, GlyphState.Complete, SizeClass.Px48, Domain: d)), default);
            AssertPaletteExact(GlyphBaker.Bake(new GlyphSpec(GlyphBase.Link, GlyphState.Complete, SizeClass.Px48, Domain: d)), default);
        }
        foreach (EraRegister e in Enum.GetValues<EraRegister>())
            foreach (GlyphBase b in Bases())
                AssertPaletteExact(GlyphBaker.Bake(new GlyphSpec(b, GlyphState.Complete, SizeClass.Px48, Era: e, Maturity: 0.9,
                    Placement: Placement.Map, Veterancy: Veterancy.Elite)), default);
        Assert.True(checkedGlyphs >= 10 * 6 * 4, $"only {checkedGlyphs} glyphs swept — vacuous");
    }

    private static void AssertPaletteExact(ArtImage img, GlyphSpec spec)
    {
        long visible = 0;
        for (int i = 0; i < img.Width * img.Height; i++)
        {
            int o = i * 4;
            if (img.Rgba[o + 3] == 0) continue;
            visible++;
            Assert.True(IsBibleColor(img.Rgba[o], img.Rgba[o + 1], img.Rgba[o + 2]),
                $"{spec}: pixel {i} rgb({img.Rgba[o]},{img.Rgba[o + 1]},{img.Rgba[o + 2]}) is not a bible color — a blend crept in");
        }
        Assert.True(visible > 0, $"{spec}: no visible pixel at all — vacuous");
    }

    // --- §8.2 deterministic ----------------------------------------------------

    [Fact]
    public void Deterministic_TwoBakes_ByteIdentical_AndTheSheetIsStable()
    {
        foreach (GlyphSpec spec in Exemplars(SizeClass.Px48))
        {
            ArtImage a = GlyphBaker.Bake(spec with { Placement = Placement.Map, Domain = GlyphDomain.Craft, Veterancy = Veterancy.Elite });
            ArtImage b = GlyphBaker.Bake(spec with { Placement = Placement.Map, Domain = GlyphDomain.Craft, Veterancy = Veterancy.Elite });
            Assert.Equal(a.Rgba, b.Rgba);
        }
        ArtImage s1 = GlyphSheet.Bake(), s2 = GlyphSheet.Bake();
        Assert.Equal(GlyphSheet.Width, s1.Width);
        Assert.Equal(GlyphSheet.Height, s1.Height);
        Assert.Equal(s1.Rgba, s2.Rgba);
    }

    // --- §8.3 visible at 16 px -------------------------------------------------

    [Fact]
    public void Visibility_EveryBaseInEveryState_ReadsAt16Px()
    {
        // The HeaderRuleBaker lesson (an invisible asset shipped twice): at the
        // smallest size every glyph must carry substantial ink — ≥ 10 % of its
        // box at alpha > 127 — and a peak pixel at alpha ≥ 200, so a degenerate
        // all-faint glyph cannot pass on area alone. (A 1 px hairline at 16 px
        // rarely covers a whole pixel, so "fully opaque" is the wrong bar here;
        // MEASURED peak across every base × state is 239, floor 200.) MEASURED
        // area: Locked is the faintest state by design at 14–21 %; every other
        // state sits at 19–48 % — the floor is cleared, not rested on.
        foreach (GlyphSpec spec in Exemplars(SizeClass.Px16))
        {
            ArtImage img = GlyphBaker.Bake(spec);
            long strong = 0; int peak = 0;
            for (int i = 0; i < img.Width * img.Height; i++)
            {
                byte a = img.Rgba[i * 4 + 3];
                if (a > 127) strong++;
                if (a > peak) peak = a;
            }
            double fraction = strong / (double)(img.Width * img.Height);
            Assert.True(fraction >= 0.10, $"{spec.Base}/{spec.State} at 16 px: alpha>127 on {fraction:P1} — below the 10 % floor");
            Assert.True(peak >= 200, $"{spec.Base}/{spec.State} at 16 px peaks at alpha {peak} — faint or broken");
        }
    }

    // --- §8.4 states are distinguishable ---------------------------------------

    [Fact]
    public void States_ArePairwiseDistinguishable_At16Px()
    {
        // The greyscale-distinguishability rule made numeric: any two states on
        // the same base differ in at least 4 % of the box's pixels at 16 px.
        foreach (GlyphBase b in Bases())
        {
            var bakes = new ArtImage[States.Length];
            for (int i = 0; i < States.Length; i++) bakes[i] = GlyphBaker.Bake(GlyphSpec.Exemplar(b, States[i], SizeClass.Px16));
            int pixels = 16 * 16;
            for (int i = 0; i < States.Length; i++)
            {
                for (int j = i + 1; j < States.Length; j++)
                {
                    int differ = 0;
                    for (int p = 0; p < pixels; p++)
                    {
                        int o = p * 4;
                        if (bakes[i].Rgba[o] != bakes[j].Rgba[o] || bakes[i].Rgba[o + 1] != bakes[j].Rgba[o + 1]
                            || bakes[i].Rgba[o + 2] != bakes[j].Rgba[o + 2] || bakes[i].Rgba[o + 3] != bakes[j].Rgba[o + 3]) differ++;
                    }
                    double fraction = differ / (double)pixels;
                    Assert.True(fraction >= 0.04,
                        $"{b}: {States[i]} vs {States[j]} differ in only {fraction:P1} of pixels at 16 px — indistinguishable");
                }
            }
        }
    }

    // --- §8.5 LOD is a contract -------------------------------------------------

    [Fact]
    public void Lod_At16Px_DomainRegisterAndBadgesAreSuppressed_ByteIdentically()
    {
        // Detail is ADDED with size, never lost to crowding: a 16 px glyph with
        // every axis loaded is byte-identical to the same spec with the suppressed
        // axes reset to neutral. A mutant that draws the domain mark at 16 px fails here.
        foreach (GlyphBase b in Bases())
        {
            var loaded = new GlyphSpec(b, GlyphState.Complete, SizeClass.Px16,
                Domain: GlyphDomain.Medicine, Era: EraRegister.Industrial, Maturity: 0.8,
                Veterancy: Veterancy.Elite, Strength: 1.0);
            var neutral = new GlyphSpec(b, GlyphState.Complete, SizeClass.Px16,
                Domain: GlyphDomain.None, Era: EraRegister.Primitive, Maturity: 1.0,
                Veterancy: Veterancy.Recruit, Strength: 1.0);
            Assert.Equal(GlyphBaker.Bake(neutral).Rgba, GlyphBaker.Bake(loaded).Rgba);
        }
        // And the anti-vacuity arm: at 24 px the domain mark IS drawn (the two differ).
        ArtImage with24 = GlyphBaker.Bake(new GlyphSpec(GlyphBase.Node, GlyphState.Complete, SizeClass.Px24, Domain: GlyphDomain.Medicine));
        ArtImage without24 = GlyphBaker.Bake(new GlyphSpec(GlyphBase.Node, GlyphState.Complete, SizeClass.Px24));
        Assert.NotEqual(with24.Rgba, without24.Rgba);
        // At 32 px the register IS drawn.
        ArtImage industrial = GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Era: EraRegister.Industrial));
        ArtImage primitive = GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Era: EraRegister.Primitive));
        Assert.NotEqual(industrial.Rgba, primitive.Rgba);
        // At 48 px the badge IS drawn.
        ArtImage elite = GlyphBaker.Bake(new GlyphSpec(GlyphBase.Formation, GlyphState.Complete, SizeClass.Px48, Veterancy: Veterancy.Elite));
        ArtImage recruit = GlyphBaker.Bake(new GlyphSpec(GlyphBase.Formation, GlyphState.Complete, SizeClass.Px48, Veterancy: Veterancy.Recruit));
        Assert.NotEqual(elite.Rgba, recruit.Rgba);
    }

    // --- §8.6 maturity is monotone ---------------------------------------------

    [Fact]
    public void Maturity_InkCoverage_IsNonDecreasingInTheFraction()
    {
        foreach (GlyphBase b in Bases())
        {
            long previous = -1;
            for (int step = 0; step <= 10; step++)
            {
                double m = step / 10.0;
                ArtImage img = GlyphBaker.Bake(new GlyphSpec(b, GlyphState.Complete, SizeClass.Px48, Maturity: m));
                long ink = 0;
                for (int i = 0; i < img.Width * img.Height; i++) ink += img.Rgba[i * 4 + 3];
                Assert.True(ink >= previous, $"{b}: total alpha fell from {previous} to {ink} between maturity {(step - 1) / 10.0} and {m}");
                previous = ink;
            }
        }
    }

    // --- §8.7 anatomy fence by construction -------------------------------------

    [Fact]
    public void AnatomyFence_NoFigureBase_AndFormationMarksStayInBand()
    {
        foreach (string name in Enum.GetNames<GlyphBase>())
        {
            string lower = name.ToLowerInvariant();
            Assert.False(lower.Contains("figure") || lower.Contains("person") || lower.Contains("soldier") || lower.Contains("character"),
                $"GlyphBase.{name} names a figure — the anatomy fence (D-038 Part C) forbids one");
        }
        for (int i = 0; i <= 20; i++)
        {
            int n = GlyphBaker.MarkCount(i / 20.0);
            Assert.InRange(n, 1, 5);
        }
        Assert.Equal(5, GlyphBaker.MarkCount(1.0));
        Assert.Equal(1, GlyphBaker.MarkCount(0.0));
        Assert.True(GlyphBaker.MarkCount(0.3) <= GlyphBaker.MarkCount(0.9), "marks must thin as strength drops");
    }

    // --- §8.8 read-only by construction -----------------------------------------

    [Fact]
    public void ReadOnly_NoPublicMemberOfTheGrammar_TouchesASimCoreType()
    {
        // A glyph is a READ: the grammar cannot see the world. Every public
        // signature in Sim.Ui.Art.Glyphs is checked against the Sim.Core assembly.
        Assembly core = typeof(Sim.Core.State.WorldState).Assembly;
        Assembly ui = typeof(GlyphBaker).Assembly;
        int members = 0;
        foreach (Type t in ui.GetTypes())
        {
            if (t.Namespace != "Sim.Ui.Art.Glyphs" || !t.IsPublic) continue;
            foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                members++;
                switch (m)
                {
                    case MethodInfo mi:
                        AssertNotCore(mi.ReturnType, core, $"{t.Name}.{mi.Name} returns");
                        foreach (ParameterInfo p in mi.GetParameters()) AssertNotCore(p.ParameterType, core, $"{t.Name}.{mi.Name}({p.Name})");
                        break;
                    case ConstructorInfo ci:
                        foreach (ParameterInfo p in ci.GetParameters()) AssertNotCore(p.ParameterType, core, $"{t.Name}..ctor({p.Name})");
                        break;
                    case PropertyInfo pi:
                        AssertNotCore(pi.PropertyType, core, $"{t.Name}.{pi.Name}");
                        break;
                    case FieldInfo fi:
                        AssertNotCore(fi.FieldType, core, $"{t.Name}.{fi.Name}");
                        break;
                }
            }
        }
        Assert.True(members > 20, $"only {members} public members inspected — vacuous");
    }

    private static void AssertNotCore(Type type, Assembly core, string where)
    {
        Type inner = type.IsByRef || type.IsArray ? type.GetElementType()! : type;
        Assert.False(inner.Assembly == core, $"{where} a Sim.Core type ({inner.Name}) — the grammar must not see the world");
        if (inner.IsGenericType)
            foreach (Type arg in inner.GetGenericArguments()) AssertNotCore(arg, core, where);
    }

    // --- §8.9 the light is one constant ------------------------------------------

    [Fact]
    public void Light_EveryMapGlyphCastsItsShadow_AlongTheOneGlobalDirection()
    {
        // D-038 B1: one light, stated once. The Map bake's extra ink is the flat
        // bake's alpha shifted by ObjectLight.ShadowOffset — for every base, state
        // and size, and the offset's direction never varies with size.
        foreach (SizeClass size in Enum.GetValues<SizeClass>())
        {
            (int dx, int dy) = ObjectLight.ShadowOffset(size);
            Assert.True(dx > 0 && dy > 0 && dx == dy, $"shadow offset at {size} is ({dx},{dy}) — off the global direction");
            foreach (GlyphBase b in Bases())
            {
                var spec = GlyphSpec.Exemplar(b, GlyphState.Complete, size);
                ArtImage flat = GlyphBaker.Bake(spec with { Placement = Placement.Panel });
                ArtImage lit = GlyphBaker.Bake(spec with { Placement = Placement.Map });
                int n = flat.Width;
                long shadowed = 0;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        if (flat.Rgba[(y * n + x) * 4 + 3] == 0) continue;
                        int tx = x + dx, ty = y + dy;
                        if (tx >= n || ty >= n) continue;
                        Assert.True(lit.Rgba[(ty * n + tx) * 4 + 3] > 0,
                            $"{b} at {size}: ink at ({x},{y}) casts no shadow at ({tx},{ty})");
                        shadowed++;
                    }
                }
                Assert.True(shadowed > 0, $"{b} at {size}: nothing to shadow — vacuous");
            }
        }
        Assert.Equal(ObjectLight.ShadowDirection.X, ObjectLight.ShadowDirection.Y, 12);
    }

    // --- the sheet round-trips through the codec ---------------------------------

    [Fact]
    public void Sheet_WritesAndReadsBack_ThroughPngCodec()
    {
        string path = Path.Combine(Path.GetTempPath(), $"glyph-sheet-{Guid.NewGuid():N}.png");
        try
        {
            ArtImage sheet = GlyphSheet.Bake();
            PngCodec.Write(path, sheet);
            ArtImage back = PngCodec.Read(path);
            Assert.Equal(sheet.Width, back.Width);
            Assert.Equal(sheet.Height, back.Height);
            Assert.Equal(sheet.Rgba, back.Rgba);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
