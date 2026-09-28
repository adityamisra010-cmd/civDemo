using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Sim.Ui.World.Content;
using Sim.Ui.World.Live;
using Sim.Ui.World.Scene;
using Sim.Ui.World.View;
using SettlementView = Sim.Ui.World.View.SettlementView;
using Xunit;

namespace Sim.Ui.Tests.World;

/// <summary>The live adapters map rows the simulation already holds — read-only, deriving no
/// rule — and a whole live frame (sources, view, scene, hit test, inspector) leaves the
/// authoritative world hash-identical.</summary>
public class LiveWorldTests
{
    private static readonly WorldMorphology M = WorldTestKit.Morph();

    private static (UiSession Session, WorldSources Sources) Live()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        WorldSources sources = LiveWorld.Sources(() => session.World, id => session.Names.Name(id), session.Config.Goods!, null);
        return (session, sources);
    }

    [Fact]
    public void AFullLiveFrame_LeavesTheWorldHashUnchanged()
    {
        (UiSession session, WorldSources sources) = Live();
        string before = WorldHash.ComputeHex(session.World);
        WorldView view = WorldViewBuilder.Build(sources, M);
        foreach (double zoom in new[] { 0.8, 8.0, 20.0 })
            foreach (WorldSceneOptions options in new[] { WorldSceneOptions.LiveOverlay, WorldSceneOptions.Full })
            {
                SettlementView s = view.Settlements[0];
                var proj = new WorldProjection(s.Report.Position.X, s.Report.Position.Y, zoom, 1280, 800);
                var ui = new WorldUiState { MirrorSettlementKey = s.Key, Selected = s.Id };
                WorldFrame f = WorldScene.Paint(view, M, proj, ui, options, ApproxTextMeasure.Instance);
                f.Hits.HitTest(640, 400);
                foreach (SettlementView sv in view.Settlements) WorldInspector.Details(view, M, sv.Id);
                foreach (ResourceView r in view.Resources) WorldInspector.Details(view, M, r.Id);
            }
        Assert.Equal(before, WorldHash.ComputeHex(session.World));
    }

    [Fact]
    public void Settlements_AreTheWorldsRows_WithTheirOwnSizeAndPopulation()
    {
        (UiSession session, WorldSources sources) = Live();
        WorldState w = session.World;
        IReadOnlyList<SettlementReport> items = sources.Settlements.Current.Items;
        Assert.Equal(w.Settlements.Count, items.Count);
        foreach (SettlementReport r in items)
        {
            int id = (int)r.SourceId!.Value;
            Assert.Equal(LiveWorld.Key(id), r.Key);
            Assert.Equal(10, r.Key.Length);
            Assert.Equal(ReportedVisibility.NotModelled, r.Visibility);
            long population = 0;
            for (int b = 0; b < w.Buckets.Count; b++) if (w.Buckets[b].Settlement.Value == id) population += w.Buckets[b].Count.Value;
            Assert.Equal(population, r.Input("population"));
            bool hasSummary = false;
            for (int c = 0; c < w.CatchmentSummaries.Count; c++)
                if (w.CatchmentSummaries[c].Settlement.Value == id) { hasSummary = true; Assert.Equal(w.CatchmentSummaries[c].SizeTier, r.Input("sizeTier")); }
            if (!hasSummary) Assert.Null(r.Input("sizeTier"));
            SettlementRow row = w.Settlements.ToArrayForTest().Single(x => x.Id.Value == id);
            LineGeometry.Vertex v = OverlayMeshes.SettlementPosition(row, w.Terrain!.Size);
            Assert.Equal(new WorldPoint(v.X, v.Y), r.Position);   // the same point the game's marker uses
        }
        // Ordinal key order is numeric id order: the tie-break agrees with SettlementSelection's.
        Assert.Equal(items.Select(i => i.SourceId).OrderBy(x => x), items.Select(i => i.SourceId));
    }

    /// <summary>At turn 0 the simulation has written no CatchmentSummary, so no live settlement
    /// reports sizeTier: the DEMONSTRATION population driver must NOT stage it (review auth-1 /
    /// live-1) — the base footprint is drawn, unnamed, and says why.</summary>
    [Fact]
    public void LiveStages_WithoutSizeTier_AreTheUnnamedBase_NeverADemonstrationStage()
    {
        (UiSession session, WorldSources sources) = Live();
        Assert.Equal(0, session.World.CatchmentSummaries.Count);   // the fixture really is the no-summary case
        WorldView v = WorldViewBuilder.Build(sources, M);
        Assert.NotEmpty(v.Settlements);
        foreach (SettlementView s in v.Settlements)
        {
            Assert.Null(s.Report.Input("sizeTier"));
            Assert.Equal(0, s.Stage.Index);
            Assert.False(s.Stage.Reported);
            Assert.NotEqual("population", s.Stage.Driver);
            Assert.DoesNotContain("DEMONSTRATION", s.Stage.Explanation, StringComparison.Ordinal);
            foreach (string named in new[] { "Town", "City", "settlement" }) Assert.DoesNotContain(named, s.Stage.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LiveStages_AreTheSimulationsSizeTier_NeverATownOrACity()
    {
        (UiSession session, WorldSources sources) = Live();
        session.EndTurn();   // the first turn writes the summaries
        WorldView v = WorldViewBuilder.Build(sources, M);
        int checkedCount = 0;
        foreach (SettlementView s in v.Settlements)
        {
            double tier = Assert.IsType<double>(s.Report.Input("sizeTier"));
            Assert.Equal((int)tier, s.Stage.Index);
            foreach (string named in new[] { "Town", "City", "settlement" }) Assert.DoesNotContain(named, s.Stage.Name, StringComparison.Ordinal);
            checkedCount++;
        }
        Assert.True(checkedCount > 0);
    }

    [Fact]
    public void Structures_AreTheM4DCounts_OneReportPerRow_NeverExpanded()
    {
        (UiSession session, WorldSources sources) = Live();
        WorldState w = session.World;
        SettlementId s = w.Settlements[0].Id;
        w.Structures.Add(new StructureRow(s, 1, 2));   // two granaries (test fixture world)
        w.Structures.Add(new StructureRow(s, 2, 0));   // a zero count is not a structure
        w.Clock = w.Clock with { Turn = w.Clock.Turn + 1 };   // the adapters re-read on a new turn
        IReadOnlyList<StructureReport> items = sources.Structures.Current.Items;
        StructureReport r = Assert.Single(items);
        Assert.Equal("granary", r.VisualType);
        Assert.Equal(2, r.Multiplicity);
        Assert.Null(r.PolityKey);          // M4-D: the settlement's, not a polity's
        Assert.Equal(0, r.Established);    // the row's append-only index: the order structures first stood
        Assert.Equal(LiveWorld.Key(s.Value), r.SettlementKey);
        WorldView v = WorldViewBuilder.Build(sources, M);
        StructureView sv = Assert.Single(v.Structures);
        Assert.NotNull(sv.Slot);
        Assert.Equal("granary", sv.Type.Id);
        Assert.Equal(1, sv.Stage.Index);   // the content's multiplicity driver: 2 granaries
        Assert.Contains(WorldInspector.Details(v, M, sv.Id)!.Lines, l => l.Label == "Count in this report" && l.Value == "2");
    }

    /// <summary>Review auth-2 / det-1 / place-1: a structure built LATER never displaces one that
    /// already stands — the live priority is the StructureRow's append-only index.</summary>
    [Fact]
    public void LiveStructures_ALaterProject_NeverMovesAnEarlierOne()
    {
        int moved = 0;
        for (int settlementRow = 0; settlementRow < 4; settlementRow++)
        {
            (UiSession session, WorldSources sources) = Live();
            WorldState w = session.World;
            SettlementId s = w.Settlements[settlementRow].Id;
            w.Structures.Add(new StructureRow(s, 2, 1));   // a workshop first
            Advance(w);
            LotGeometry? before = WorldViewBuilder.Build(sources, M).Structures.Single().Slot;
            w.Structures.Add(new StructureRow(s, 1, 1));   // then a granary (a LOWER project id)
            Advance(w);
            WorldView after = WorldViewBuilder.Build(sources, M);
            StructureView workshop = after.Structures.Single(x => x.Report.VisualType == "workshop");
            Assert.Equal(before, workshop.Slot);
            Assert.Equal(0, workshop.Report.Established);
            Assert.Equal(1, after.Structures.Single(x => x.Report.VisualType == "granary").Report.Established);
            if (before != workshop.Slot) moved++;
        }
        Assert.Equal(0, moved);
    }

    /// <summary>Review arch-2 / semantics-2: no source reports building maturity, so no icon
    /// carries the grammar's maturity-stage pips.</summary>
    [Fact]
    public void StructureIcons_CarryNoMaturityPips()
    {
        (UiSession session, WorldSources sources) = Live();
        session.World.Structures.Add(new StructureRow(session.World.Settlements[0].Id, 1, 3));
        Advance(session.World);
        Assert.All(WorldViewBuilder.Build(sources, M).Structures, s => Assert.Equal(0, s.Icon.Stage));
        for (int step = 0; step < 6; step++)
            Assert.All(WorldTestKit.DemoView(M, step).Structures, s => Assert.Equal(0, s.Icon.Stage));
    }

    [Fact]
    public void Notables_AreLivingPeopleAtTheirSettlement_ReportedOnce()
    {
        (UiSession session, WorldSources sources) = Live();
        WorldState w = session.World;
        var ledger = new Ledger(new Table<LedgerFlowRow>());
        int bucket = 0;
        while (w.Buckets[bucket].Count.Value <= 0) bucket++;
        int row = NotableLifecycle.Born(ledger, w.Buckets, w.Notables, bucket, new NotableId(7), new PolityId(1));
        Advance(w);
        AgentReport a = Assert.Single(sources.Agents.Current.Items);
        Assert.Equal("notable-" + LiveWorld.Key(7), a.Key);
        Assert.Equal(PolityRelation.Allegiance, a.Relation);
        Assert.Null(a.Position);
        Assert.Equal(LiveWorld.Key(w.Buckets[bucket].Settlement.Value), a.AttachedSettlementKey);

        SettlementId to = w.Settlements[w.Settlements.Count - 1].Id;
        NotableLifecycle.Defects(ledger, w.Notables, row, to, new PolityId(2));
        Advance(w);
        a = Assert.Single(sources.Agents.Current.Items);   // the vacated old row is not a person
        Assert.Equal(LiveWorld.Key(to.Value), a.AttachedSettlementKey);
        Assert.Equal(LiveWorld.Key(2), a.PolityKey);

        NotableLifecycle.Dies(ledger, w.Notables, NotableLifecycle.LivingRowOf(w.Notables, new NotableId(7)));
        Advance(w);
        Assert.Empty(sources.Agents.Current.Items);
    }

    private static void Advance(WorldState w) => w.Clock = w.Clock with { Turn = w.Clock.Turn + 1 };

    [Fact]
    public void Resources_AreTheDeposits()
    {
        (UiSession session, WorldSources sources) = Live();
        Assert.Equal(session.World.Deposits.Count, sources.Resources.Current.Items.Count);
        Assert.All(sources.Resources.Current.Items, r => Assert.NotNull(r.Input("abundance")));
    }

    [Fact]
    public void Snapshots_AreReused_UntilTheTurnChanges()
    {
        (UiSession session, WorldSources sources) = Live();
        Snapshot<SettlementReport> a = sources.Settlements.Current, b = sources.Settlements.Current;
        Assert.Same(a, b);
        Advance(session.World);
        Assert.NotSame(a, sources.Settlements.Current);
    }

    [Fact]
    public void EveryLiveString_IsLatin1()
    {
        (_, WorldSources sources) = Live();
        foreach (SettlementReport r in sources.Settlements.Current.Items) { Latin(r.Note); Latin(r.DisplayName); }
        foreach (PolityReport r in sources.Polities.Current.Items) { Latin(r.Note); Latin(r.DisplayName); }
        foreach (ResourceReport r in sources.Resources.Current.Items) { Latin(r.Note); Latin(r.DisplayName); }
        Latin(LiveWorld.Observer);
        static void Latin(string s) => Assert.Equal(DrawList.Latin1(s), s);
    }
}

internal static class TableTestExtensions
{
    public static List<T> ToArrayForTest<T>(this IReadOnlyTable<T> t) where T : unmanaged
    {
        var list = new List<T>(t.Count);
        for (int i = 0; i < t.Count; i++) list.Add(t[i]);
        return list;
    }
}
