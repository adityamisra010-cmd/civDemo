using System.Globalization;
using System.Text.Json;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.World;

/// <summary>One population class of <c>settlement-classes.json</c>: from <see cref="MinPopulation"/> people up, the World-zoom
/// mark radius (screen px) and the footprint radius (lattice units) the class's lower edge is drawn at.</summary>
public sealed record SettlementSizeClass(long MinPopulation, double WorldMarkRadiusPx, double FootprintRadiusUnits);

/// <summary>One fixed ring of glyph lots: its radius (lattice units) and its lot count. Lot k sits at (k + 0.5) × 360 / Lots
/// degrees clockwise from 12 o'clock.</summary>
public sealed record SlotRing(double Radius, int Lots);

/// <summary>
/// P12a — THE FIXED COMPOSITION LATTICE of a settlement, in lattice UNITS about its centre (the block pitch is one unit;
/// <c>settlement-classes.json</c> <c>lattice</c>). Nothing here depends on a settlement's population, its structures or
/// its institutions — a lot is where it is for ever — and the screen size of a unit depends on the ZOOM only
/// (<see cref="SettlementClasses.UnitPx"/>).
/// </summary>
public sealed record CompositionLattice(
    double WorldPxPerUnit, double MinUnitPxRegional, double MinUnitPxSettlement, double PlazaRadiusUnits,
    double GlyphUnits, IReadOnlyList<SlotRing> SlotRings,
    double BlockUnits, double BlockFirstRadius, double BlockRingPitch, double BlockLotPitch, int BlockRings,
    long DwellingsPerBlock)
{
    /// <summary>A glyph of size g (<see cref="WorldLens"/>'s StructureGlyph / UniversityGlyph) is drawn inside the square of
    /// half-side 0.95 g about its centre (the hall's pediment reaches 0.95 g up, every body less), before its stroke.</summary>
    public const double GlyphHalfBox = 0.95;

    /// <summary>The allowance the lattice keeps for a glyph's stroke and the era's wobble outside its box, in units: two px
    /// at the smallest unit the lens draws (<see cref="MinUnitPxRegional"/>, 4 px) — validated against the smallest
    /// footprint, so a glyph's stroke never crosses the footprint's edge.</summary>
    public const double StrokeAllowanceUnits = 0.5;

    /// <summary>The side of the square a glyph lot occupies (units): blocks under it are hidden where they stand.</summary>
    public double GlyphBoxUnits => 2 * GlyphHalfBox * GlyphUnits;

    /// <summary>A dwelling block is drawn BlockUnits wide and 0.8 × BlockUnits tall, centred on its lot.</summary>
    public double BlockHeightUnits => BlockUnits * 0.8;

    /// <summary>How far a block reaches from its lot's centre (its half-diagonal, units).</summary>
    public double BlockReach => Math.Sqrt(BlockUnits * BlockUnits + BlockHeightUnits * BlockHeightUnits) / 2;

    /// <summary>How far a glyph's box (with its stroke allowance) reaches from its lot's centre (units).</summary>
    public double GlyphReach => GlyphHalfBox * GlyphUnits * Math.Sqrt(2) + StrokeAllowanceUnits;

    /// <summary>The radius of block ring <paramref name="k"/> (units).</summary>
    public double BlockRingRadius(int k) => BlockFirstRadius + k * BlockRingPitch;

    /// <summary>The lots on block ring <paramref name="k"/>: as many as fit at <see cref="BlockLotPitch"/> round it.</summary>
    public int BlockLots(int k) => (int)Math.Floor(2 * Math.PI * BlockRingRadius(k) / BlockLotPitch);

    /// <summary>Every glyph lot of the lattice.</summary>
    public int SlotCount
    {
        get { int n = 0; foreach (SlotRing r in SlotRings) n += r.Lots; return n; }
    }
}

/// <summary>
/// P12a (M5 map workstream; D-043 D1) — THE SETTLEMENT VISUAL CLASSES: a settlement's visual scale and class come
/// primarily from its POPULATION, on a COMPRESSIVE scale with data-defined thresholds (<c>Sim.Ui/UiContent/world/
/// settlement-classes.json</c>, embedded as <see cref="ResourceName"/>). Between two thresholds the size index is linear in
/// ln(1 + population), so every class spans the same visual step though its population span grows; the World mark radius
/// and the footprint radius are interpolated at that index — continuous, STRICTLY increasing in population up to
/// <see cref="SaturationPopulation"/> (the last threshold) and constant above it. View data only: the simulation reads
/// none of it.
/// </summary>
public sealed class SettlementClasses
{
    /// <summary>The embedded resource's logical name (Sim.Ui.csproj).</summary>
    public const string ResourceName = "Sim.Ui.settlement-classes.json";

    public IReadOnlyList<SettlementSizeClass> Classes { get; }
    public CompositionLattice Lattice { get; }

    /// <summary>M6 E1.2: the part conventions — D-043 D2's count stages and D-066 A4's parts limit (<c>parts</c>).</summary>
    public PartConventions Parts { get; }

    private SettlementClasses(IReadOnlyList<SettlementSizeClass> classes, CompositionLattice lattice, PartConventions parts)
    {
        Classes = classes;
        Lattice = lattice;
        Parts = parts;
    }

    private static readonly Lazy<SettlementClasses> Shipped = new(() =>
    {
        using Stream stream = typeof(SettlementClasses).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("Sim.Ui is missing its embedded " + ResourceName);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    });

    /// <summary>The shipped table.</summary>
    public static SettlementClasses Default => Shipped.Value;

    /// <summary>The population at and above which the scale saturates: the last class's threshold.</summary>
    public long SaturationPopulation => Classes[^1].MinPopulation;

    /// <summary>The class a population belongs to (0-based): the last whose threshold it reaches. Integer comparison only.</summary>
    public int ClassOf(long population)
    {
        long p = Math.Max(0L, population);
        int i = 0;
        while (i + 1 < Classes.Count && Classes[i + 1].MinPopulation <= p) i++;
        return i;
    }

    /// <summary>The continuous size index: <see cref="ClassOf"/> plus the fraction of the way to the next threshold in
    /// ln(1 + population); the last class's index (no fraction) at and above <see cref="SaturationPopulation"/>.</summary>
    public double SizeIndex(long population)
    {
        long p = Math.Max(0L, population);
        int i = ClassOf(p);
        if (i >= Classes.Count - 1) return Classes.Count - 1;
        double x0 = Math.Log(1.0 + Classes[i].MinPopulation), x1 = Math.Log(1.0 + Classes[i + 1].MinPopulation);
        return i + (Math.Log(1.0 + p) - x0) / (x1 - x0);
    }

    /// <summary>The World-zoom mark radius (screen px) of a settlement of <paramref name="population"/>.</summary>
    public double WorldMarkRadiusPx(long population) => At(SizeIndex(population), static c => c.WorldMarkRadiusPx);

    /// <summary>The footprint radius (lattice units) of a settlement of <paramref name="population"/>.</summary>
    public double FootprintRadiusUnits(long population) => At(SizeIndex(population), static c => c.FootprintRadiusUnits);

    /// <summary>Screen px per lattice unit at <paramref name="scale"/> screen px per world px and the Settlement cross-fade
    /// <paramref name="settlementBlend"/> (<see cref="MapDetail.Settlement"/>): the world's own scale over
    /// <see cref="CompositionLattice.WorldPxPerUnit"/>, floored by the band's minimum (Regional's blended to Settlement's
    /// across the window — continuous). A function of the zoom only.</summary>
    public double UnitPx(double scale, double settlementBlend) =>
        Math.Max(scale / Lattice.WorldPxPerUnit, MapDetail.Mix(Lattice.MinUnitPxRegional, Lattice.MinUnitPxSettlement, settlementBlend));

    private double At(double index, Func<SettlementSizeClass, double> value)
    {
        int i = (int)Math.Floor(index);
        if (i >= Classes.Count - 1) return value(Classes[^1]);
        double t = index - i;
        double a = value(Classes[i]), b = value(Classes[i + 1]);
        return a + (b - a) * t;
    }

    // ------------------------------------------------------------------ loading and validation

    /// <summary>Parses and validates a <c>settlement-classes.json</c> document. A malformed table throws
    /// <see cref="InvalidDataException"/> naming the offending field — never a silent default.</summary>
    public static SettlementClasses Parse(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        JsonElement arr = Req(root, "sizeClasses", "");
        if (arr.ValueKind != JsonValueKind.Array) throw Bad("sizeClasses must be an array");
        var classes = new List<SettlementSizeClass>();
        int n = 0;
        foreach (JsonElement c in arr.EnumerateArray())
        {
            string at = "sizeClasses[" + n.ToString(CultureInfo.InvariantCulture) + "].";
            classes.Add(new SettlementSizeClass(Req(c, "minPopulation", at).GetInt64(),
                Req(c, "worldMarkRadiusPx", at).GetDouble(), Req(c, "footprintRadiusUnits", at).GetDouble()));
            n++;
        }
        if (classes.Count < 2) throw Bad("sizeClasses needs at least two classes (a scale and its saturation)");
        if (classes[0].MinPopulation != 0) throw Bad("sizeClasses[0].minPopulation must be 0");
        for (int i = 0; i < classes.Count; i++)
        {
            string at = "sizeClasses[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            if (!(classes[i].WorldMarkRadiusPx > 0) || !(classes[i].FootprintRadiusUnits > 0)) throw Bad(at + ": sizes must be positive");
            if (i == 0) continue;
            if (classes[i].MinPopulation <= classes[i - 1].MinPopulation) throw Bad(at + ".minPopulation must exceed the previous class's");
            if (!(classes[i].WorldMarkRadiusPx > classes[i - 1].WorldMarkRadiusPx)) throw Bad(at + ".worldMarkRadiusPx must exceed the previous class's (strictly monotone)");
            if (!(classes[i].FootprintRadiusUnits > classes[i - 1].FootprintRadiusUnits)) throw Bad(at + ".footprintRadiusUnits must exceed the previous class's (strictly monotone)");
        }

        JsonElement l = Req(root, "lattice", "");
        var rings = new List<SlotRing>();
        JsonElement ra = Req(l, "slotRings", "lattice.");
        int k = 0;
        foreach (JsonElement r in ra.EnumerateArray())
        {
            string at = "lattice.slotRings[" + k.ToString(CultureInfo.InvariantCulture) + "].";
            rings.Add(new SlotRing(Req(r, "radius", at).GetDouble(), Req(r, "lots", at).GetInt32()));
            k++;
        }
        var lattice = new CompositionLattice(
            Req(l, "worldPxPerUnit", "lattice.").GetDouble(), Req(l, "minUnitPxRegional", "lattice.").GetDouble(),
            Req(l, "minUnitPxSettlement", "lattice.").GetDouble(), Req(l, "plazaRadiusUnits", "lattice.").GetDouble(),
            Req(l, "glyphUnits", "lattice.").GetDouble(), rings,
            Req(l, "blockUnits", "lattice.").GetDouble(), Req(l, "blockFirstRadius", "lattice.").GetDouble(),
            Req(l, "blockRingPitch", "lattice.").GetDouble(), Req(l, "blockLotPitch", "lattice.").GetDouble(),
            Req(l, "blockRings", "lattice.").GetInt32(), Req(l, "dwellingsPerBlock", "lattice.").GetInt64());
        Validate(lattice, classes);
        PartConventions parts = PartConventions.Parse(Req(root, "parts", ""));
        if (parts.MaxParts > lattice.SlotCount)
            throw Bad("parts.maxParts (" + parts.MaxParts.ToString(CultureInfo.InvariantCulture) + ") exceeds the lattice's glyph lots ("
                + lattice.SlotCount.ToString(CultureInfo.InvariantCulture) + ")");
        return new SettlementClasses(classes, lattice, parts);
    }

    private static void Validate(CompositionLattice l, List<SettlementSizeClass> classes)
    {
        if (!(l.WorldPxPerUnit > 0) || !(l.MinUnitPxRegional > 0) || !(l.MinUnitPxSettlement > 0)) throw Bad("lattice: the unit scales must be positive");
        if (!(l.GlyphUnits > 0) || !(l.BlockUnits > 0) || !(l.BlockRingPitch > 0) || !(l.BlockLotPitch > 0)) throw Bad("lattice: sizes and pitches must be positive");
        if (l.DwellingsPerBlock <= 0) throw Bad("lattice.dwellingsPerBlock must be positive");
        if (l.SlotRings.Count == 0) throw Bad("lattice.slotRings must name at least one ring");
        if (l.BlockRings <= 0) throw Bad("lattice.blockRings must be positive");
        double smallest = classes[0].FootprintRadiusUnits;
        double previous = 0;
        for (int i = 0; i < l.SlotRings.Count; i++)
        {
            SlotRing r = l.SlotRings[i];
            string at = "lattice.slotRings[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            if (r.Lots <= 0 || !(r.Radius > previous)) throw Bad(at + ": rings run outward with at least one lot");
            if (r.Radius + l.GlyphReach > smallest)
                throw Bad(at + ": a glyph on this ring would cross the SMALLEST footprint's edge (radius " + Num(r.Radius) + " + reach " + Num(l.GlyphReach) + " > " + Num(smallest) + ")");
            previous = r.Radius;
        }
        if (l.BlockFirstRadius - l.BlockReach < l.PlazaRadiusUnits) throw Bad("lattice.blockFirstRadius: the first block ring would lie under the centre plaza");
        double last = l.BlockRingRadius(l.BlockRings - 1) + l.BlockReach;
        if (last < classes[^1].FootprintRadiusUnits)
            throw Bad("lattice.blockRings: the last ring (reach " + Num(last) + ") does not reach the saturated footprint (" + Num(classes[^1].FootprintRadiusUnits) + ") - a block cap below the saturation");
    }

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static JsonElement Req(JsonElement e, string name, string at) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) ? v : throw Bad(at + name + " is missing");

    private static InvalidDataException Bad(string what) => new("settlement-classes.json: " + what);
}

/// <summary>One lot of the composition lattice: its ring and index, and its centre in lattice units about the settlement's
/// centre (x right, y down).</summary>
public readonly record struct CompositionLot(int Ring, int Index, double X, double Y);

/// <summary>One drawn part of a settlement's composition: its key (<c>"structure:{projectId}"</c> or
/// <c>"institution:{typeKey}"</c>, the <see cref="InstitutionMarker"/> keys) and the lot it holds. <see cref="Spilled"/>
/// marks a part placed after every lot was claimed (on the outermost glyph ring, between lots) — unreachable with the
/// shipped content, whose every kind has a lot.</summary>
public sealed record PlacedPart(string Key, CompositionLot Lot, bool Spilled);

/// <summary>
/// P12a — A SETTLEMENT'S COMPOSITION, a pure function of its projected state and the content: its size class and index
/// (D-043 D1, population-primary), its footprint radius in lattice units, the lot of each part it holds (one per
/// structure kind and per institution type — aggregated, D-043 D2) and the lots of its dwelling blocks.
/// <see cref="BlocksWanted"/> is the dwellings' block count (one per <see cref="CompositionLattice.DwellingsPerBlock"/>);
/// <see cref="Blocks"/> holds fewer only when the footprint's open lots (<see cref="OpenBlockLots"/>, less those under a
/// glyph) are all built — a full footprint.
/// </summary>
public sealed record SettlementComposition(
    int Settlement, int SizeClass, double SizeIndex, double FootprintUnits,
    IReadOnlyList<PlacedPart> Parts, IReadOnlyList<CompositionLot> Blocks, long BlocksWanted, int OpenBlockLots)
{
    /// <summary>M6 E1.2 (D-066 A4): the lot the OVERFLOW part stands on when the settlement holds more kinds than the
    /// composite shows — its own claim, after every kind's (so no kind's lot moves for it).</summary>
    public CompositionLot OverflowLot { get; init; }

    /// <summary>M6 E1.2: the parts UNDER CONSTRUCTION at the settlement (<see cref="SettlementLensView.Building"/>), each on
    /// its kind's lot — the lot the built part will take — key ascending. Not in <see cref="Parts"/>: only what is built is
    /// a part (P12a), so the composition digest of a settlement with nothing queued is unchanged.</summary>
    public IReadOnlyList<PlacedPart> Scaffolds { get; init; } = [];
}

/// <summary>
/// The kinds of part a settlement can hold, from CONTENT: every construction project drawn as a structure (goods.json
/// projects that found no institution — ADR-033 D6 draws a university once, as its institution), ascending id, and every
/// institution type (research.json <c>universityTypes</c> and sim.json <c>institutions.types</c>, ADR-057), ascending key. Every settlement reserves a lot for each of
/// them, whether it holds one or not, so what a settlement holds never decides where anything else stands.
/// </summary>
public sealed record PartUniverse(IReadOnlyList<int> StructureProjects, IReadOnlyList<int> InstitutionTypes)
{
    public static readonly PartUniverse Empty = new([], []);

    public static PartUniverse Of(SimConfig cfg)
    {
        var projects = new List<int>();
        foreach (ConstructionProjectEntry p in cfg.Goods?.Projects ?? [])
            if (!InstitutionContent.FoundsInstitution(p) && !projects.Contains(p.Id)) projects.Add(p.Id);
        projects.Sort();
        var types = new List<int>();
        foreach (InstitutionTypeInfo u in InstitutionContent.Types(cfg))   // ADR-057: every content-driven institution type
            if (!types.Contains(u.Key)) types.Add(u.Key);
        types.Sort();
        return new PartUniverse(projects, types);
    }
}

/// <summary>One kind of part that claims a lot: a structure kind (<see cref="Category"/> 0, <see cref="Key"/> the project
/// id) or an institution type (1, the type key).</summary>
public readonly record struct PartClaim(int Category, int Key)
{
    /// <summary>M6 E1.2: <see cref="Overflow"/> is the overflow part's claim (D-066 A4), key 0.</summary>
    public const int Structure = 0, Institution = 1, Overflow = 2;

    /// <summary>The claim's key — the string its preferred lots hash from, and the <see cref="InstitutionMarker"/> key.</summary>
    public string HashKey => Category switch
    {
        Structure => "structure:",
        Institution => "institution:",
        _ => "overflow:",
    } + Key.ToString(CultureInfo.InvariantCulture);

    /// <summary>The claim of a part key ("structure:{id}" or "institution:{key}").</summary>
    public static bool TryParse(string key, out PartClaim claim)
    {
        claim = default;
        int colon = key.IndexOf(':');
        if (colon < 0 || !int.TryParse(key.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int k)) return false;
        string head = key[..colon];
        if (head == "structure") { claim = new PartClaim(Structure, k); return true; }
        if (head == "institution") { claim = new PartClaim(Institution, k); return true; }
        return false;
    }
}

/// <summary>
/// P12a — THE COMPOSER: stable slot placement for a settlement's parts and its dwelling blocks. The algorithm is ported
/// from origin/claude/civdemo-work-b1z2y4 (<c>Composition.AssignSlots</c> / <c>VisibleBlocks</c>), not its content:
/// <list type="number">
/// <item>CLAIMS: every kind of the <see cref="PartUniverse"/> (tier 0), then any kind the settlement holds that content does
///   not name (tier 1 — unreachable with the shipped content). PRIORITY is the integer composite (tier, category, key):
///   unique, stable, independent of any table's order.</item>
/// <item>SLOTS: in priority order, each claim's preferred lot on each glyph ring is <see cref="StableHash"/>(settlement key,
///   claim key) mod the ring's lots; rings are tried inner to outer, each probed from the preferred lot upward (wrapping),
///   every lot at most once — bounded. A claim finding no free lot SPILLS onto the outermost ring between lots.</item>
/// <item>BLOCKS: the first <see cref="SettlementComposition.BlocksWanted"/> block lots in a FIXED order — rings inner to
///   outer, each in an even coprime-step order from a per-settlement rotation — on the rings the footprint holds, skipping
///   any lot under a held part's glyph (hidden where it stands; the next lot in the order is shown instead).</item>
/// </list>
/// WHY THE UNIVERSE AND NOT THE HELD SET (the plan's claim order, corrected against the acceptance): a structure kind has no
/// establishment turn in state (<c>StructureRow</c> is settlement, project, count) and its row position is table order, so
/// a priority over the HELD kinds cannot both ignore table order and keep a later-built kind with a lower key from taking
/// an earlier kind's lot. Over the universe a kind's lot depends only on the settlement and the content: arrival, removal,
/// counts, zoom and pan move nothing.
/// Pure: no state, no cache; integer and fixed-arithmetic decisions only (trig makes final coordinates, never a choice).
/// </summary>
public static class SettlementComposer
{
    /// <summary>The settlement's hash key.</summary>
    public static string SettlementKey(int settlement) => "settlement:" + settlement.ToString(CultureInfo.InvariantCulture);

    /// <summary>Every glyph lot of the lattice, ring-major (ring 0 first), index ascending within a ring.</summary>
    public static CompositionLot[] SlotLots(CompositionLattice lattice)
    {
        var lots = new CompositionLot[lattice.SlotCount];
        int at = 0;
        for (int r = 0; r < lattice.SlotRings.Count; r++)
        {
            SlotRing ring = lattice.SlotRings[r];
            for (int k = 0; k < ring.Lots; k++) lots[at++] = Lot(r, k, ring.Radius, (k + 0.5) * 360.0 / ring.Lots);
        }
        return lots;
    }

    /// <summary>The claims' priority order (indices into <paramref name="claims"/>): ascending (tier, category, key) — an
    /// integer composite key, unique per distinct claim, so the order is total whatever the input order.</summary>
    public static int[] PriorityOrder(IReadOnlyList<(int Tier, PartClaim Claim)> claims)
    {
        var order = new int[claims.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            int c = claims[a].Tier.CompareTo(claims[b].Tier);
            if (c != 0) return c;
            c = claims[a].Claim.Category.CompareTo(claims[b].Claim.Category);
            if (c != 0) return c;
            c = claims[a].Claim.Key.CompareTo(claims[b].Claim.Key);
            return c != 0 ? c : a.CompareTo(b);   // equal claims (duplicates): input position, and both get one lot below
        });
        return order;
    }

    /// <summary>The lot of every claim, index-aligned with <paramref name="claims"/>; <paramref name="spilled"/> marks those
    /// placed after every lot was taken. Duplicate claims share the first one's lot.</summary>
    public static CompositionLot[] AssignSlots(int settlement, IReadOnlyList<(int Tier, PartClaim Claim)> claims,
        CompositionLattice lattice, out bool[] spilled)
    {
        CompositionLot[] lots = SlotLots(lattice);
        var used = new bool[lots.Length];
        var result = new CompositionLot[claims.Count];
        spilled = new bool[claims.Count];
        var done = new List<(PartClaim Claim, int Index)>();
        string key = SettlementKey(settlement);
        int spill = 0;
        foreach (int i in PriorityOrder(claims))
        {
            int dup = -1;
            foreach ((PartClaim c, int at) in done) if (c == claims[i].Claim) { dup = at; break; }
            if (dup >= 0) { result[i] = result[dup]; spilled[i] = spilled[dup]; continue; }
            done.Add((claims[i].Claim, i));
            ulong h = StableHash.Of(key, claims[i].Claim.HashKey);
            int found = -1, ringStart = 0;
            for (int r = 0; r < lattice.SlotRings.Count && found < 0; r++)
            {
                int n = lattice.SlotRings[r].Lots;
                int start = StableHash.Index(h, n);
                for (int p = 0; p < n; p++)
                {
                    int idx = ringStart + (start + p) % n;
                    if (!used[idx]) { found = idx; break; }
                }
                ringStart += n;
            }
            if (found >= 0) { used[found] = true; result[i] = lots[found]; }
            else { result[i] = Spill(lattice, spill++); spilled[i] = true; }
        }
        return result;
    }

    /// <summary>The k-th spill position: on the outermost glyph ring, at the boundary between two lots (wrapping).</summary>
    public static CompositionLot Spill(CompositionLattice lattice, int k)
    {
        int r = lattice.SlotRings.Count - 1;
        SlotRing ring = lattice.SlotRings[r];
        int at = k % ring.Lots;
        return Lot(lattice.SlotRings.Count, k, ring.Radius, (at + 1.0) * 360.0 / ring.Lots);
    }

    /// <summary>The fill step for a ring of n lots: the smallest step ≥ round(0.382·n) coprime with n, so a part-filled ring
    /// is filled evenly round, not one side first (b1z2y4's <c>FillStep</c>).</summary>
    public static int FillStep(int n)
    {
        if (n <= 2) return 1;
        int s = Math.Max(1, (int)Math.Round(n * 0.382, MidpointRounding.AwayFromZero));
        while (Gcd(s, n) != 1) s++;
        return s;
    }

    private static int Gcd(int a, int b) { while (b != 0) (a, b) = (b, a % b); return a; }

    /// <summary>Block ring <paramref name="ring"/>'s rotation for a settlement, in whole tenths of a degree from the stable
    /// hash — settlements are not stamped from one die; a settlement's rotation never changes.</summary>
    public static double RingRotationDeg(int settlement, int ring) =>
        StableHash.Index(StableHash.Of(SettlementKey(settlement), "block-ring-" + ring.ToString(CultureInfo.InvariantCulture)), 3600) / 10.0;

    /// <summary>Whether block ring <paramref name="ring"/> lies inside a footprint of <paramref name="footprintUnits"/>.</summary>
    public static bool RingOpen(CompositionLattice lattice, int ring, double footprintUnits) =>
        lattice.BlockRingRadius(ring) + lattice.BlockReach <= footprintUnits;

    /// <summary>The block lots in their FIXED order on the rings a footprint of <paramref name="footprintUnits"/> holds (no
    /// glyph considered): the order <see cref="VisibleBlocks"/> fills.</summary>
    public static List<CompositionLot> BlockOrder(int settlement, double footprintUnits, CompositionLattice lattice)
    {
        var lots = new List<CompositionLot>();
        for (int r = 0; r < lattice.BlockRings && RingOpen(lattice, r, footprintUnits); r++)
        {
            int n = lattice.BlockLots(r);
            int step = FillStep(n);
            double rot = RingRotationDeg(settlement, r), radius = lattice.BlockRingRadius(r);
            for (int j = 0; j < n; j++)
            {
                int k = (int)((long)j * step % n);
                lots.Add(Lot(r, k, radius, (k + 0.5) * 360.0 / n + rot));
            }
        }
        return lots;
    }

    /// <summary>Whether a block on <paramref name="lot"/> lies under the glyph of a part on <paramref name="part"/>.</summary>
    public static bool Covers(CompositionLattice lattice, CompositionLot part, CompositionLot lot) =>
        Math.Abs(lot.X - part.X) < (lattice.BlockUnits + lattice.GlyphBoxUnits) / 2
        && Math.Abs(lot.Y - part.Y) < (lattice.BlockHeightUnits + lattice.GlyphBoxUnits) / 2;

    /// <summary>The first <paramref name="wanted"/> block lots of <see cref="BlockOrder"/> that no held part covers.</summary>
    public static List<CompositionLot> VisibleBlocks(int settlement, long wanted, IReadOnlyList<PlacedPart> parts,
        double footprintUnits, CompositionLattice lattice, out int open)
    {
        List<CompositionLot> order = BlockOrder(settlement, footprintUnits, lattice);
        open = order.Count;
        var shown = new List<CompositionLot>();
        foreach (CompositionLot lot in order)
        {
            if (shown.Count >= wanted) break;
            bool covered = false;
            foreach (PlacedPart p in parts) if (Covers(lattice, p.Lot, lot)) { covered = true; break; }
            if (!covered) shown.Add(lot);
        }
        return shown;
    }

    /// <summary>
    /// The composition of <paramref name="s"/>: its size from its population (<paramref name="classes"/>), the lot of every
    /// part it holds (its structures in the projection's order, then its institution types) from the universe's slot
    /// assignment, and its dwelling blocks. Pure.
    /// </summary>
    public static SettlementComposition Compose(SettlementLensView s, PartUniverse universe, SettlementClasses classes)
    {
        CompositionLattice lattice = classes.Lattice;
        var claims = new List<(int Tier, PartClaim Claim)>();
        foreach (int id in universe.StructureProjects) claims.Add((0, new PartClaim(PartClaim.Structure, id)));
        foreach (int key in universe.InstitutionTypes) claims.Add((0, new PartClaim(PartClaim.Institution, key)));
        var held = new List<PartClaim>();
        foreach (StructureView st in s.Structures) held.Add(new PartClaim(PartClaim.Structure, st.ProjectId));
        foreach (InstitutionView iv in s.Institutions) held.Add(new PartClaim(PartClaim.Institution, iv.TypeKey));
        foreach (PartClaim c in held)
        {
            bool known = false;
            foreach ((int _, PartClaim u) in claims) if (u == c) { known = true; break; }
            if (!known) claims.Add((1, c));
        }
        // M6 E1.2: the overflow part's own claim (tier 2) and any kind under construction that no claim names yet (tier 3)
        // — both AFTER every claim above in the priority order, so no kind's lot moves for them.
        int overflowAt = claims.Count;
        claims.Add((2, new PartClaim(PartClaim.Overflow, 0)));
        var building = new List<PartClaim>();
        foreach (string key in s.Building ?? [])
            if (PartClaim.TryParse(key, out PartClaim b) && !building.Contains(b)) building.Add(b);
        building.Sort(static (a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : a.Key.CompareTo(b.Key));
        foreach (PartClaim c in building)
        {
            bool known = false;
            foreach ((int _, PartClaim u) in claims) if (u == c) { known = true; break; }
            if (!known) claims.Add((3, c));
        }
        CompositionLot[] lots = AssignSlots(s.Id, claims, lattice, out bool[] spilled);
        var parts = new List<PlacedPart>(held.Count);
        foreach (PartClaim c in held)
            for (int i = 0; i < claims.Count; i++)
                if (claims[i].Claim == c) { parts.Add(new PlacedPart(c.HashKey, lots[i], spilled[i])); break; }

        double footprint = classes.FootprintRadiusUnits(s.Population);
        long wanted = Math.Max(0L, s.Dwellings) / lattice.DwellingsPerBlock;
        List<CompositionLot> blocks = VisibleBlocks(s.Id, wanted, parts, footprint, lattice, out int open);
        var scaffolds = new List<PlacedPart>(building.Count);
        foreach (PartClaim c in building)
            for (int i = 0; i < claims.Count; i++)
                if (claims[i].Claim == c) { scaffolds.Add(new PlacedPart(c.HashKey, lots[i], spilled[i])); break; }
        return new SettlementComposition(s.Id, classes.ClassOf(s.Population), classes.SizeIndex(s.Population), footprint,
            parts, blocks, wanted, open) { OverflowLot = lots[overflowAt], Scaffolds = scaffolds };
    }

    private static CompositionLot Lot(int ring, int index, double radius, double deg)
    {
        double a = deg * Math.PI / 180.0;
        return new CompositionLot(ring, index, radius * Math.Sin(a), -radius * Math.Cos(a));
    }
}
