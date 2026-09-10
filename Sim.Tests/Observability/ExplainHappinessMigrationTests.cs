using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Observability;

/// <summary>T4.19 lane A2 — happiness and migration explanations read what
/// the public reader and the migration rows say, nothing else.</summary>
public class ExplainHappinessMigrationTests
{
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
