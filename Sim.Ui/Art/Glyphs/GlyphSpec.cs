namespace Sim.Ui.Art.Glyphs;

/// <summary>
/// THE SILHOUETTE FAMILY — what KIND of thing a glyph stands for
/// (docs/architecture/pre-m5-visual-system.md §2.1). Every member is an analytic
/// silhouette in the unit box that must read alone at 16 px. There is no figure
/// member and the grammar has no way to draw one: <see cref="Formation"/> is the
/// only base that stands for people, and it is a bar of marks — D-038 Part C /
/// style-bible §7 (the anatomy fence) and D-011 §4 (the formation token).
/// </summary>
public enum GlyphBase
{
    /// <summary>Wide gable — ordinary buildings, granary, workshop.</summary>
    Hall = 0,
    /// <summary>Tall narrow with a cap — watchtower, keep, stack-class industry.</summary>
    Tower = 1,
    /// <summary>Annulus — walls, enclosure.</summary>
    Ring = 2,
    /// <summary>Half-disc on a plinth — temple, shrine, observatory.</summary>
    Dome = 3,
    /// <summary>Plinth, three columns, pediment — INSTITUTIONS: a capability a
    /// settlement has, never an object at coordinates (D-038 H2).</summary>
    Portico = 4,
    /// <summary>A bar containing a cluster of marks — UNITS. Strength is the mark
    /// count, thinning as it drops. Never a figure.</summary>
    Formation = 5,
    /// <summary>Circle — progression / research nodes.</summary>
    Node = 6,
    /// <summary>Diamond (the HeaderRuleBaker lozenge, reused) — milestones, Age cells.</summary>
    Lozenge = 7,
    /// <summary>Three discs on a baseline — resources / goods stocks.</summary>
    Heap = 8,
    /// <summary>Two small nodes joined by a stroke — infrastructure edges.</summary>
    Link = 9,
}

/// <summary>
/// THE DOMAIN MARK — a VISUAL alphabet (§2.2). Its binding to simulation domains
/// is the knowledge packet's decision, not this enumeration's: nothing in Sim.Core
/// names a research domain today (pre-m5-repository-audit.md §1), and this list
/// must not be read as one.
/// </summary>
public enum GlyphDomain
{
    None = 0,
    Agriculture = 1,
    Craft = 2,
    Military = 3,
    Maritime = 4,
    Letters = 5,
    /// <summary>A plain Greek cross in INK — never red (palette; protected emblem).</summary>
    Medicine = 6,
    Commerce = 7,
    Civic = 8,
}

/// <summary>
/// THE STYLE REGISTER — how a silhouette is drawn, NEVER when (§2.3). The value is
/// an input chosen by the view-model from computed state; no glyph reads a date,
/// a turn or an era label to pick one (CLAUDE.md law 4). Today the tree carries
/// no register on any structure kind, so every caller passes Primitive and the
/// axis is inert.
/// </summary>
public enum EraRegister
{
    Primitive = 0,
    Classical = 1,
    Medieval = 2,
    Industrial = 3,
    Modern = 4,
}

/// <summary>THE STATE ALPHABET — what is happening to the thing (§2.5). Closed; each
/// member has a distinct silhouette-level signature at 16 px (tested).</summary>
public enum GlyphState
{
    Locked = 0,
    Available = 1,
    /// <summary>Researching / under construction: progress arc + scaffold hatch + wash to the fraction.</summary>
    InProgress = 2,
    /// <summary>Unlocked / complete.</summary>
    Complete = 3,
    /// <summary>In progress but not advancing: dashed ring with the arc frozen.</summary>
    Stalled = 4,
    /// <summary>Ring and outline with gaps.</summary>
    Decayed = 5,
}

/// <summary>Panel: flat, no shadow (substrate/furniture lighting, style-bible §1).
/// Map: world-anchored at constant screen size with a contact shadow along the one
/// global light (D-038 B1, <see cref="ObjectLight"/>).</summary>
public enum Placement
{
    Panel = 0,
    Map = 1,
}

/// <summary>The four size classes of the LOD contract (§2.7). The numeric value is
/// the glyph box in pixels.</summary>
public enum SizeClass
{
    Px16 = 16,
    Px24 = 24,
    Px32 = 32,
    Px48 = 48,
}

/// <summary>A badge under the formation token: 0–3 chevrons, the third in gold-leaf.</summary>
public enum Veterancy
{
    Recruit = 0,
    Regular = 1,
    Veteran = 2,
    Elite = 3,
}

/// <summary>
/// ONE GLYPH, FULLY DESCRIBED. A pure value: the composer is a total function of
/// it, and a view-model is the only thing that builds one — from
/// IReadOnlyWorldState or an observation record, never from inside the grammar
/// (§0 rule 1; pinned by the reflection test: no member of this namespace takes
/// or returns a Sim.Core type).
/// </summary>
/// <param name="Maturity">Wash fraction ∈ [0,1] (§2.4). Pass 1.0 when no stock exists.</param>
/// <param name="Progress">Arc fraction ∈ [0,1]; read only for InProgress / Stalled.</param>
/// <param name="Strength">Formation strength ∈ [0,1] → mark count 1..5; read only for Formation.</param>
public readonly record struct GlyphSpec(
    GlyphBase Base,
    GlyphState State,
    SizeClass Size,
    GlyphDomain Domain = GlyphDomain.None,
    EraRegister Era = EraRegister.Primitive,
    double Maturity = 1.0,
    double Progress = 0.0,
    Placement Placement = Placement.Panel,
    Veterancy Veterancy = Veterancy.Recruit,
    double Strength = 1.0)
{
    /// <summary>The glyph box in pixels.</summary>
    public int Pixels => (int)Size;

    /// <summary>
    /// THE LOD CONTRACT, AS CODE (§2.7): detail is ADDED with size, never lost to
    /// crowding. At 16 px only the silhouette and the state ring are drawn; the
    /// domain mark arrives at 24, the register vocabulary and the full wash at 32,
    /// badges at 48. The composer bakes the NORMALIZED spec, so a glyph at size N
    /// is byte-identical to the same spec with its suppressed axes reset — which is
    /// what GlyphGrammarTests proves instead of eyeballing it.
    /// </summary>
    public GlyphSpec Normalize()
    {
        double maturity = Clamp01(Maturity);
        double progress = Clamp01(Progress);
        double strength = Clamp01(Strength);
        GlyphDomain domain = Domain;
        EraRegister era = Era;
        Veterancy vet = Veterancy;

        switch (Size)
        {
            case SizeClass.Px16:
                domain = GlyphDomain.None;
                era = EraRegister.Primitive;
                vet = Veterancy.Recruit;
                maturity = maturity >= 0.5 ? 1.0 : 0.0;          // binary wash
                break;
            case SizeClass.Px24:
                era = EraRegister.Primitive;
                vet = Veterancy.Recruit;
                maturity = System.Math.Floor(maturity * 4.0) / 4.0; // quarter steps
                if (Maturity >= 1.0) maturity = 1.0;
                break;
            case SizeClass.Px32:
                vet = Veterancy.Recruit;
                break;
            case SizeClass.Px48:
                break;
        }

        // Axes that are meaningless for a base are neutralised too, so two specs
        // that DRAW the same bake the same.
        if (Base != GlyphBase.Formation) { vet = Veterancy.Recruit; strength = 1.0; }
        if (State != GlyphState.InProgress && State != GlyphState.Stalled) progress = 0.0;

        return this with
        {
            Domain = domain, Era = era, Veterancy = vet,
            Maturity = maturity, Progress = progress, Strength = strength,
        };
    }

    /// <summary>The representative spec for a (base, state) pair — what the contact
    /// sheet shows and what the distinguishability test compares. InProgress and
    /// Stalled carry a half-full arc so their signature is the arc, not its absence.</summary>
    public static GlyphSpec Exemplar(GlyphBase b, GlyphState s, SizeClass size) =>
        new(b, s, size,
            Progress: s is GlyphState.InProgress or GlyphState.Stalled ? 0.5 : 0.0,
            Maturity: s == GlyphState.InProgress ? 0.5 : 1.0);

    internal static double Clamp01(double v) =>
        double.IsNaN(v) ? 0.0 : v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
}

/// <summary>
/// THE ONE GLOBAL LIGHT (D-038 B1: "a single global light direction, stated once
/// as a constant and never varied per asset, with a contact shadow"). Light from
/// the upper-left; the contact shadow falls toward the lower-right. Declared HERE
/// so that the future parametric-render parts (D-038 Part H) bind to the same
/// value instead of declaring a twin — a second light is Part H's collage failure.
/// </summary>
public static class ObjectLight
{
    /// <summary>Unit direction the shadow is cast along (screen space, y down).</summary>
    public static readonly (double X, double Y) ShadowDirection = (0.7071067811865476, 0.7071067811865476);

    /// <summary>Contact-shadow offset in whole pixels for a size class: 1 px at 16,
    /// scaling with the box. Both components are equal by construction of the
    /// direction, and the test pins that every Map glyph uses exactly this.</summary>
    public static (int Dx, int Dy) ShadowOffset(SizeClass size)
    {
        int px = System.Math.Max(1, (int)size / 16);
        return (px, px);
    }

    /// <summary>Shadow ink alpha (InkPrimary at 25 %).</summary>
    public const double ShadowAlpha = 0.25;
}
