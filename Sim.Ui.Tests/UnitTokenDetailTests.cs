using System.Globalization;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 map workstream P11 — THE FORMATION TOKEN AND ITS DETAIL WINDOW, under the Director's default for the
/// unit-representation issue: ONE non-anatomical emblem token per formation (D-038 C1), keyed by its CURRENT identity;
/// no count label, because <c>MilitaryUnitRow</c> holds no headcount (ledger §24's labels/counts have no composition to
/// show); a detail window of the row's fields and the unit-family content only, with each unsimulated figure named and
/// marked "not simulated", and movement a locked line (M7, CR-017). The tests:
/// <list type="bullet">
/// <item>TOKEN/ROW ACCOUNTING — the tokens drawn are exactly the rows the player knows, one each, at every zoom, over a
/// POPULATED world (twelve formations at one settlement, eight at one field position, one a hair from them, inserted in a
/// scrambled id order), and no token carries a number;</item>
/// <item>the emblem is keyed by the identity (injective over the whole content; a modernization re-keys it; nothing else
/// of the row moves it) and is a plate with strokes, never a figure (the anatomy fence), in every era's hand;</item>
/// <item>TIE-DENSE HIT TESTS — every row is picked at its own centre whatever the table order, and a distance tie goes to
/// the lower id in any order;</item>
/// <item>CARD FIELD PROVENANCE — each line of the card moves with exactly its own row field, and no line shows a
/// statistic the row does not hold;</item>
/// <item>the LEAK test with the P7 stub — a rival formation is drawn and selectable only where the player sees it, and
/// perturbing a hidden one (its identity, experience, army) leaves every token and card identical, with an all-known
/// control that sees the perturbation;</item>
/// <item>the real UI — the window draws every line and its only control is close.</item>
/// </list>
/// </summary>
[Collection("ImGui context")]
public class UnitTokenDetailTests : IDisposable
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-p11-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private string WorkDir(string name) => Path.Combine(_root, name);

    /// <summary>The one-AI canonical world at its founding: the player's Warband and the AI's, each at its capital.</summary>
    private static readonly Lazy<UiSession> Base = new(() => UiSession.Start(42, aiEmpiresOverride: 1),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static SimConfig Cfg => Base.Value.Config;

    private static UnitFamilyContent Families => Cfg.UnitFamilies!;

    private static string Name(int id) => Base.Value.Names.Name(id);

    private static PolityId Rival(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Owner.Value != Me.Value) return w.MilitaryUnits[i].Owner;
        throw new InvalidOperationException("the fixture has no rival formation");
    }

    /// <summary>Every identity of the content, in content order.</summary>
    private static List<UnitIdentity> Identities()
    {
        var all = new List<UnitIdentity>();
        foreach (UnitFamily f in Families.Families) all.AddRange(f.Line);
        return all;
    }

    /// <summary>
    /// THE POPULATED WORLD: the base world plus eleven more of the player's formations at its capital (twelve there), eight
    /// at one field position and one a quarter-px from them, each of a different identity (the content's, family set
    /// accordingly), inserted in a SCRAMBLED id order so the table is not in id order.
    /// </summary>
    private static WorldState Populated(out int[] stationed, out int[] field, out int nearly)
    {
        WorldState w = Base.Value.World.Clone();
        MilitaryUnitRow mine = MilitaryQuery.Units(w, Me)[0];
        int next = 0;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) next = Math.Max(next, w.MilitaryUnits[i].Id + 1);
        List<UnitIdentity> ids = Identities();
        int[] order = [7, 2, 9, 0, 5, 10, 1, 8, 3, 6, 4];
        var st = new List<int> { mine.Id };
        int k = 0;
        foreach (int o in order)
        {
            UnitIdentity ident = ids[(3 * o + 1) % ids.Count];
            w.MilitaryUnits.Add(mine with { Id = next + o, Family = ident.FamilyKey, Identity = ident.Key });
            st.Add(next + o);
            k++;
        }
        int fieldBase = next + 20;
        int[] fieldOrder = [5, 0, 7, 2, 4, 1, 6, 3];
        var fl = new List<int>();
        foreach (int o in fieldOrder)
        {
            UnitIdentity ident = ids[(5 * o + 2) % ids.Count];
            w.MilitaryUnits.Add(mine with { Id = fieldBase + o, Family = ident.FamilyKey, Identity = ident.Key,
                Location = new SettlementId(-1), X = 200.25, Y = 31.5 });
            fl.Add(fieldBase + o);
        }
        nearly = next + 40;
        w.MilitaryUnits.Add(mine with { Id = nearly, Location = new SettlementId(-1), X = 200.25, Y = 31.75 });
        stationed = [.. st];
        field = [.. fl];
        return w;
    }

    private static (DrawList D, LensFrame F) Paint(WorldProjection p, WorldZoom z, MapInk? ink = null, LegendAnchor? legend = null)
    {
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4, y * 4), 4,
            new RectD(0, 0, 4096, 4096), ink: ink, legend: legend);
        return (d, f);
    }

    private static WorldProjection With(WorldProjection p, IReadOnlyList<UnitLensView> units) => new()
    {
        WorldSize = p.WorldSize, KmPerPx = p.KmPerPx, Settlements = p.Settlements, Units = units, Roads = p.Roads,
        Routes = p.Routes, Territory = p.Territory, PlayerPolity = p.PlayerPolity, PlayerAge = p.PlayerAge,
        PlayerAgeName = p.PlayerAgeName, Absent = p.Absent,
    };

    private static readonly WorldZoom[] Zooms = [WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement];

    /// <summary>The commands the formation layer emitted: the lens draws formations after every other layer (the legend,
    /// absent here, comes last), so they are the tail past what the same projection without formations draws.</summary>
    private static List<DrawCmd> UnitLayer(WorldProjection p, WorldZoom z, MapInk? ink = null)
    {
        (DrawList without, _) = Paint(With(p, []), z, ink);
        (DrawList with, _) = Paint(p, z, ink);
        for (int i = 0; i < without.Commands.Count; i++) Assert.Equal(Canon(without.Commands[i]), Canon(with.Commands[i]));
        return [.. with.Commands.Skip(without.Commands.Count)];
    }

    /// <summary>A command as exact text (a polygon's points spelled out: a record compares its array by reference).</summary>
    private static string Canon(DrawCmd c) => c is PolygonCmd g
        ? "Polygon " + string.Join(";", g.Points.Select(q => q.X.ToString("R", CultureInfo.InvariantCulture) + "," + q.Y.ToString("R", CultureInfo.InvariantCulture))) + " " + g.Fill
        : c.ToString()!;

    private static List<string> Canon(IEnumerable<DrawCmd> cs) => [.. cs.Select(Canon)];

    // ================================================================== token / row accounting

    [Fact]
    public void TheTokens_AreExactlyTheKnownRows_OneEach_AtEveryZoom_AndNoTokenCarriesACount()
    {
        WorldState w = Populated(out int[] stationed, out int[] field, out int nearly);
        Assert.Equal(12, stationed.Length);
        WorldProjection p = WorldProjection.Build(w, Cfg, Name, Me);
        var rows = new List<int>();
        for (int i = 0; i < w.MilitaryUnits.Count; i++) rows.Add(w.MilitaryUnits[i].Id);
        Assert.Equal(1 + 11 + 8 + 1 + 1, rows.Count);   // the player's 21 and the AI's Warband
        foreach (WorldZoom z in Zooms)
        {
            (DrawList d, LensFrame f) = Paint(p, z);
            // One token per row: the ids drawn are the table's, each once, in table order (the paint-order pin).
            Assert.Equal(rows, f.UnitTokens);
            Assert.Equal(rows, f.UnitPlacements.Select(u => u.Id).ToList());
            // One PLATE per row, exactly at its placement (the 2s x 1.5s box), and no other plate.
            List<DrawCmd> layer = UnitLayer(p, z);
            foreach (UnitPlacement u in f.UnitPlacements)
            {
                double s = u.Size;
                var box = new RectD(u.X - s, u.Y - s * 0.75, s * 2, s * 1.5);
                Assert.Single(layer.OfType<RectCmd>(), r => r.Rect == box);
            }
            Assert.Equal(rows.Count, layer.OfType<RectCmd>().Count());
            // NO COUNT: the formation layer's only text is the identity labels (Regional and Settlement), one per token,
            // and none carries a digit — no "x12", no headcount.
            List<string> texts = layer.OfType<TextCmd>().Select(t => t.Text).ToList();
            if (z == WorldZoom.World) Assert.Empty(texts);
            else
            {
                Assert.Equal(p.Units.Select(u => u.IdentityName).ToList(), texts);
                foreach (string t in texts) Assert.DoesNotContain(t, c => c is >= '0' and <= '9');
            }
        }
        // The legend and the projection state the absence, verbatim.
        Assert.Contains(WorldProjection.FormationCountAbsent, p.Absent);
    }

    // ================================================================== the emblem, keyed by identity

    [Fact]
    public void TheEmblem_IsKeyedByTheCurrentIdentity_AndIsInjectiveOverTheWholeContent()
    {
        var seen = new HashSet<(int Glyph, int Form)>();
        int longest = 0;
        foreach (UnitFamily f in Families.Families)
        {
            longest = Math.Max(longest, f.Line.Count);
            for (int i = 0; i < f.Line.Count; i++)
            {
                UnitIdentity ident = f.Line[i];
                // Whatever family the row says, the emblem is the IDENTITY's: its family's mark, its place in the line.
                foreach (int rowFamily in new[] { f.Key, 0, 99 })
                {
                    UnitEmblem e = UnitEmblem.Of(Families, rowFamily, ident.Key);
                    Assert.Equal(new UnitEmblem(ident.Key, f.Key, i + 1), e);
                }
                Assert.True(seen.Add((f.Key, i + 1)), "two identities share an emblem: " + ident.Id);
            }
        }
        Assert.Equal(66, seen.Count);
        Assert.Equal(UnitEmblem.MaxPips, longest);   // the longest line draws every pip
        // An identity the content does not know: the row's family mark alone, no pip guessed.
        Assert.Equal(new UnitEmblem(4242, 3, 0), UnitEmblem.Of(Families, 3, 4242));
        Assert.Equal(UnitEmblem.Unkeyed(5) with { Identity = 7 }, UnitEmblem.Of(null, 5, 7));
    }

    private static Sim.Ui.Art.ParchmentPalette.Rgba Rgba(uint hex) => Sim.Ui.Art.ParchmentPalette.Rgba.Hex(hex);

    /// <summary>
    /// The emblem is keyed by the IDENTITY: the same formation's painted token (its placement unchanged) is command for
    /// command the same when the row's experience and army change, and changes when its identity does — the Age II
    /// modernization's axe warriors draw the warband's role mark and a SECOND form pip. The role mark is the H1 family
    /// token's, command for command (P11 adds the pips after it).
    /// </summary>
    [Fact]
    public void AModernizedFormation_DrawsItsNewForm_AndNothingElseOfTheRowMovesItsToken()
    {
        WorldState w = Base.Value.World.Clone();
        int at = -1;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Owner.Value == Me.Value) at = i;
        MilitaryUnitRow warband = w.MilitaryUnits[at];
        Assert.Equal("warband", Families.IdentityByKey(warband.Identity)!.Id);
        WorldState other = w.Clone();
        other.MilitaryUnits[at] = warband with { Experience = 7.5, Army = 3 };
        WorldState modern = w.Clone();
        modern.MilitaryUnits[at] = warband with { Identity = Families.FamilyById("heavy_infantry")!.Line[1].Key };
        WorldProjection p0 = WorldProjection.Build(w, Cfg, Name, Me), p1 = WorldProjection.Build(other, Cfg, Name, Me),
            p2 = WorldProjection.Build(modern, Cfg, Name, Me);
        foreach (WorldZoom z in Zooms)
        {
            List<string> before = Canon(UnitLayer(p0, z));
            Assert.Equal(before, Canon(UnitLayer(p1, z)));
            List<string> after = Canon(UnitLayer(p2, z));
            Assert.NotEqual(before, after);
            Assert.Single(Pips(p0, z, warband.Id));
            Assert.Equal(2, Pips(p2, z, warband.Id).Count);
        }
        var h1 = new DrawList();
        WorldLens.UnitToken(h1, 0, 0, 9, warband.Family, Rgba(0x123456));
        var keyed = new DrawList();
        WorldLens.UnitToken(keyed, 0, 0, 9, UnitEmblem.Of(Families, warband.Family, warband.Identity), Rgba(0x123456));
        Assert.Equal(Canon(h1.Commands), Canon(keyed.Commands.Take(h1.Commands.Count)));
        Assert.Equal(h1.Commands.Count + 1, keyed.Commands.Count);   // + the one form pip
    }

    /// <summary>The form pips of unit <paramref name="id"/>: the upright emblem strokes in the band under the role mark.</summary>
    private static List<LineCmd> Pips(WorldProjection p, WorldZoom z, int id)
    {
        (_, LensFrame f) = Paint(p, z);
        UnitPlacement u = f.UnitPlacements.Single(x => x.Id == id);
        return UnitLayer(p, z).OfType<LineCmd>().Where(l => l.X0 == l.X1 && Math.Abs(l.X0 - u.X) <= u.Size
            && l.Y0 == u.Y + u.Size * 0.54 && l.Y1 == u.Y + u.Size * 0.70).ToList();
    }

    // ================================================================== the anatomy fence

    /// <summary>
    /// D-038 C1, NOT NOW, NOT LATER: the token of EVERY identity of the content, in EVERY era's hand and at both token sizes,
    /// is one plate and, inside its box, at most two role strokes (lines, a small polygon, circles) and one upright pip per
    /// form — no text, no curve or polyline that could make a figure, no face, no limb, no frame of animation. Its strokes
    /// stay inside the plate (the hit rect), the hand's wobble and half a stroke allowed.
    /// </summary>
    [Fact]
    public void TheToken_IsAPlateWithStrokes_NeverAFigure_ForEveryIdentity_InEveryEra()
    {
        foreach (UiEra era in UiEras.All)
        {
            MapInk k = MapInk.For(EraThemes.For(era));
            foreach (double s in new[] { 9.0, 12.0 })
                foreach (UnitIdentity ident in Identities())
                {
                    UnitEmblem e = UnitEmblem.Of(Families, ident.FamilyKey, ident.Key);
                    var d = new DrawList();
                    WorldLens.UnitToken(d, 500, 300, s, e, Rgba(0x445566), k);
                    var plate = Assert.IsType<RectCmd>(d.Commands[0]);
                    Assert.Equal(new RectD(500 - s, 300 - s * 0.75, 2 * s, 1.5 * s), plate.Rect);
                    List<DrawCmd> marks = d.Commands.Skip(1).ToList();
                    Assert.All(marks, c => Assert.True(c is LineCmd or PolygonCmd or CircleCmd, era + " " + ident.Id + ": " + c.GetType().Name));
                    Assert.Equal(Math.Min(e.Form, UnitEmblem.MaxPips), marks.Count - RoleStrokes(ident.FamilyKey));
                    Assert.InRange(RoleStrokes(ident.FamilyKey), 1, 2);
                    double slack = k.Hand.WobblePx + 2.0;
                    foreach (DrawCmd c in marks)
                        foreach ((double x, double y) in Points(c))
                        {
                            Assert.InRange(x, 500 - s - slack, 500 + s + slack);
                            Assert.InRange(y, 300 - s * 0.75 - slack, 300 + s * 0.75 + slack);
                        }
                }
        }
    }

    /// <summary>The role mark's stroke count per family (the H1 emblems, read off the painter's switch).</summary>
    private static int RoleStrokes(int family)
    {
        var d = new DrawList();
        WorldLens.UnitToken(d, 0, 0, 12, family, Rgba(0x445566));
        return d.Commands.Count - 1;
    }

    private static IEnumerable<(double X, double Y)> Points(DrawCmd c) => c switch
    {
        LineCmd l => [(l.X0, l.Y0), (l.X1, l.Y1)],
        CircleCmd o => [(o.Cx - o.R, o.Cy - o.R), (o.Cx + o.R, o.Cy + o.R)],
        PolygonCmd g => g.Points,
        _ => [],
    };

    // ================================================================== tie-dense hit tests

    /// <summary>
    /// TIE-DENSE: the populated world's tokens — twelve fanned at one settlement, eight fanned at one field position, one
    /// a quarter-px off that fan (its plate almost wholly under the fan's first token) — in five table orders. In every
    /// order and at every zoom each row is picked at its OWN centre, and every probe across the fans picks the same
    /// formation. (The pre-P11 rule, last drawn wins, loses the quarter-px formation in the reversed order: the fan's
    /// first token is drawn over its centre.)
    /// </summary>
    [Fact]
    public void EveryRow_IsPickedAtItsOwnCentre_WhateverTheTableOrder()
    {
        WorldState w = Populated(out _, out _, out int nearly);
        WorldProjection p = WorldProjection.Build(w, Cfg, Name, Me);
        List<UnitLensView> units = [.. p.Units];
        var orders = new List<List<UnitLensView>> { units, units.AsEnumerable().Reverse().ToList(), units.OrderBy(u => u.Id).ToList(),
            units.OrderBy(u => (u.Id * 7919) % 101).ToList(), units.OrderBy(u => -u.Id).ToList() };
        foreach (WorldZoom z in Zooms)
        {
            int[]? reference = null;
            List<(double X, double Y)>? probes = null;
            foreach (List<UnitLensView> order in orders)
            {
                (_, LensFrame f) = Paint(With(p, order), z);
                Assert.Equal(order.Count, f.UnitPlacements.Count);
                foreach (UnitPlacement u in f.UnitPlacements)
                    Assert.True(u.Id == UnitSelection.HitTest(f, u.X, u.Y), z + ": unit " + u.Id + " is not picked at its own centre");
                if (probes is null)
                {
                    probes = [];
                    foreach (UnitPlacement u in f.UnitPlacements.OrderBy(x => x.Id))
                        for (int dx = -15; dx <= 15; dx += 3)
                            for (int dy = -12; dy <= 12; dy += 3) probes.Add((u.X + dx, u.Y + dy));
                }
                int[] got = [.. probes.Select(q => UnitSelection.HitTest(f, q.X, q.Y))];
                if (reference is null) reference = got;
                else Assert.Equal(reference, got);
            }
            Assert.Contains(nearly, reference!);
        }
    }

    /// <summary>A bit-exact distance tie — two tokens equidistant from the click, or two whose centres coincide — goes to
    /// the lower id, whatever order the placements are in; the click outside every plate (plus grip) picks nothing.</summary>
    [Fact]
    public void TheHitTest_BreaksADistanceTie_ByTheLowerId_InAnyOrder()
    {
        UnitPlacement a = new(9, 100, 100, 0) { Size = 12 }, b = new(4, 110, 100, 0) { Size = 12 }, c = new(6, 105, 100, 0) { Size = 12 };
        UnitPlacement[][] orders = [[a, b], [b, a]];
        foreach (UnitPlacement[] o in orders)
        {
            Assert.Equal(4, UnitSelection.HitTest(o, WorldZoom.Regional, 105, 100));   // equidistant from 9 and 4
            Assert.Equal(9, UnitSelection.HitTest(o, WorldZoom.Regional, 101, 100));
            Assert.Equal(4, UnitSelection.HitTest(o, WorldZoom.Regional, 109, 100));
        }
        UnitPlacement[][] stacked = [[a, c, b], [c, b, a], [b, a, c]];
        foreach (UnitPlacement[] o in stacked) Assert.Equal(6, UnitSelection.HitTest(o, WorldZoom.Regional, 105, 101));
        UnitPlacement twin = new(2, 100, 100, 0) { Size = 12 };
        Assert.Equal(2, UnitSelection.HitTest([a, twin], WorldZoom.Regional, 100, 100));
        Assert.Equal(2, UnitSelection.HitTest([twin, a], WorldZoom.Regional, 100, 100));
        Assert.Equal(-1, UnitSelection.HitTest([a, b], WorldZoom.Regional, 100 - 12 - UnitSelection.GripPx - 0.01, 100));
    }

    // ================================================================== the card: provenance, gaps, locked movement

    /// <summary>
    /// CARD FIELD PROVENANCE: one formation's row is set to distinctive values; each card line reads exactly its field (or
    /// the content's entry for it), and changing ONE field changes exactly the lines that read it. The gap lines and the
    /// locked movement line are constants that no field reaches; no line shows a figure the row does not hold.
    /// </summary>
    [Fact]
    public void TheCard_ShowsOnlyWhatTheRowAndTheContentHold_FieldByField()
    {
        WorldState w = Base.Value.World.Clone();
        int at = -1;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Owner.Value == Me.Value) at = i;
        UnitIdentity axe = Families.FamilyById("heavy_infantry")!.Line[1];
        MilitaryUnitRow row = w.MilitaryUnits[at] with
        {
            Identity = axe.Key, Location = new SettlementId(-1), X = 123.5, Y = 45.25, Experience = 2.5, Army = 7,
        };
        w.MilitaryUnits[at] = row;
        UnitCard card = UnitCardModel.Build(w, Cfg, row.Id, Me, Name)!;
        Assert.Equal("Axe warriors", card.Title);
        Assert.Equal("Your formation", card.Owner);
        Assert.Equal("Heavy Infantry / Line Infantry", card.Family);
        Assert.Equal("Its Age II form: Axe warriors", card.AgeForm);
        Assert.Equal("In the field", card.Station);
        Assert.Equal("Position: 123.5, 45.25 on the map", card.Position);
        Assert.Equal("Experience: 2.5 - nothing earns it yet", card.Experience);
        Assert.Equal("Army: number 7", card.Army);
        Assert.Equal(UnitCardModel.NotSimulatedLines, card.NotSimulated);
        Assert.Equal(UnitCardModel.MovementText, card.Movement);

        // One field at a time: exactly the lines that read it move (MUST), and at most those that may follow them (the
        // next-Age plan follows the identity and the owner's Age).
        (Func<MilitaryUnitRow, MilitaryUnitRow> Change, string[] Must, string[] May)[] cases =
        [
            (r => r with { Experience = 4.0 }, ["Experience"], []),
            (r => r with { Army = 0 }, ["Army"], []),
            (r => r with { X = 124.75 }, ["Position"], []),
            (r => r with { Y = 46.0 }, ["Position"], []),
            (r => r with { Identity = Families.FamilyById("heavy_infantry")!.Line[2].Key }, ["AgeForm", "Line", "Title"], ["NextAge"]),
            (r => r with { Location = new SettlementId(Base.Value.World.Settlements[0].Id.Value) }, ["Station"], []),
            (r => r with { Owner = Rival(Base.Value.World) }, ["Mine", "Owner"], ["NextAge"]),
        ];
        static Dictionary<string, string> Lines(UnitCard c) => new()
        {
            ["Title"] = c.Title, ["Owner"] = c.Owner, ["Mine"] = c.Mine.ToString(), ["Family"] = c.Family, ["Line"] = c.Line,
            ["AgeForm"] = c.AgeForm, ["Station"] = c.Station, ["NextAge"] = c.NextAge, ["Limitation"] = c.Limitation,
            ["Position"] = c.Position, ["Experience"] = c.Experience, ["Army"] = c.Army,
            ["NotSimulated"] = string.Join("|", c.NotSimulated), ["Movement"] = c.Movement,
        };
        Dictionary<string, string> before = Lines(card);
        foreach ((Func<MilitaryUnitRow, MilitaryUnitRow> change, string[] must, string[] may) in cases)
        {
            WorldState v = w.Clone();
            v.MilitaryUnits[at] = change(row);
            Dictionary<string, string> after = Lines(UnitCardModel.Build(v, Cfg, row.Id, Me, Name)!);
            var moved = before.Keys.Where(key => before[key] != after[key]).ToList();
            foreach (string m in must) Assert.Contains(m, moved);
            foreach (string m in moved) Assert.True(must.Contains(m) || may.Contains(m), m + " moved with " + string.Join(",", must));
        }
    }

    /// <summary>
    /// "NO STATISTIC IS INVENTED": strength, health, attack, Action Points and the headcount are each named on their own
    /// line and said to be not simulated, with no number; movement is the locked line; and nothing on the card is a
    /// number the row does not hold — every digit the card shows is in the position, experience or army line (the row's)
    /// or the owner's polity id, or "M7" in the milestone the movement line names.
    /// </summary>
    [Fact]
    public void TheCard_NamesEachGap_AndTheLockedMovementLine_AndShowsNoNumberTheRowDoesNotHold()
    {
        Assert.Equal(["Strength", "Health", "Attack", "Action points", "Headcount"],
            UnitCardModel.NotSimulatedLines.Select(l => l[..l.IndexOf(':')]).ToList());
        foreach (string line in UnitCardModel.NotSimulatedLines)
        {
            Assert.Contains(": not simulated", line, StringComparison.Ordinal);
            Assert.DoesNotContain(line, c => c is >= '0' and <= '9');
        }
        Assert.Equal("Movement: not simulated in this build — scheduled for the Battle Layer (M7), awaiting the Director's ruling",
            UnitCardModel.MovementText);
        WorldState w = Base.Value.World.Clone();
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
        {
            UnitCard card = UnitCardModel.Build(w, Cfg, w.MilitaryUnits[i].Id, Me, Name)!;
            string rowLines = card.Position + card.Experience + card.Army + card.Owner;
            foreach (string line in new[] { card.Title, card.Family, card.Line, card.AgeForm, card.Station, card.NextAge })
                Assert.DoesNotContain(line, c => c is >= '0' and <= '9');
            Assert.Equal("7", new string([.. card.Movement.Where(c => c is >= '0' and <= '9')]));     // "(M7)" and nothing else
            Assert.Equal("7", new string([.. card.Limitation.Where(c => c is >= '0' and <= '9')]));   // "(milestone M7)"
            Assert.Equal("Experience: 0.0 - nothing earns it yet", card.Experience);   // the founded value, as recorded
            Assert.StartsWith("Army: none", card.Army, StringComparison.Ordinal);
            Assert.NotEmpty(rowLines);
        }
    }

    [Fact]
    public void TheLegend_SaysTheTokensCarryNoCount_AndWhatTheirMarksAre()
    {
        WorldProjection p = WorldProjection.Build(Base.Value.World, Cfg, Name, Me);
        (_, LensFrame f) = Paint(p, WorldZoom.Regional, legend: new LegendAnchor(4000, 4000, 600));
        MapLegend legend = f.Legend!;
        Assert.Contains(WorldLayer.MilitaryFormations, legend.Layers);
        Assert.Contains("Formations: a token each - family emblem, a pip per form in its line", legend.Entries);
        Assert.Contains(WorldProjection.FormationCountAbsent, legend.Absent);
        Assert.Equal("Formation counts: not simulated - a formation holds no people, so no token shows a count",
            WorldProjection.FormationCountAbsent);
    }

    // ================================================================== leak tests with the P7 stub

    /// <summary>
    /// D-039 C1 THROUGH THE P7 SEAM: a rival formation in the field is drawn, picked and carded only while its node is
    /// VISIBLE. Known but out of sight, or unknown, it is not drawn, a click at its place picks nothing of it, and its card
    /// reads as an id that names nothing; the player's own formation is drawn and picked under every mask.
    /// </summary>
    [Fact]
    public void ARivalFormation_IsDrawnAndSelectable_OnlyWhereThePlayerSeesIt()
    {
        WorldState w = Base.Value.World.Clone();
        PolityId rival = Rival(w);
        int next = 0;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) next = Math.Max(next, w.MilitaryUnits[i].Id + 1);
        MilitaryUnitRow template = MilitaryQuery.Units(w, rival)[0];
        MilitaryUnitRow scout = template with { Id = next, Location = new SettlementId(-1), X = 300.5, Y = 400.5 };
        w.MilitaryUnits.Add(scout);
        PolityKnowledge grid = PolityKnowledge.Of(w, Me);
        (int lsize, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        int node = grid.NodeAt(scout.X, scout.Y);
        Assert.True(node >= 0);
        int myUnit = MilitaryQuery.Units(w, Me)[0].Id;

        foreach ((string what, bool known, bool visible) in new[] { ("visible", true, true), ("known, out of sight", true, false), ("unknown", false, false) })
        {
            var k = new bool[lsize * lsize];
            var v = new bool[lsize * lsize];
            Array.Fill(k, true);
            Array.Fill(v, true);
            k[node] = known;
            v[node] = visible;
            PlayerKnowledge pk = PlayerKnowledge.From(w, PolityKnowledge.Restricted(w, Me, k, v));
            WorldProjection p = WorldProjection.Build(w, Cfg, Name, Me, knowledge: pk);
            (_, LensFrame f) = Paint(p, WorldZoom.Regional);
            bool drawn = f.UnitTokens.Contains(scout.Id);
            Assert.True(drawn == visible, what + ": drawn " + drawn);
            Assert.Contains(myUnit, f.UnitTokens);
            (double sx, double sy) = (4 * (scout.X + 0.5), 4 * (scout.Y + 0.5));
            int picked = UnitSelection.HitTest(f, sx, sy);
            Assert.True(visible ? picked == scout.Id : picked != scout.Id, what + ": picked " + picked);
            UnitCard? card = UnitCardModel.Build(w, Cfg, scout.Id, Me, Name, pk);
            Assert.True((card is not null) == visible, what + ": card " + (card is not null));
            if (card is not null)
            {
                Assert.False(card.Mine);
                Assert.Equal(UnitCardModel.NotSimulatedLines, card.NotSimulated);
            }
        }
    }

    /// <summary>
    /// METAMORPHIC, ON VALUES: through the stub that hides the AI polity whole, everything P11 reads of its formations —
    /// the identity (the emblem's key), experience, army membership, and a second formation of it in the field inside its
    /// hidden nodes — is perturbed; the projection (emblems included), the lens paint (SVG bytes and LensFrame) and the card
    /// of every id are IDENTICAL. POSITIVE CONTROL: all-known, the same perturbation changes each of them.
    /// </summary>
    [Fact]
    public void PerturbingAHiddenFormation_LeavesEveryTokenAndCardIdentical_AndTheAllKnownControlSeesIt()
    {
        WorldState a = Base.Value.World.Clone();
        PolityId rival = Rival(a);
        bool[] mask = KnowledgeStubs.HiddenMask(a, Me, rival);
        WorldState b = a.Clone();
        int next = 0, hidden = -1;
        for (int i = 0; i < b.MilitaryUnits.Count; i++)
        {
            next = Math.Max(next, b.MilitaryUnits[i].Id + 1);
            if (b.MilitaryUnits[i].Owner.Value == rival.Value) hidden = i;
        }
        MilitaryUnitRow h = b.MilitaryUnits[hidden];
        b.MilitaryUnits[hidden] = h with { Identity = Families.FamilyById("heavy_infantry")!.Line[3].Key, Experience = 6.5, Army = 4 };
        b.MilitaryUnits.Add(h with { Id = next, Location = new SettlementId(-1), X = h.X + 1.0, Y = h.Y });
        Assert.False(KnowledgeStubs.Over(b, Me, mask).KnowsUnit(b.MilitaryUnits[b.MilitaryUnits.Count - 1]));

        string Readers(WorldState w, PlayerKnowledge k)
        {
            var sb = new System.Text.StringBuilder();
            WorldProjection p = WorldProjection.Build(w, Cfg, Name, Me, knowledge: k);
            sb.Append(ProjectionDigest.Of(p));
            foreach (UnitLensView u in p.Units) sb.Append("emblem ").Append(u.Id).Append(' ').Append(u.Emblem).Append('\n');
            foreach (WorldZoom z in Zooms)
            {
                (DrawList d, LensFrame f) = Paint(p, z);
                sb.Append(ProjectionDigest.Sha(SvgWriter.Write(d, 4096, 4096))).Append('\n').Append(ProjectionDigest.Of(f));
            }
            for (int id = 0; id <= next; id++)
            {
                UnitCard? c = UnitCardModel.Build(w, Cfg, id, Me, Name, k);
                sb.Append(c is null ? "none" : c + string.Join("|", c.NotSimulated)).Append('\n');
            }
            return sb.ToString();
        }
        Assert.Equal(Readers(a, KnowledgeStubs.Over(a, Me, mask)), Readers(b, KnowledgeStubs.Over(b, Me, mask)));
        Assert.NotEqual(Readers(a, PlayerKnowledge.Of(a)), Readers(b, PlayerKnowledge.Of(b)));
        // The control reaches each channel: the emblem, the card's identity and the recorded fields, the field formation.
        WorldProjection pa = WorldProjection.Build(a, Cfg, Name, Me), pb = WorldProjection.Build(b, Cfg, Name, Me);
        Assert.NotEqual(pa.Units.Single(u => u.Id == h.Id).Emblem, pb.Units.Single(u => u.Id == h.Id).Emblem);
        Assert.Equal(pa.Units.Count + 1, pb.Units.Count);
        UnitCard ca = UnitCardModel.Build(a, Cfg, h.Id, Me, Name)!, cb = UnitCardModel.Build(b, Cfg, h.Id, Me, Name)!;
        Assert.NotEqual(ca.Title, cb.Title);
        Assert.NotEqual(ca.Experience, cb.Experience);
        Assert.NotEqual(ca.Army, cb.Army);
    }

    // ================================================================== the real UI

    /// <summary>
    /// The detail window in the live UI: clicking the Warband's token opens it; every line the card model holds is drawn
    /// on the card's surface (FrameText), the locked movement line among them; its ONLY control is close (no Move, no
    /// order); at the 1280x800 design window nothing is scrolled away; a right-click on the map queues nothing.
    /// </summary>
    [Fact]
    public void TheDetailWindow_DrawsEveryLine_FitsTheDesignWindow_AndItsOnlyControlIsClose()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("detail"), Assets());
        MilitaryUnitRow unit = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, unit.Location.Value, h.Ui.MapRect);
        h.Idle(2);
        UnitPlacement at = h.Ui.LensFrame!.UnitPlacements.Single(p => p.Id == unit.Id);
        h.Click(at.X, at.Y);
        h.Idle(1);
        UnitCard card = h.Ui.UnitCard!;
        IReadOnlyList<string> drawn = h.Ui.FrameText.Texts(FrameSurface.UnitCard);
        Assert.Equal(UnitCardModel.NotSimulatedLines, card.NotSimulated);
        Assert.Equal(UnitCardModel.MovementText, card.Movement);
        var want = new List<string> { card.Title, card.Experience, card.Army, UnitCardModel.MovementText, card.Limitation };
        // The founded Warband stands on its capital's site: its position says nothing the station does not, so it is not drawn.
        Assert.False(card.PositionAddsToStation);
        Assert.DoesNotContain(card.Position, drawn);
        want.AddRange(UnitCardModel.NotSimulatedLines);
        foreach (string line in want) Assert.Contains(line, drawn);
        Assert.Equal(["unit-close"], OnCard(h));
        // At the 1280x800 design window the whole card shows UNSCROLLED (measured: P11 record).
        Assert.Equal(0f, h.Ui.UnitCardScroll.Max);
        int orders = h.Ui.Session.Orders.Count;
        h.RightClick(at.X + 120, at.Y + 60);
        Assert.Equal(orders, h.Ui.Session.Orders.Count);
        Assert.Empty(h.Problems);
    }

    /// <summary>
    /// At the 1080x640 window floor the card is taller than the room under the selection card: its BODY scrolls under the
    /// fixed header — the wheel over the card scrolls it to the end (every line reachable) and does not zoom the map, and
    /// the close button stays where it was, the card's only control.
    /// </summary>
    [Fact]
    public void AtTheWindowFloor_TheCardsBodyScrolls_UnderAFixedHeader_AndTheWheelThereDoesNotZoomTheMap()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("detail-floor"), Assets(),
            width: PanelLayout.MinWindowWidth, height: PanelLayout.MinWindowHeight);
        MilitaryUnitRow unit = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, unit.Location.Value, h.Ui.MapRect);
        h.Idle(2);
        UnitPlacement at = h.Ui.LensFrame!.UnitPlacements.Single(p => p.Id == unit.Id);
        h.Click(at.X, at.Y);
        h.Idle(1);
        Assert.True(h.Ui.UnitCardContentHeight > h.Ui.UnitCardRect.Height, "the card fits at the floor: nothing to scroll");
        Assert.True(h.Ui.UnitCardScroll.Max > 0, "the card is taller than its rect but does not scroll");
        UiControl close = h.Ui.Controls.Find("unit-close")!.Value;
        double zoom = h.Ui.Camera.Zoom;
        (double cx, double cy) = (h.Ui.UnitCardRect.X + h.Ui.UnitCardRect.Width / 2, h.Ui.UnitCardRect.Y + h.Ui.UnitCardRect.Height - 20);
        h.MoveTo(cx, cy);   // the pointer is over the card before the wheel turns, as a player's is
        for (int i = 0; i < 30 && h.Ui.UnitCardScroll.Y < h.Ui.UnitCardScroll.Max; i++) h.Wheel(cx, cy, -1);
        Assert.Equal(h.Ui.UnitCardScroll.Max, h.Ui.UnitCardScroll.Y);
        Assert.Equal(zoom, h.Ui.Camera.Zoom);
        UiControl still = h.Ui.Controls.Find("unit-close")!.Value;
        Assert.Equal((close.X0, close.Y0, close.X1, close.Y1), (still.X0, still.Y0, still.X1, still.Y1));
        Assert.Equal(["unit-close"], OnCard(h));
        Assert.Contains(h.Ui.UnitCard!.Limitation, h.Ui.FrameText.Texts(FrameSurface.UnitCard));
        Assert.True(h.ClickControl("unit-close"));
        Assert.Equal(-1, h.Ui.SelectedUnit);
        Assert.Empty(h.Problems);
    }

    /// <summary>The controls drawn inside the formation card's rect.</summary>
    private static List<string> OnCard(UiFrameHarness h)
    {
        var onCard = new List<string>();
        foreach (UiControl c in h.Ui.Controls.Last)
            if (c.X0 >= h.Ui.UnitCardRect.X && c.X1 <= h.Ui.UnitCardRect.X + h.Ui.UnitCardRect.Width
                && c.Y0 >= h.Ui.UnitCardRect.Y && c.Y1 <= h.Ui.UnitCardRect.Y + h.Ui.UnitCardRect.Height)
                onCard.Add(c.Name);
        return onCard;
    }
}
