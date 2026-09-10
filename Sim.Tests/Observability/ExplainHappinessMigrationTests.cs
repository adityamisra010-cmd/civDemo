using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;

namespace Sim.Tests.Observability;

/// <summary>T4.19 lane A2 — happiness and migration explanations read what
/// the public reader and the migration rows say, nothing else.</summary>
public class ExplainHappinessMigrationTests
{
    private const int Peasant = 1;

    [Fact]
    public void Happiness_IsThePublicReader_WithItsFactorsAndChains()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(4);
        WorldState w = worlds[^1];
        int explained = 0;
        var factors = new double[SettlementHappiness.FactorCount];
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            HappinessExplanation h = HappinessExplanation.For(w, cfg, s);
            Assert.Equal(SettlementHappiness.Of(w, s, cfg), h.Happiness);
            SettlementHappiness.Factors(w, s, cfg, factors);
            Assert.Equal(SettlementHappiness.FactorCount, h.Factors.Length);
            Assert.Equal(factors[(int)SettlementHappiness.Factor.Food], h.Factors[(int)SettlementHappiness.Factor.Food].Value);
            Assert.Equal(factors[(int)SettlementHappiness.Factor.Housing], h.Factors[(int)SettlementHappiness.Factor.Housing].Value);

            // Food hangs the food-supply block; Housing the housing block — the §5 chains, reused.
            Assert.Contains(h.Factors[(int)SettlementHappiness.Factor.Food].Chain, l => l.Node == ChainNode.DeficitRatio && l.Kind == LinkKind.Read);
            Assert.Contains(h.Factors[(int)SettlementHappiness.Factor.Food].Chain, l => l.Node == ChainNode.GrainHarvest);
            Assert.Contains(h.Factors[(int)SettlementHappiness.Factor.Housing].Chain, l => l.Node == ChainNode.Dwellings && l.Kind == LinkKind.Read);
            Assert.Contains(h.Factors[(int)SettlementHappiness.Factor.Housing].Chain, l => l.Node == ChainNode.HousingSufficiency && l.Kind == LinkKind.Recomputed);
            // One world asked about: no Δdwellings is claimed.
            Assert.DoesNotContain(h.Factors[(int)SettlementHappiness.Factor.Housing].Chain, l => l.Node == ChainNode.DwellingsDelta);
            explained++;
        }
        Assert.True(explained > 0);
        Assert.Contains("Comfort", HappinessExplanation.ScopeNote);
        Assert.Contains("Tier-A", HappinessExplanation.ScopeNote);
    }

    [Fact]
    public void Happiness_StarvedSettlement_FoodFactorFalls()
    {
        (SimConfig cfg, List<WorldState> worlds, int first) = ExplainRigs.Starved(12);
        Assert.True(first > 0);
        HappinessExplanation h = HappinessExplanation.For(worlds[first], cfg, new SettlementId(ExplainRigs.Target));
        double food = h.Factors[(int)SettlementHappiness.Factor.Food].Value;
        Assert.True(food < 1.0, $"food factor {food} did not fall on a starving settlement");
        Assert.Equal(1.0 - ExplainRigs.Deficit(worlds[first], ExplainRigs.Target), food);
        Assert.True(h.Happiness < 100.0);
    }

    /// <summary>
    /// A2-LABEL (lane B's verifier, D4). Every link says which world its value
    /// was READ from, and that must be the world it was actually read from —
    /// not a constant the shared block assumed. The first cut's FoodSupply
    /// wrote Prev on every link it emitted, so the happiness Food chain, built
    /// on NEXT, printed a Next deficit labelled "prev ConsumptionDeficits";
    /// beside the Migration tab's genuine prev deficit the two disagreed for
    /// the same settlement and turn. The checker never saw it because it
    /// resolved the happiness chain against (next, next), where a Prev label
    /// and a Next label open the same table.
    ///
    /// Pinned on the starved rig at the drawdown turn, where prev and next
    /// deficits DIFFER (0.772… on world 3, larger on world 4): the happiness
    /// chain's DeficitRatio says Next and equals next's table; the grievance
    /// chain's (both the bare Sustenance chain and GrievanceExplanation's
    /// primary chain) says Prev and equals prev's table; and nothing in either
    /// happiness chain claims Prev at all.
    /// </summary>
    [Fact]
    public void DeficitRatio_HappinessChainSaysNext_GrievanceChainSaysPrev_EachEqualToItsOwnWorld()
    {
        (SimConfig cfg, List<WorldState> worlds, int first) = ExplainRigs.Starved(12);
        Assert.True(first > 0);
        Assert.True(first + 1 < worlds.Count);
        WorldState prev = worlds[first], next = worlds[first + 1];
        var s = new SettlementId(ExplainRigs.Target);
        double prevDeficit = ExplainRigs.Deficit(prev, ExplainRigs.Target);
        double nextDeficit = ExplainRigs.Deficit(next, ExplainRigs.Target);
        // NON-VACUITY: a label bug is invisible when the two worlds agree.
        Assert.True(prevDeficit > 0.0);
        Assert.NotEqual(prevDeficit, nextDeficit);

        // Happiness asks about ONE world — next — and every read is from it.
        HappinessExplanation h = HappinessExplanation.For(next, cfg, s);
        Link hd = ExplainGrievanceTests.Single(h.Factors[(int)SettlementHappiness.Factor.Food].Chain, ChainNode.DeficitRatio);
        Assert.Equal(LinkKind.Read, hd.Kind);
        Assert.Equal(SourceWorld.Next, hd.World);
        Assert.Equal("ConsumptionDeficits", hd.SourceTable);
        Assert.Equal(nextDeficit, hd.Value);
        Assert.Equal(next.ConsumptionDeficits[hd.SourceIndex].DeficitRatio, hd.Value);
        Assert.Equal(s, next.ConsumptionDeficits[hd.SourceIndex].Settlement);
        Assert.Equal(1.0 - hd.Value, h.Factors[(int)SettlementHappiness.Factor.Food].Value);
        foreach (HappinessFactor f in h.Factors)
            foreach (Link l in f.Chain)
                Assert.True(l.World != SourceWorld.Prev, $"happiness {f.Name}: {l.Node} '{l.Label}' claims Prev on a one-world query");

        // The Sustenance chain under a grievance reads the fills and the deficit
        // off PREV (what the system read, §3.2); only the satisfaction sits on Next.
        CausalChain chain = CausalChain.ForNeed(prev, next, cfg, s, new ClassId(Peasant), BasketBook.SustenanceNeedId);
        Link gd = ExplainGrievanceTests.Single(chain.Links, ChainNode.DeficitRatio);
        Assert.Equal(LinkKind.Read, gd.Kind);
        Assert.Equal(SourceWorld.Prev, gd.World);
        Assert.Equal("ConsumptionDeficits", gd.SourceTable);
        Assert.Equal(prevDeficit, gd.Value);
        Assert.Equal(prev.ConsumptionDeficits[gd.SourceIndex].DeficitRatio, gd.Value);
        Assert.Equal(s, prev.ConsumptionDeficits[gd.SourceIndex].Settlement);
        Assert.NotEqual(hd.Value, gd.Value);
        foreach (Link l in chain.Links)
        {
            if (l.Node == ChainNode.SustenanceSatisfaction) { Assert.Equal(SourceWorld.Next, l.World); continue; }
            Assert.True(l.World is SourceWorld.Prev or SourceWorld.Config or SourceWorld.None,
                $"sustenance chain: {l.Node} '{l.Label}' claims {l.World}");
        }

        // And the chain GrievanceExplanation hangs under its primary is that same chain.
        GrievanceExplanation g = GrievanceExplanation.For(prev, next, cfg, s, new ClassId(Peasant));
        Assert.Equal(BasketBook.SustenanceNeedId, g.PrimaryNeedId);
        Link pd = ExplainGrievanceTests.Single(g.PrimaryChain!.Links, ChainNode.DeficitRatio);
        Assert.Equal(SourceWorld.Prev, pd.World);
        Assert.Equal(prevDeficit, pd.Value);
    }

    /// <summary>The supply blocks take the world's identity as a parameter and
    /// refuse one that is not a world: Config and None are not places a row
    /// can be read from, and a Δdwellings against a next world only makes
    /// sense when the block is on Prev.</summary>
    [Fact]
    public void SupplyBlocks_RefuseAWorldIdentityThatIsNotAWorld()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(1);
        var s = new SettlementId(ExplainRigs.Target);
        var into = new List<Link>();
        Assert.Throws<ArgumentOutOfRangeException>(() => CausalChain.FoodSupply(worlds[1], SourceWorld.Config, cfg, s, into));
        Assert.Throws<ArgumentOutOfRangeException>(() => CausalChain.FoodSupply(worlds[1], SourceWorld.None, cfg, s, into));
        Assert.Throws<ArgumentOutOfRangeException>(() => CausalChain.HousingSupply(worlds[1], SourceWorld.None, null, cfg, s, into));
        Assert.Throws<ArgumentException>(() => CausalChain.HousingSupply(worlds[1], SourceWorld.Next, worlds[1], cfg, s, into));
        Assert.Empty(into);
    }

    [Fact]
    public void Migration_ReadsFlowsAndSignals_OthersSorted_GapsRecorded()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(10);
        long moved = 0;
        int explained = 0;
        for (int t = 1; t < worlds.Count; t++)
        {
            WorldState prev = worlds[t - 1], next = worlds[t];
            for (int i = 0; i < prev.Settlements.Count; i++)
            {
                SettlementId s = prev.Settlements[i].Id;
                MigrationExplanation m = MigrationExplanation.For(prev, next, cfg, s);
                explained++;

                // Flows READ from Next's chronicle row.
                MigrationFlowRow flow = default;
                bool found = false;
                for (int f = 0; f < next.MigrationFlows.Count; f++)
                    if (next.MigrationFlows[f].Settlement == s) { flow = next.MigrationFlows[f]; found = true; break; }
                Assert.True(found, "migration writes a flow row for every settlement every turn");
                Assert.Equal(flow.Inflow, m.Inflow);
                Assert.Equal(flow.Outflow, m.Outflow);
                moved += m.Inflow + m.Outflow;

                // Pull READ from Next's smoothed row; push from Prev's deficit.
                Assert.True(m.PullRecorded);
                bool smoothedFound = false;
                for (int k = 0; k < next.SmoothedAttractiveness.Count; k++)
                    if (next.SmoothedAttractiveness[k].Settlement == s) { Assert.Equal(next.SmoothedAttractiveness[k].Value, m.Pull); smoothedFound = true; }
                Assert.True(smoothedFound);
                Assert.Equal(ExplainRigs.Deficit(prev, s.Value), m.PushDeficit);
                Assert.Equal(s, m.Self.Id);
                Assert.Equal(m.Pull, m.Self.SmoothedAttractiveness);
                Assert.Equal(SettlementHappiness.Of(prev, s, cfg), m.Self.Happiness);

                // Every other settlement, (value DESC, id ASC).
                Assert.Equal(prev.Settlements.Count - 1, m.Others.Length);
                for (int k = 1; k < m.Others.Length; k++)
                {
                    Destination a = m.Others[k - 1], b = m.Others[k];
                    Assert.True(a.SmoothedAttractiveness > b.SmoothedAttractiveness
                        || (a.SmoothedAttractiveness == b.SmoothedAttractiveness && a.Id.Value < b.Id.Value),
                        $"others not sorted (value DESC, id ASC) at {k}");
                    Assert.NotEqual(s, b.Id);
                }
                Assert.True(m.UnplacedDeparture >= 0.0);
            }
        }
        Assert.True(explained > 0);
        // NON-VACUITY: the dev world does migrate over ten turns — measured; a
        // run where nobody moved would make the flow READ assertions trivial.
        Assert.True(moved > 0, "vacuous: no migrant moved in 10 turns of the dev world");

        Assert.Contains("not recorded", MigrationExplanation.PairwiseFlows);
        Assert.Contains("not recorded", MigrationExplanation.Damping);
        Assert.Contains("not recorded", MigrationExplanation.ViabilityProducts);
        Assert.Contains("not recorded", MigrationExplanation.GapScale);
        Assert.Contains("gap-INDEPENDENT", MigrationExplanation.PushReading);
    }

    [Fact]
    public void Migration_StarvedSource_PushIsTheFamineDeficit_AndSelfIsRepellent()
    {
        (SimConfig cfg, List<WorldState> worlds, int first) = ExplainRigs.Starved(12);
        Assert.True(first > 0);
        WorldState prev = worlds[first], next = worlds[first + 1];
        var s = new SettlementId(ExplainRigs.Target);
        MigrationExplanation m = MigrationExplanation.For(prev, next, cfg, s);

        Assert.True(m.PushDeficit > 0.0, "the starved settlement's push deficit is zero");
        Assert.Equal(ExplainRigs.Deficit(prev, ExplainRigs.Target), m.PushDeficit);
        // As a DESTINATION the same settlement carries the repulsion input others see.
        Assert.Equal(m.PushDeficit, m.Self.DestinationDeficit);
        // Others still see a fed world: at least one destination has zero deficit and grain present.
        Assert.Contains(m.Others, o => o.DestinationDeficit == 0.0 && o.GrainPresent);
    }
}
