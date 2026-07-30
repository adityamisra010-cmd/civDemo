using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.8 ITEM 1 — THE BEFORE-COLUMN, MEASURED NOT CITED (T3.4c M1/M2
/// precedent). Replays the director's T3.9a gate session order log
/// (Fixtures/t38-director-orders.bin: Mothian farm 7% @ t29; Hikiavur farm
/// 17%@59, 98%@104, 33%@122, 6%@142, 100%@174) on MAIN's world — seed 42,
/// canonical 1024², N = 12. PROVENANCE: the gate's cited numbers (Shelter
/// 0.00 / grievance 119.55 etc.) came from the PRE-T3.6b world
/// (endowmentJitter 0.25 — the uploaded chronicle's founding populations
/// match that world exactly); this rig measures what TODAY's world does
/// under the same orders, which is the honest baseline the packet's
/// after-column compares against. Also carries the Item 3 Comfort
/// measurement (is Comfort flow-bound the same way?) via the same run.
///
/// INVALIDATION CONDITIONS (Q-F discipline, from birth): the recorded
/// numbers are invalidated by ANY change to worldgen, siting, endowment
/// jitter, the sector mix or LaborAllocation semantics, the needs registry
/// (baskets, weights, tiers, CES), consumption/production rates, or the
/// grievance integration — re-run before citing after touching any of
/// those. (The T3.8 packet itself invalidates the SHELTER rows by design;
/// that is the point.)
///
/// Skip-gated after the measurement (~7 min: canonical worldgen + 190
/// turns); docs/t3.8-review-record.md records the table.
/// </summary>
public class HousingBeforeColumnTests
{
    private const int Hikiavur = 4;
    private const int Mothian = 11;

    [Fact(Skip = "T3.8 before/after-column measurement rig (~2 min: canonical worldgen + 190 turns) — run manually; docs/t3.8-review-record.md records both tables")]
    public void BeforeColumn_DirectorSessionReplay_OnMain()
    {
        SimConfig cfg = TestConfigs.Sim();
        using var orderStream = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "t38-director-orders.bin"));
        OrderLog orders = OrderLog.Load(orderStream);

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), orders);
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        OrderValidation.ValidateAgainstWorld(orders, world);

        var sb = new System.Text.StringBuilder();
        for (int t = 1; t <= 190; t++)
        {
            world = exec.Step(world);
            if (t == 29) Snapshot(sb, world, cfg, t, Mothian);
            if (t == 177)
            {
                Snapshot(sb, world, cfg, t, Hikiavur);
                Snapshot(sb, world, cfg, t, Mothian);
            }
        }
        File.WriteAllText("/tmp/t38-before.txt", sb.ToString());
        Assert.True(sb.Length > 0);
    }

    private static void Snapshot(System.Text.StringBuilder sb, WorldState w, SimConfig cfg,
        int turn, int s)
    {
        var inv = CultureInfo.InvariantCulture;
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement.Value == s) pop += w.Buckets[i].Count.Value;
        double farming = 0, herding = 0, extraction = 0, crafting = 0, construction = 0;
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement.Value == s)
            {
                SectorAllocationRow r = w.SectorAllocations[i];
                (farming, herding, extraction, crafting, construction) =
                    (r.Farming, r.Herding, r.Extraction, r.Crafting, r.Construction);
            }
        sb.Append(inv, $"t{turn} s{s}: pop={pop} sectors={farming:F2}/{herding:F2}/{extraction:F2}/{crafting:F2}/{construction:F2}\n");

        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
        {
            NeedSatisfactionRow r = w.NeedSatisfactions[i];
            if (r.Settlement.Value != s) continue;
            sb.Append(inv, $"t{turn} s{s}: class={r.Class.Value} need={r.NeedId} value={r.Value:F4}\n");
        }
        double grievance = 0;
        for (int i = 0; i < w.Grievances.Count; i++)
            if (w.Grievances[i].Settlement.Value == s) grievance += w.Grievances[i].Value;
        sb.Append(inv, $"t{turn} s{s}: grievanceSum={grievance:F2}\n");

        // T3.8 after-column extension (a strict SUPERSET of the before
        // instrument — shared fields stay comparable): the housing observables
        // (H1/H2) and the catchment density decomposition inputs (H3).
        for (int i = 0; i < w.Housing.Count; i++)
        {
            HousingRow r = w.Housing[i];
            if (r.Settlement.Value != s) continue;
            sb.Append(inv, $"t{turn} s{s}: dwellings={r.Dwellings.Value} maintFraction={r.LastMaintenanceFraction:F4} laborUsed={r.LastLaborUsed:F2}\n");
        }
        for (int i = 0; i < w.CatchmentSummaries.Count; i++)
        {
            CatchmentSummaryRow r = w.CatchmentSummaries[i];
            if (r.Settlement.Value != s) continue;
            sb.Append(inv, $"t{turn} s{s}: catchNodes={r.NodeCount} arableKm2={r.EffectiveArableKm2:F1} sizeTier={r.SizeTier}\n");
        }

        foreach (string g in new[] { "grain", "timber", "stone", "clay", "pottery", "cloth" })
        {
            int id = cfg.Goods!.IdOf(g);
            for (int i = 0; i < w.GoodStocks.Count; i++)
            {
                GoodStockRow r = w.GoodStocks[i];
                if (r.Settlement.Value != s || r.Good.Value != id) continue;
                sb.Append(inv, $"t{turn} s{s}: {g} stock={r.Amount.Value} produced={r.LastProducedUnits} "
                    + $"consDemand={r.LastConsumptionDemandUnits} eaten={r.LastConsumptionEatenUnits}\n");
            }
        }
    }
}
