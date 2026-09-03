using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Trade;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.6 — foreign trade as a derived classification over the existing D-034
/// machinery. These tests assert the CLASSIFICATION and the fact that adding it
/// changed nothing about how goods move.
/// </summary>
public class ForeignTradeScopeTests
{
    private static WorldState Found(int settlements = 3, ulong seed = 42)
        => WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), seed, settlements);

    private static PolityId PlayerOf(WorldState w)
    {
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return w.Polities[i].Id;
        throw new InvalidOperationException("founded world has no player Empire");
    }

    /// <summary>Move one settlement under a second Empire — the only way a
    /// boundary exists at all today (see the unreachability test below).</summary>
    private static PolityId Secede(WorldState w, SettlementId place, int id = 2)
    {
        var rival = new PolityId(id);
        w.Polities.Add(new PolityRow(rival, CommandSource.Ai));
        for (int i = 0; i < w.Controls.Count; i++)
            if (w.Controls[i].Place == place) { w.Controls[i] = new ControlRow(rival, place, 1.0); return rival; }
        w.Controls.Add(new ControlRow(rival, place, 1.0));
        return rival;
    }

    [Fact]
    public void SamePolityIsDomestic()
    {
        WorldState w = Found();
        Assert.Equal(TradeScope.Domestic,
            TradeScopes.Classify(w, w.Settlements[0].Id, w.Settlements[1].Id));
    }

    [Fact]
    public void DifferentPolitiesIsForeign_AndTheClassificationIsSymmetric()
    {
        WorldState w = Found();
        SettlementId ours = w.Settlements[0].Id;
        SettlementId theirs = w.Settlements[1].Id;
        Secede(w, theirs);

        Assert.Equal(TradeScope.Foreign, TradeScopes.Classify(w, ours, theirs));
        Assert.Equal(TradeScope.Foreign, TradeScopes.Classify(w, theirs, ours));

        // ...and the settlements that stayed are still domestic to each other.
        Assert.Equal(TradeScope.Domestic,
            TradeScopes.Classify(w, w.Settlements[0].Id, w.Settlements[2].Id));
    }

    [Fact]
    public void ScopeIsDerivedFromControl_SoMovingControlMovesTheClassification()
    {
        // The point of deriving rather than storing: one edit to the control
        // relation, and every reader agrees immediately. Nothing to resync.
        WorldState w = Found();
        SettlementId a = w.Settlements[0].Id;
        SettlementId b = w.Settlements[1].Id;
        Assert.Equal(TradeScope.Domestic, TradeScopes.Classify(w, a, b));

        PolityId rival = Secede(w, b);
        Assert.Equal(TradeScope.Foreign, TradeScopes.Classify(w, a, b));

        // Hand it back: domestic again, with no other state touched.
        for (int i = 0; i < w.Controls.Count; i++)
            if (w.Controls[i].Place == b) w.Controls[i] = new ControlRow(PlayerOf(w), b, 1.0);
        Assert.Equal(TradeScope.Domestic, TradeScopes.Classify(w, a, b));
        Assert.NotEqual(PlayerOf(w), rival);
    }

    [Fact]
    public void AnUnruledEndpointIsNeitherDomesticNorForeign_AndNoPolityIsFabricated()
    {
        // Colonization founds settlements and writes no control row, so this is a
        // reachable state, not a malformed one. The classifier must not guess.
        WorldState w = Found();
        var orphan = new SettlementId(9_999);

        Assert.Equal(TradeScope.Unruled, TradeScopes.Classify(w, w.Settlements[0].Id, orphan));
        Assert.Equal(TradeScope.Unruled, TradeScopes.Classify(w, orphan, w.Settlements[0].Id));
        Assert.False(EmpireQuery.TryGetController(w, orphan, out PolityId none));
        Assert.Equal(default, none);

        int before = w.Polities.Count;
        TradeScopes.Classify(w, w.Settlements[0].Id, orphan);
        Assert.Equal(before, w.Polities.Count);   // nothing invented
    }

    [Fact]
    public void NoCompetingIdentityWasIntroduced_TheScopeReadsPolityIdAlone()
    {
        WorldState w = Found();
        Secede(w, w.Settlements[1].Id);

        // The classifier's answer is a function of ControlRow.Polity, which is a
        // PolityId. If a second identity had been introduced this could not hold.
        Assert.True(EmpireQuery.TryGetController(w, w.Settlements[1].Id, out PolityId owner));
        Assert.Equal(2, owner.Value);
        Assert.True(EmpireQuery.TryGetCommandSource(w, owner, out CommandSource src));
        Assert.Equal(CommandSource.Ai, src);
        Assert.Equal(TradeScope.Foreign, TradeScopes.Classify(w, w.Settlements[0].Id, w.Settlements[1].Id));
    }

    [Fact]
    public void CountingForeignFlowsReadsTheRealisedFlowTable()
    {
        WorldState w = Found();
        SettlementId a = w.Settlements[0].Id;
        SettlementId b = w.Settlements[1].Id;
        var good = new GoodId(TestConfigs.Sim().Goods!.IdOf("timber"));

        w.TradeFlows.Add(new TradeFlowRow(a, w.Settlements[2].Id, good, 5));
        w.TradeFlows.Add(new TradeFlowRow(a, b, good, 7));
        Assert.Equal(0, TradeScopes.CountForeignFlows(w));   // all one Empire

        Secede(w, b);
        Assert.Equal(1, TradeScopes.CountForeignFlows(w));   // exactly the a→b flow
        Assert.Equal(TradeScope.Foreign, TradeScopes.Classify(w, w.TradeFlows[1]));
        Assert.Equal(TradeScope.Domestic, TradeScopes.Classify(w, w.TradeFlows[0]));
    }

    [Fact]
    public void AddingTheClassificationChangedNoTradeBehaviour()
    {
        // The seam is a pure reader. A world run with it present must be
        // bit-identical to the same world run — there is no arm in which the
        // classifier can alter a flow, and this is the assertion that says so.
        static string Run()
        {
            WorldState w = Found(settlements: 4);
            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            var exec = new TurnExecutor(
                EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream,
                    Sim.Core.SystemCatalog.All(TestConfigs.Sim(), TestConfigs.Worldgen())));
            return WorldHash.ComputeHex(exec.Run(w, 12));
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void FOREIGN_TRADE_IS_CURRENTLY_UNREACHABLE_IN_A_CANONICAL_WORLD()
    {
        // THE HONEST FINDING, pinned so it cannot rot silently.
        //
        // WorldFounding is the ONLY producer of Polities/Controls/Capitals in the
        // tree, and it creates exactly ONE Empire holding every founded
        // settlement. Colonization adds settlements and writes no control row.
        // So in a canonical world every pair is Domestic or Unruled, and FOREIGN
        // IS UNREACHABLE — not rare, structurally impossible.
        //
        // T4.6 therefore ships the classification and no activity. Manufacturing
        // a second polity to make foreign trade "work" would be exactly the
        // fabricated volume the packet forbids. When something real creates a
        // second Empire — secession, conquest, a stateless colony taking control
        // — this test fails and tells its reader the world has changed.
        WorldState w = Found(settlements: 6);
        Assert.Equal(1, w.Polities.Count);

        int foreign = 0, domestic = 0, unruled = 0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            for (int j = i + 1; j < w.Settlements.Count; j++)
            {
                switch (TradeScopes.Classify(w, w.Settlements[i].Id, w.Settlements[j].Id))
                {
                    case TradeScope.Foreign: foreign++; break;
                    case TradeScope.Domestic: domestic++; break;
                    default: unruled++; break;
                }
            }
        }

        Assert.Equal(0, foreign);
        Assert.Equal(0, unruled);
        Assert.Equal(15, domestic);   // every one of the 6-choose-2 pairs
    }

    [Fact]
    public void AMalformedDoubleControlResolvesByLowestPolityId_NotByRowOrder()
    {
        // D-037 A3 says a place has one controller or none, and NOTHING enforces
        // it. So the classifier must answer from the row SET, not the row ORDER —
        // otherwise the same world serialized differently classifies differently,
        // which is a determinism defect (law 5). Two worlds, same rows, opposite
        // insertion order, must agree.
        static WorldState WithDoubleControl(bool lowFirst)
        {
            WorldState w = Found(settlements: 2);
            SettlementId place = w.Settlements[1].Id;
            for (int i = 0; i < w.Controls.Count; i++)
                if (w.Controls[i].Place == place) w.Controls[i] = new ControlRow(new PolityId(7), place, 1.0);

            w.Polities.Add(new PolityRow(new PolityId(3), CommandSource.Ai));
            w.Polities.Add(new PolityRow(new PolityId(7), CommandSource.Ai));
            if (lowFirst)
            {
                // rewrite so the LOW id sits first, the high id second
                for (int i = 0; i < w.Controls.Count; i++)
                    if (w.Controls[i].Place == place) w.Controls[i] = new ControlRow(new PolityId(3), place, 1.0);
                w.Controls.Add(new ControlRow(new PolityId(7), place, 1.0));
            }
            else
            {
                w.Controls.Add(new ControlRow(new PolityId(3), place, 1.0));
            }
            return w;
        }

        WorldState a = WithDoubleControl(lowFirst: true);
        WorldState b = WithDoubleControl(lowFirst: false);
        SettlementId contested = a.Settlements[1].Id;

        Assert.True(EmpireQuery.TryGetController(a, contested, out PolityId ca));
        Assert.True(EmpireQuery.TryGetController(b, contested, out PolityId cb));
        Assert.Equal(3, ca.Value);      // the LOWEST id, both times
        Assert.Equal(ca, cb);           // and order did not change the answer
        Assert.Equal(
            TradeScopes.Classify(a, a.Settlements[0].Id, contested),
            TradeScopes.Classify(b, b.Settlements[0].Id, contested));
    }
}
