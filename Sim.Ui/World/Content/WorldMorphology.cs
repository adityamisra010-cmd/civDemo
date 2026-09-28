using Sim.Ui.Art.Glyphs;

namespace Sim.Ui.World.Content;

// THE VISUAL MORPHOLOGY — the validated, immutable form of ui-content/world/morphology.json
// (docs/architecture/world-visualization.md §5). Every number here is a VIEW choice: how a
// reported value is DRAWN. None is a gameplay threshold, and the simulation reads none of
// them (Sim.Ui is outside the simulation; nothing references it).

/// <summary>A glyph-grammar icon: a silhouette base and an object mark.</summary>
public sealed record WorldGlyph(GlyphBase Base, GlyphDomain Mark);

/// <summary>How big a settlement's composed sprite is on screen: pixels per sprite unit =
/// clamp(camera px-per-world-unit × <see cref="WorldPerUnit"/>, min, max). The clamp is what
/// keeps a sprite from implying land it does not occupy when zoomed in.</summary>
public sealed record SpriteScale(double WorldPerUnit, double MinPxPerUnit, double MaxPxPerUnit)
{
    public double PxPerUnit(double cameraPxPerWorldUnit)
    {
        double v = cameraPxPerWorldUnit * WorldPerUnit;
        return double.IsNaN(v) ? MinPxPerUnit : Math.Clamp(v, MinPxPerUnit, MaxPxPerUnit);
    }
}

/// <summary>Level of detail — ADDED with size, never lost to crowding (the glyph LOD contract).</summary>
public enum WorldLod { Far = 0, Mid = 1, Near = 2 }

public sealed record LodThresholds(double MidFromPxPerUnit, double NearFromPxPerUnit)
{
    public WorldLod For(double pxPerUnit) =>
        pxPerUnit >= NearFromPxPerUnit ? WorldLod.Near : pxPerUnit >= MidFromPxPerUnit ? WorldLod.Mid : WorldLod.Far;
}

/// <summary>One ring of a composition lattice: its radius (sprite units), how many lots, and
/// the angle of lot 0's leading edge (degrees clockwise from 12 o'clock). Lot k sits at
/// OffsetDeg + (k + 0.5) × 360 / Lots.</summary>
public sealed record LotRing(double Radius, int Lots, double OffsetDeg)
{
    public double AngleDeg(int k) => OffsetDeg + (k + 0.5) * 360.0 / Lots;
}

/// <summary>
/// The composed settlement sprite's FIXED lattices (D-038 H3/H4: one composed sprite, parts
/// assembled at draw time). Structure slots and residential block lots are both fixed rings
/// in sprite units; neither depends on the settlement's stage, population or structure
/// count, so growth never re-lays the sprite.
/// </summary>
public sealed record CompositionLayout(
    double PlazaRadius, double SlotSize, IReadOnlyList<LotRing> SlotRings,
    double BlockSize, IReadOnlyList<LotRing> BlockRings)
{
    public int SlotCount { get { int n = 0; foreach (LotRing r in SlotRings) n += r.Lots; return n; } }
    public int BlockCount { get { int n = 0; foreach (LotRing r in BlockRings) n += r.Lots; return n; } }
    public double OuterRadius => Math.Max(
        SlotRings.Count > 0 ? SlotRings[^1].Radius + SlotSize / 2 : 0,
        BlockRings.Count > 0 ? BlockRings[^1].Radius + BlockSize / 2 : 0);
}

/// <summary>Which reported input selects a settlement's visual stage, with its thresholds
/// (one per stage, first 0, strictly ascending). <see cref="Provenance"/> says whose the
/// thresholds are: "simulation" (the live SizeTier, D-017 — mapped one-to-one) or
/// "demonstration" (view-only demo thresholds). <see cref="NamedStages"/> false means the
/// stage is shown by number ("size tier 2 of 4"), never as Town/City.</summary>
public sealed record StageDriver(string Input, string Provenance, bool NamedStages, IReadOnlyList<double> Thresholds, string Note);

public sealed record SettlementStage(string Id, string Name, int Blocks);

public sealed record SettlementStages(IReadOnlyList<StageDriver> Drivers, IReadOnlyList<SettlementStage> Stages);

/// <summary>What selects a structure's or edge's visual stage: a numeric reported input
/// (<see cref="Input"/>; "multiplicity" is built in), or a categorical reported label mapped
/// through an ordinal table (<see cref="Label"/> + <see cref="Ordinals"/>).</summary>
public sealed record StageRule(string? Input, string? Label, IReadOnlyList<(string Value, double Score)> Ordinals)
{
    public string Describe => Input ?? ("label " + Label);
}

/// <summary>One building part in LOT-LOCAL units: the lot is the square [-0.5, 0.5]² of one
/// composition slot, so no stage can ever overflow into a neighbour.</summary>
public sealed record MorphPart(string Kind, double X, double Y, double W, double H);

/// <summary>A visual stage: it ADDS parts to every earlier stage's (cumulative — an
/// institution grows, it is never replaced by another icon).</summary>
public sealed record MorphStage(string Name, double Min, IReadOnlyList<MorphPart> Adds);

public sealed record VisualType(
    string Id, string Name, string Category, WorldGlyph Glyph, StageRule StageBy, IReadOnlyList<MorphStage> Stages, string Note)
{
    /// <summary>The part set of stage <paramref name="stage"/>: every earlier stage's adds, then its own.</summary>
    public IReadOnlyList<MorphPart> PartsAt(int stage)
    {
        var parts = new List<MorphPart>();
        for (int s = 0; s <= stage && s < Stages.Count; s++) parts.AddRange(Stages[s].Adds);
        return parts;
    }
}

public sealed record SpecializationMark(string Id, string Name, GlyphDomain Mark);

/// <summary>A reported operational state and the EXISTING glyph-grammar state it is drawn
/// with (one state alphabet, no second "under construction" treatment).</summary>
public sealed record StateStyle(string Id, string Name, GlyphState GlyphState);

public enum StrokeKind { Solid = 0, Dashed = 1, Dotted = 2 }

public sealed record InfraStage(string Name, double Min, double WidthPx, StrokeKind Stroke, string Ink);

public sealed record InfraType(string Id, string Name, StageRule StageBy, IReadOnlyList<InfraStage> Stages);

public sealed record NodeType(string Id, string Name, WorldGlyph? Glyph);

public sealed record ResourceType(string Id, string Name, GlyphDomain Mark);

/// <summary>A mobile-agent display type. Category picks the silhouette family: military →
/// a Standard (banner), person → an Emblem (the object mark alone), group → a Node (a
/// circled mark) — never a figure (D-038 C1/C3).</summary>
public sealed record AgentType(string Id, string Name, string Category, WorldGlyph Glyph, string? CountNoun);

/// <summary>A token-size band by reported count — a VISUAL scale, not strength.</summary>
public sealed record SizeBand(long Min, double Scale);

/// <summary>Maps a live M4-D construction project name onto a visual type.</summary>
public sealed record LiveProjectMap(string Project, string VisualType);

/// <summary>Visual aggregation (D-038 H8 is UNRULED: this is a provisional view default).
/// Per settlement and visual type, the first <see cref="IndividualUpTo"/> reports by
/// (established, key) are drawn individually; the rest share one cluster token.</summary>
public sealed record Aggregation(int IndividualUpTo, string Provisional);

public sealed record WorldMorphology(
    string Notice, SpriteScale Sprite, LodThresholds Lod, CompositionLayout Layout, SettlementStages Settlements,
    Aggregation Aggregation, IReadOnlyList<VisualType> VisualTypes, IReadOnlyList<SpecializationMark> Specializations,
    IReadOnlyList<StateStyle> States, IReadOnlyList<InfraType> InfraTypes, IReadOnlyList<NodeType> NodeTypes,
    IReadOnlyList<ResourceType> ResourceTypes, IReadOnlyList<AgentType> AgentTypes, IReadOnlyList<SizeBand> SizeBands,
    IReadOnlyList<string> PolityInks, IReadOnlyList<LiveProjectMap> LiveProjects)
{
    public VisualType? Visual(string id) => Find(VisualTypes, id, v => v.Id);
    public SpecializationMark? Specialization(string id) => Find(Specializations, id, v => v.Id);
    public StateStyle? State(string id) => Find(States, id, v => v.Id);
    public InfraType? Infra(string id) => Find(InfraTypes, id, v => v.Id);
    public NodeType? Node(string id) => Find(NodeTypes, id, v => v.Id);
    public ResourceType? Resource(string id) => Find(ResourceTypes, id, v => v.Id);
    public AgentType? Agent(string id) => Find(AgentTypes, id, v => v.Id);

    public string? LiveVisualType(string projectName)
    {
        foreach (LiveProjectMap m in LiveProjects) if (string.Equals(m.Project, projectName, StringComparison.Ordinal)) return m.VisualType;
        return null;
    }

    /// <summary>The ink token for a polity: depends on the polity's own seed only.</summary>
    public string PolityInk(long inkSeed)
    {
        if (PolityInks.Count == 0) return "InkSoft";
        long n = PolityInks.Count;
        long i = ((inkSeed % n) + n) % n;
        return PolityInks[(int)i];
    }

    public double SizeScale(long? count)
    {
        double scale = 1.0;
        if (count is not long c) return scale;
        foreach (SizeBand b in SizeBands) if (c >= b.Min) scale = b.Scale;
        return scale;
    }

    private static T? Find<T>(IReadOnlyList<T> list, string id, Func<T, string> key) where T : class
    {
        for (int i = 0; i < list.Count; i++) if (string.Equals(key(list[i]), id, StringComparison.Ordinal)) return list[i];
        return null;
    }
}
