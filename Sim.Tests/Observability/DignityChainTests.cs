using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// F1 (2026-10-05, the H2 verifier's finding on the Glass Box; d049 §15) — the Dignity causal chain states the SHIPPED
/// felt-burden formula (declared rate, state capacity, provision, services) and links the levy-grievance stock the
/// felt burden accrues into, with values equal to the public readers.
/// </summary>
public class DignityChainTests
{
    [Fact]
    public void TheDignityChain_StatesTheShippedFormula_AndLinksTheLevyGrievanceStock()
    {
        SimConfig cfg = TestConfigs.Sim();
        (WorldState w, PolityId p) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, p);
        SettlementId seat = GovernanceRigs.Seat(w, p);
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.7));
        w.Structures.Add(new StructureRow(seat, 1, 1));   // a granary: V > 0
        var peasants = new ClassId(1);
        WorldState next = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(cfg)).Step(w);

        int dignity = -1;
        foreach (NeedEntry n in cfg.Needs!.Needs) if (n.FromTaxBurden) dignity = n.Id;
        CausalChain chain = CausalChain.ForNeed(w, next, cfg, seat, peasants, dignity);

        Link Of(ChainNode node) => Assert.Single(chain.Links, l => l.Node == node);
        string formula = Of(ChainNode.DignitySatisfaction).Note;
        foreach (string term in new[] { "taxCapacityOffsetMax", "taxBurdenOffsetMax", "taxServiceOffsetMax", "DECLARED", "reach" })
            Assert.Contains(term, formula);
        Assert.DoesNotContain("s = 1 − r × (1 − offsetMax × P)", formula);   // the stale R4a statement

        Assert.Equal(0.7, Of(ChainNode.DeclaredTaxRate).Value);
        Assert.Equal(Governance.ControlStrength(w, p, seat), Of(ChainNode.AdministrativeReach).Value);
        Assert.Equal(Unrest.ServiceOffset(w, seat, cfg), Of(ChainNode.ServiceOffset).Value);
        Assert.True(Of(ChainNode.ServiceOffset).Value > 0.0);
        Assert.Equal(Governance.EffectiveTaxRate(w, seat, cfg), Of(ChainNode.EffectiveTaxRate).Value);
        Link levy = Of(ChainNode.LevyGrievance);
        Assert.Equal(Unrest.SegmentTaxGrievance(next, seat, peasants), levy.Value);
        Assert.True(levy.Value > 0.0, "the levy accrued nothing in a 10-year turn at 70 %");
        Assert.Equal("TaxGrievances", levy.SourceTable);
        Assert.Equal(next.TaxGrievances[levy.SourceIndex], new TaxGrievanceRow(seat, peasants, levy.Value));
        Assert.True(Levers.For(ChainNode.LevyGrievance).IsNone);
    }
}
