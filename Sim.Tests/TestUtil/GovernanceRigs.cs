using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>
/// ADR-033 D4 — rigs for the governing loop (ported from <c>m5-full-build</c>'s GovernanceTests
/// helpers). Every conserved stock a rig changes moves through the Ledger (law 1), and the research
/// gate is opened only by completing a REAL taxation node in the world (never by a config bypass).
/// </summary>
internal static class GovernanceRigs
{
    public const string TaxationNode = "arithmetic_babylonian";   // "tax assessment" — the gate's cheapest node

    public static SimConfig Cfg() => TestConfigs.Sim();

    public static EraTable FlatEra(double dtYears = 10.0) => ResearchRigs.FlatEra(dtYears);

    /// <summary>A founded dev world (256², 4 settlements by default) and its player Empire.</summary>
    public static (WorldState World, PolityId Player) Founded(int settlements = 4, int aiEmpires = 0)
    {
        WorldState w = WorldFounding.Found(
            TestConfigs.DevWorldgen() with { AiEmpires = aiEmpires }, Cfg(), 42, settlements);
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return (w, w.Polities[i].Id);
        throw new InvalidOperationException("founded world has no player Empire");
    }

    /// <summary>Completes <paramref name="node"/> for <paramref name="polity"/> — the constructed-knowledge
    /// rig (a ResearchCompleted row, exactly what ResearchSystem writes on completion).</summary>
    public static void Grant(WorldState w, PolityId polity, string node = TaxationNode)
    {
        global::Sim.Core.Systems.Research.ResearchContent content = TestConfigs.Research();
        int index = content.IndexOfId(node);
        if (index < 0) throw new ArgumentException($"no research node '{node}'", nameof(node));
        w.ResearchCompleted.Add(new ResearchCompletedRow(polity, content.Nodes[index].Key));
    }

    public static OrderRecord SetTax(long turn, PolityId p, double percent) => Governance.TaxOrder(turn, p, percent);

    public static SettlementId Seat(WorldState w, PolityId p)
    {
        if (!EmpireQuery.TryGetCapital(w, p, out SettlementId seat)) throw new InvalidOperationException("no capital");
        return seat;
    }

    /// <summary>An executor running ONLY the governance system.</summary>
    public static TurnExecutor GovernanceOnly(OrderLog orders, SimConfig? cfg = null) =>
        new(FlatEra(), [SystemCatalog.Governance(cfg ?? Cfg())], orders);

    public static long Population(IReadOnlyWorldState w, SettlementId s)
    {
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s) pop += w.Buckets[i].Count.Value;
        return pop;
    }

    /// <summary>Drives the settlement's dwelling stock to <paramref name="target"/> through the Ledger.</summary>
    public static void SetDwellings(WorldState w, SettlementId s, long target)
    {
        int row = -1;
        for (int i = 0; i < w.Housing.Count; i++)
            if (w.Housing[i].Settlement == s) { row = i; break; }
        if (row < 0)
        {
            w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            row = w.Housing.Count - 1;
        }
        long delta = target - w.Housing[row].Dwellings.Value;
        if (delta == 0) return;
        new Ledger(w.LedgerFlows).Flow(
            ref w.Housing.Ref(row).Dwellings, ConservedQuantityIds.Dwellings,
            ReasonIds.InitialEndowment, Math.Abs(delta),
            delta > 0 ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    /// <summary>Sources <paramref name="units"/> more of <paramref name="good"/> at a settlement, through the Ledger.</summary>
    public static void AddStock(WorldState w, SettlementId s, GoodId good, long units)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Settlement != s || w.GoodStocks[i].Good != good) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(good),
                ReasonIds.InitialEndowment, units, FlowDirection.Source, OverdrawPolicy.Throw);
            return;
        }
        throw new InvalidOperationException($"no stock row for good {good.Value} at settlement {s.Value}");
    }

    public static void SetDeficit(WorldState w, SettlementId s, double ratio)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
        {
            if (w.ConsumptionDeficits[i].Settlement != s) continue;
            w.ConsumptionDeficits[i] = w.ConsumptionDeficits[i] with { DeficitRatio = ratio };
            return;
        }
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, ratio, 1000));
    }

    /// <summary>Unfed and unhoused: every provision factor at zero.</summary>
    public static void Destitute(WorldState w, SettlementId s)
    {
        SetDeficit(w, s, 1.0);
        SetDwellings(w, s, 0);
    }

    /// <summary>Fed, and housed beyond its population — a settlement with something to lose.</summary>
    public static void WellProvided(WorldState w, SettlementId s)
    {
        SetDeficit(w, s, 0.0);
        SetDwellings(w, s, Population(w, s) + 100);
    }

    /// <summary>Sets the (polity, place) control row's Strength directly — a rig standing in for
    /// GovernanceSystem's write, used where a test isolates the READERS of the stored reach.</summary>
    public static void SetStrength(WorldState w, PolityId p, SettlementId s, double strength)
    {
        for (int i = 0; i < w.Controls.Count; i++)
        {
            if (w.Controls[i].Polity != p || w.Controls[i].Place != s) continue;
            w.Controls[i] = w.Controls[i] with { Strength = strength };
            return;
        }
        throw new InvalidOperationException($"no control row ({p.Value}, {s.Value})");
    }

    /// <summary>The (From, To) distance row's travel cost set (or added).</summary>
    public static void SetDistance(WorldState w, SettlementId from, SettlementId to, double cost)
    {
        for (int i = 0; i < w.SettlementDistances.Count; i++)
        {
            if (w.SettlementDistances[i].From != from || w.SettlementDistances[i].To != to) continue;
            w.SettlementDistances[i] = new SettlementDistanceRow(from, to, cost);
            return;
        }
        w.SettlementDistances.Add(new SettlementDistanceRow(from, to, cost));
    }
}
