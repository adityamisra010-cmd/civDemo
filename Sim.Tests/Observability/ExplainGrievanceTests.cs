using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.19 lane A2 — the grievance explanation on REAL stepped worlds. Every
/// claim here is made on a world the production pipeline produced, never on a
/// fixture the explanation could agree with while the system disagreed.
/// </summary>
public class ExplainGrievanceTests
{
    private const int Peasant = 1;
    private static readonly SettlementId Target = new(ExplainRigs.Target);

    [Fact]
    public void Starved_PrimaryIsSustenance_AndChainShowsHarvestBelowEaten()
    {
        (SimConfig cfg, List<WorldState> worlds, int first) = ExplainRigs.Starved(maxTurns: 12);
        Assert.True(first > 0, "rig vacuous: zero farming and herding never produced a deficit within 12 turns");
        Assert.True(first + 1 < worlds.Count, "rig: no Next after the first deficit world");

        // WHICH TURN, pinned turn-exact (T1.9 precedent: a delivery semantic
        // gets its own pin, because live-vs-replay equality cannot see stamping
        // drift). The batch is stamped Turn 1; PathBuild writes the row in the
        // step 1→2, so world 2 is the FIRST world carrying it and world 1 does
        // not; Production reads it in the step 2→3, so world 3 is the first with
        // zero harvest and a positive deficit. Measured at seed 42 on the dev
        // preset: first = 3, deficit(w3) = 0.7786416647610336 at dt 10 — ALL
        // FOUR asserted, the deficit bit-exact (A2-FIX D5: the first cut
        // pinned the turn and only wrote the value in this comment). If the
        // turn moves, the ORDER-DELIVERY semantic moved — not the explanation;
        // if the value moves at the same turn, consumption or production did.
        // T4.19 lane C RE-PIN: 0.7722222222222223 -> 0.7786416647610336. The
        // turn did NOT move; the value did, because the founding cohort vector
        // changed and the rig founds a different population (the same single
        // cause, with the same turn-0 control, as every founded pin in
        // docs/m4-founding-demographics-correction.md §7).
        Assert.False(ExplainRigs.HasSectorRow(worlds[1], ExplainRigs.Target), "the Turn-1 batch must not be in force in world 1");
        Assert.True(ExplainRigs.HasSectorRow(worlds[2], ExplainRigs.Target), "the Turn-1 batch must land in world 2");
        // T4.21-2 RE-PIN: 0.7786416647610336 -> 0.76919291338582674. The turn did
        // NOT move; the value did, because bounded migration (ADR-025: the turn-2
        // vacancy refusal — turn 1 is the zero-harvest endowment turn, so every
        // settlement reads V = 0 on turn 2 — and the basin caps on the gap
        // channel) leaves the target a different population by the drawdown turn.
        Assert.Equal(3, first);
        Assert.Equal(0.76919291338582674, ExplainRigs.Deficit(worlds[first], ExplainRigs.Target));
        WorldState prev = worlds[first], next = worlds[first + 1];

        GrievanceExplanation g = GrievanceExplanation.For(prev, next, cfg, Target, new ClassId(Peasant));

        // NON-VACUITY: grievance is positive and rising on the class we explain.
        Assert.True(g.Total > 0.0, $"grievance was {g.Total} — nothing to explain");
        Assert.True(g.Delta > 0.0, $"grievance did not rise across the drawdown turn (Δ = {g.Delta})");

        Assert.Equal(BasketBook.SustenanceNeedId, g.PrimaryNeedId);
        Assert.NotNull(g.PrimaryChain);
        Assert.Equal(BasketBook.SustenanceNeedId, g.PrimaryChain!.NeedId);

        // The store-drawdown signature: harvest (zero — no farming) below what was eaten.
        Link harvest = Single(g.PrimaryChain.Links, ChainNode.GrainHarvest);
        Link eaten = Single(g.PrimaryChain.Links, ChainNode.GrainEaten);
        Assert.Equal(LinkKind.Read, harvest.Kind);
        Assert.Equal(LinkKind.Read, eaten.Kind);
        Assert.True(eaten.Value > 0.0, "rig: nothing was eaten on the drawdown turn — the store was already empty");
        Assert.True(harvest.Value < eaten.Value,
            $"chain does not show Harvest < Eaten: harvest {harvest.Value}, eaten {eaten.Value}");
        Assert.Equal(0.0, harvest.Value);   // zero farming was ORDERED; the chain reads it back

        // The lever the chain names for the harvest is the farming slider — the order that caused this.
        Lever lever = Levers.For(harvest.Node);
        Assert.Equal(Sim.Core.Kernel.OrderKind.SectorAllocation, lever.Kind);
        Assert.Contains(Sectors.Farming, lever.Sectors);

        // The Sustenance component is the most unmet bound need, and the gate marks it.
        NeedComponent sustenance = Component(g, BasketBook.SustenanceNeedId);
        Assert.True(sustenance.Bound);
        Assert.True(sustenance.IsTierAGate);
        Assert.True(sustenance.Satisfaction < 0.5, $"Sustenance satisfaction {sustenance.Satisfaction} not below the Tier-A floor");
        Assert.Equal(sustenance.Weight * (1.0 - sustenance.Satisfaction), sustenance.WeightedShortfall);
    }

    [Fact]
    public void Homeless_PrimaryIsShelter()
    {
        (SimConfig cfg, WorldState prev, WorldState next) = ExplainRigs.Homeless(prefixTurns: 3);
        GrievanceExplanation g = GrievanceExplanation.For(prev, next, cfg, Target, new ClassId(Peasant));

        Assert.True(g.Total > 0.0, "grievance zero on a homeless class — nothing to explain");
        NeedComponent shelter = Component(g, 2);
        Assert.True(shelter.Bound);
        Assert.Equal(0.0, shelter.Satisfaction);        // people, dwellings 0 → exactly 0.0
        Assert.Equal(2, g.PrimaryNeedId);

        // The chain reads the zeroed stock back, and the sufficiency reader agrees.
        Assert.NotNull(g.PrimaryChain);
        Link dwellings = Single(g.PrimaryChain!.Links, ChainNode.Dwellings);
        Assert.Equal(LinkKind.Read, dwellings.Kind);
        Assert.Equal(0.0, dwellings.Value);
        Link sufficiency = Single(g.PrimaryChain.Links, ChainNode.HousingSufficiency);
        Assert.Equal(LinkKind.Recomputed, sufficiency.Kind);
        Assert.Equal(0.0, sufficiency.Value);
        // Comfort's weight collapses to zero under a fully unmet gate need, so
        // its lift is exactly zero — the gate is what the ranking respects.
        Assert.Equal(0.0, Component(g, 6).MarginalLift);
    }

    [Fact]
    public void MarginalLift_NonNegative_AndFullyMetNeedLiftsExactlyZero()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(6);
        int fullyMet = 0, positive = 0, classes = 0;
        for (int t = 1; t < worlds.Count; t++)
        {
            foreach ((SettlementId s, ClassId c) in ExplainRigs.GrievanceKeys(worlds[t]))
            {
                GrievanceExplanation g = GrievanceExplanation.For(worlds[t - 1], worlds[t], cfg, s, c);
                if (g.ClassPopulation == 0) continue;
                classes++;
                foreach (NeedComponent n in g.Needs)
                {
                    if (!n.Bound) { Assert.True(double.IsNaN(n.MarginalLift)); continue; }
                    Assert.True(n.MarginalLift >= 0.0, $"turn {t} s{s.Value} c{c.Value} need {n.NeedId}: lift {n.MarginalLift} < 0");
                    if (n.Satisfaction >= 1.0) { fullyMet++; Assert.Equal(0.0, n.MarginalLift); }
                    if (n.MarginalLift > 0.0) positive++;
                }
            }
        }
        Assert.True(classes > 0, "no populated class was explained");
        Assert.True(fullyMet > 0, "vacuous: no need was ever fully met (Shelter on a founded world should be)");
        Assert.True(positive > 0, "vacuous: no need ever had a positive lift");
    }

    /// <summary>
    /// THE DRIFT DETECTOR. Accrual and decay are recomputed from Prev state and
    /// config through the same public gate + aggregate the system calls, with the
    /// same operands in the same association, so the recomputed stock must equal
    /// the system's to the BIT — asserted with exact equality, no tolerance.
    /// Measured on every (settlement, class) of a fed run and of a starved run:
    /// the two diverge only if the system's arithmetic changes without this
    /// observer, which is exactly the event the test exists to catch.
    /// </summary>
    [Fact]
    public void AccrualAndDecay_ReproduceTheSystemsStock_Exactly()
    {
        int checked_ = 0, positive = 0;
        void Check(SimConfig cfg, List<WorldState> worlds)
        {
            for (int t = 1; t < worlds.Count; t++)
            {
                foreach ((SettlementId s, ClassId c) in ExplainRigs.GrievanceKeys(worlds[t]))
                {
                    GrievanceExplanation g = GrievanceExplanation.For(worlds[t - 1], worlds[t], cfg, s, c);
                    Assert.Equal(g.Total, g.Recomputed);
                    Assert.Equal(g.Total - g.Previous, g.Delta);
                    Assert.Equal(worlds[t].Clock.DtYears, g.DtYears);
                    checked_++;
                    if (g.Total > 0.0) positive++;
                }
            }
        }

        (SimConfig fedCfg, List<WorldState> fed) = ExplainRigs.Fed(8);
        Check(fedCfg, fed);
        (SimConfig starvedCfg, List<WorldState> starved, int first) = ExplainRigs.Starved(12);
        Assert.True(first > 0, "rig vacuous: the starved run never starved");
        Check(starvedCfg, starved);

        Assert.True(checked_ > 0, "no (settlement, class) was checked");
        Assert.True(positive > 0, "vacuous: every grievance was zero");
    }

    [Fact]
    public void UnboundNeeds_ListedAsNotSimulated_NeverOmitted()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(2);
        GrievanceExplanation g = GrievanceExplanation.For(worlds[1], worlds[2], cfg, Target, new ClassId(Peasant));

        Assert.Equal(cfg.Needs!.Needs.Length, g.Needs.Length);
        int unbound = 0;
        for (int i = 0; i < g.Needs.Length; i++)
        {
            Assert.Equal(cfg.Needs.Needs[i].Id, g.Needs[i].NeedId);   // registry order, registry ids
            if (cfg.Needs.Needs[i].Bound) continue;
            unbound++;
            Assert.False(g.Needs[i].Bound);
            Assert.Equal("not yet simulated", g.Needs[i].Note);
            Assert.True(double.IsNaN(g.Needs[i].Satisfaction));
            CausalChain chain = CausalChain.ForNeed(worlds[1], worlds[2], cfg, Target, new ClassId(Peasant), g.Needs[i].NeedId);
            Assert.Single(chain.Links);
            Assert.Equal(LinkKind.Gap, chain.Links[0].Kind);
            Assert.Equal(ChainNode.NotSimulated, chain.Links[0].Node);
        }
        Assert.True(unbound > 0, "vacuous: the registry has no unbound need");
        Assert.Contains("not additive", GrievanceExplanation.AttributionNote);
    }

    internal static Link Single(Link[] links, ChainNode node)
    {
        int found = -1;
        for (int i = 0; i < links.Length; i++)
            if (links[i].Node == node) { Assert.True(found < 0, $"node {node} appears more than once"); found = i; }
        Assert.True(found >= 0, $"node {node} absent from the chain");
        return links[found];
    }

    internal static NeedComponent Component(GrievanceExplanation g, int needId)
    {
        for (int i = 0; i < g.Needs.Length; i++) if (g.Needs[i].NeedId == needId) return g.Needs[i];
        Assert.Fail($"need {needId} absent from the explanation");
        return default;
    }
}
