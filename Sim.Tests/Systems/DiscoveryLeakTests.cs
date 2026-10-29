using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M6 Track EX (D-062 D10; ADR-044) — METAMORPHIC NON-INTERFERENCE: perturbing what a polity has NOT discovered — an
/// undiscovered concealed deposit at its own settlement, an unseen rival settlement's books, a rival's land in unknown
/// nodes — leaves every reader of its information class unchanged (the AI's orders, the action surface's descriptors, the
/// labour goods, the info card), and each test carries a POSITIVE CONTROL: the same reader DOES change under a perturbation
/// of something the polity knows, or under the same perturbation with the fog and concealment off — so an identical answer
/// means the information was not read, not that the reader is blind.
/// </summary>
public class DiscoveryLeakTests
{
    private static readonly PolityId Player = new(1), Ai = new(2);
    private static readonly WorldgenConfig Wg = TestConfigs.DevWorldgen() with { AiEmpires = 1 };

    private static WorldState Duo(SimConfig cfg) => WorldFounding.Found(Wg, cfg, 42);

    private static string Orders(IReadOnlyWorldState w, SimConfig cfg) => string.Join(";", AiOrders.For(w, cfg, Wg));

    private static ActionDescriptor[] Actions(IReadOnlyWorldState w, SimConfig cfg, PolityId p) =>
        [.. AvailableActionsQuery.For(w, cfg, p)];

    private static string Extraction(IReadOnlyWorldState w, SimConfig cfg, SettlementId s) =>
        string.Join(",", LabourActivities.GoodsOf(w, cfg.Goods, s, Sectors.Extraction, cfg.Research, cfg).Names);

    private static int DepositRow(IReadOnlyWorldState w, SettlementId s, int good)
    {
        for (int i = 0; i < w.Deposits.Count; i++) if (w.Deposits[i].Settlement == s && w.Deposits[i].Good.Value == good) return i;
        return -1;
    }

    private static WorldState Scaled(WorldState w, int row, double factor)
    {
        WorldState c = w.Clone();
        c.Deposits[row] = c.Deposits[row] with { Abundance = c.Deposits[row].Abundance * factor };
        return c;
    }

    [Fact]
    public void AnUndiscoveredConcealedDeposit_Perturbed_ChangesNoReaderOfItsController_PositiveControlsDo()
    {
        SimConfig cfg = TestConfigs.Sim();
        foreach (PolityId viewer in new[] { Player, Ai })
        {
            // Only the ores are concealable (ADR-044 §3), about a quarter of the eligible rows: the first of the pinned
            // seeds whose founding world gives this viewer a concealed ore at a settlement it controls.
            WorldState w = null!;
            SettlementId s = default;
            int good = -1;
            foreach (ulong seed in new ulong[] { 42, 1, 2, 7 })
            {
                w = WorldFounding.Found(Wg, cfg, seed);
                for (int i = 0; i < w.Settlements.Count && good < 0; i++)
                {
                    SettlementId id = w.Settlements[i].Id;
                    if (!EmpireQuery.ControlsSettlement(w, viewer, id)) continue;
                    // ADR-056: a base-pool ore (no economy packet). A packet ore (iron-ore, coal) is not WORKED until its
                    // knowledge gate is met, so the labour-surface positive control below could not see it either way.
                    int[] hidden = Array.FindAll(DepositConcealment.ConcealedGoods(w.Deposits, w.Seed, cfg, id),
                        g => cfg.Goods!.ById(g).Packet is null);
                    if (hidden.Length > 0) { s = id; good = hidden[0]; }
                }
                if (good >= 0) break;
            }
            Assert.True(good >= 0, "no pinned seed conceals an ore at a settlement of polity " + viewer.Value);
            int row = DepositRow(w, s, good);
            WorldState doubled = Scaled(w, row, 2.0);
            Assert.NotEqual(WorldHash.ComputeHex(w), WorldHash.ComputeHex(doubled));   // the perturbation is real
            Assert.Equal(DepositConcealment.ConcealedGoods(w.Deposits, w.Seed, cfg, s), DepositConcealment.ConcealedGoods(doubled.Deposits, doubled.Seed, cfg, s));
            Assert.Equal(Orders(w, cfg), Orders(doubled, cfg));
            Assert.Equal(Actions(w, cfg, viewer), Actions(doubled, cfg, viewer));
            Assert.Equal(Extraction(w, cfg, s), Extraction(doubled, cfg, s));
            Assert.Equal(InfoQuery.Card(w, cfg, viewer, InfoSubject.OfGood(good)), InfoQuery.Card(doubled, cfg, viewer, InfoSubject.OfGood(good)));

            // POSITIVE CONTROL 1: with concealment off the same row is visible, and removing it changes the labour goods.
            SimConfig off = TestConfigs.PreDiscovery(cfg);
            Assert.NotEqual(Extraction(w, off, s), Extraction(Scaled(w, row, 0.0), off, s));
            // POSITIVE CONTROL 2: under concealment, a VISIBLE deposit of the same settlement removed changes the reader.
            int visible = -1;
            for (int i = 0; i < w.Deposits.Count && visible < 0; i++)
                if (w.Deposits[i].Settlement == s && w.Deposits[i].Abundance > 0.0 && w.Deposits[i].Good.Value != good
                    && !DepositConcealment.IsConcealable(cfg, w.Deposits[i].Good.Value) && !LabourActivities.IsHerded(cfg.Goods!, w.Deposits[i].Good.Value))
                    visible = i;
            Assert.True(visible >= 0);
            Assert.NotEqual(Extraction(w, cfg, s), Extraction(Scaled(w, visible, 0.0), cfg, s));
        }
    }

    [Fact]
    public void AnUnseenRivalSettlement_ItsBooksPerturbed_ChangesNoAiOrderAndNoActionOfTheViewer_PositiveControlFogOff()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Duo(cfg);
        // The player's settlement the AI has not seen.
        SettlementId unseen = default;
        bool found = false;
        PolityKnowledge ai = PolityKnowledge.Of(w, Ai, cfg);
        for (int i = 0; i < w.Settlements.Count && !found; i++)
        {
            SettlementId id = w.Settlements[i].Id;
            if (EmpireQuery.ControlsSettlement(w, Player, id) && !ai.KnowsSettlement(id)) { unseen = id; found = true; }
        }
        Assert.True(found);
        WorldState perturbed = w.Clone();
        GovernanceRigs.AddStock(perturbed, unseen, new GoodId(cfg.Goods!.GrainId), 50_000);
        GovernanceRigs.AddStock(perturbed, unseen, new GoodId(cfg.Goods.IdOf("tools")), 500);
        Assert.NotEqual(WorldHash.ComputeHex(w), WorldHash.ComputeHex(perturbed));
        Assert.Equal(Orders(w, cfg), Orders(perturbed, cfg));
        Assert.Equal(Actions(w, cfg, Ai), Actions(perturbed, cfg, Ai));
        Assert.False(PolityKnowledge.Of(perturbed, Ai, cfg).KnowsSettlement(unseen));
        // POSITIVE CONTROL: with the fog off the AI knows the settlement (the seam is not blind to it).
        Assert.True(PolityKnowledge.Of(w, Ai, TestConfigs.PreDiscovery(cfg)).KnowsSettlement(unseen));
    }

    [Fact]
    public void ARivalsLandInUnknownNodes_Perturbed_DoesNotReachTheKnowledgeOrTheFoundingPreFilter_PositiveControlKnownNode()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Duo(cfg);
        PolityKnowledge k = PolityKnowledge.Of(w, Ai, cfg);
        int l = k.LatticeSize;
        int unknown = -1, known = -1;
        for (int n = 0; n < l * l && (unknown < 0 || known < 0); n++)
        {
            if (unknown < 0 && !k.Known(n)) unknown = n;
            if (known < 0 && k.Known(n) && !k.Visible(n)) known = n;
        }
        Assert.True(unknown >= 0 && known >= 0);
        // A rival's (the player's) extent grows over the AI's unknown node: the AI's knowledge is unchanged.
        WorldState perturbed = w.Clone();
        perturbed.KnownExtents.Add(new KnownExtentRow(Player, unknown >> 6, 1UL << (unknown & 63)));
        PolityKnowledge k2 = PolityKnowledge.Of(perturbed, Ai, cfg);
        for (int n = 0; n < l * l; n++) { Assert.Equal(k.Known(n), k2.Known(n)); Assert.Equal(k.Visible(n), k2.Visible(n)); }
        Assert.Equal(Orders(w, cfg), Orders(perturbed, cfg));
        // POSITIVE CONTROL: the AI's OWN extent growing over that node changes what it knows.
        WorldState own = w.Clone();
        own.KnownExtents.Add(new KnownExtentRow(Ai, unknown >> 6, 1UL << (unknown & 63)));
        Assert.True(PolityKnowledge.Of(own, Ai, cfg).Known(unknown));
    }
}
