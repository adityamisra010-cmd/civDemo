using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// One-call exact conservation check (law 1): for every conserved quantity,
/// Σ world stocks + Σ sunk − Σ sourced must equal 0 — exactly, no epsilon.
/// The stock enumeration below is the M0 registry of where each quantity lives;
/// EXTEND IT when a new conserved table lands (the T0.8 harness runs this every
/// turn, so an un-audited stock is caught the moment it drifts).
/// All arithmetic is checked — an overflowing audit is itself a failure.
/// The audit proves bookkeeping consistency, not rate truth — recorded-but-wrong
/// flows balance. Per-flow exactness tests own rate truth (T1.5 precedent).
/// </summary>
public static class ConservationAuditor
{
    public readonly record struct QuantityAudit(
        ConservedQuantityId Quantity, long StockTotal, long TotalSourced, long TotalSunk)
    {
        /// <summary>Exact identity: stocks + sunk − sourced == 0. Checked — an
        /// audit whose identity arithmetic overflows is itself a failure.</summary>
        public bool IsConserved
        {
            get { checked { return StockTotal + TotalSunk - TotalSourced == 0; } }
        }
    }

    /// <summary>Audits every known quantity; true only if ALL conserve exactly.
    /// T3.2: the retired Food quantity is replaced by ONE AUDIT PER GOOD —
    /// every good id present in the stocks table or the flow table is audited
    /// (an un-audited goods stock cannot exist: rows carry their good id).
    /// Pass the goods registry via the overload to audit ALL fourteen BY NAME
    /// even when a good has no rows yet.</summary>
    public static bool IsConserved(IReadOnlyWorldState world, out string report) =>
        IsConserved(world, null, out report);

    public static bool IsConserved(
        IReadOnlyWorldState world, Sim.Core.Systems.GoodsConfig? goods, out string report)
    {
        QuantityAudit biomass = AuditQuantity(world, ConservedQuantityIds.Biomass);
        QuantityAudit toyGood = AuditQuantity(world, ConservedQuantityIds.ToyGood);
        QuantityAudit population = AuditQuantity(world, ConservedQuantityIds.Population);
        QuantityAudit dwellings = AuditQuantity(world, ConservedQuantityIds.Dwellings);

        // Distinct good ids, ascending (law 5: no set iteration — collect and sort).
        var goodIds = new List<int>();
        if (goods is not null)
        {
            foreach (Sim.Core.Systems.GoodEntry g in goods.Goods) goodIds.Add(g.Id);
        }
        else
        {
            for (int i = 0; i < world.GoodStocks.Count; i++)
            {
                int id = world.GoodStocks[i].Good.Value;
                if (!goodIds.Contains(id)) goodIds.Add(id);
            }
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                if (!ConservedQuantityIds.IsGood(world.LedgerFlows[i].Quantity)) continue;
                int id = ConservedQuantityIds.GoodOf(world.LedgerFlows[i].Quantity).Value;
                if (!goodIds.Contains(id)) goodIds.Add(id);
            }
            goodIds.Sort();
        }

        bool ok = biomass.IsConserved && toyGood.IsConserved && population.IsConserved
            && dwellings.IsConserved;
        var bad = new List<string>();
        if (!biomass.IsConserved) bad.Add($"biomass: {Describe(biomass)}");
        if (!toyGood.IsConserved) bad.Add($"toyGood: {Describe(toyGood)}");
        if (!population.IsConserved) bad.Add($"population: {Describe(population)}");
        if (!dwellings.IsConserved) bad.Add($"dwellings: {Describe(dwellings)}");
        foreach (int id in goodIds)
        {
            QuantityAudit a = AuditQuantity(world, ConservedQuantityIds.OfGood(new GoodId(id)));
            if (a.IsConserved) continue;
            ok = false;
            string name = goods is not null ? goods.ById(id).Name
                : $"good[{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            bad.Add($"{name}: {Describe(a)}");
        }
        report = ok
            ? "conserved: all quantities balance exactly."
            : "CONSERVATION VIOLATION — " + string.Join("; ", bad);
        return ok;
    }

    public static QuantityAudit AuditQuantity(IReadOnlyWorldState world, ConservedQuantityId quantity)
    {
        long stocks = 0;
        checked
        {
            if (quantity == ConservedQuantityIds.Biomass)
            {
                for (int i = 0; i < world.Biomass.Count; i++)
                    stocks += world.Biomass[i].Biomass.Value;
            }
            else if (quantity == ConservedQuantityIds.ToyGood)
            {
                for (int i = 0; i < world.Goods.Count; i++)
                    stocks += world.Goods[i].Amount.Value;
            }
            else if (quantity == ConservedQuantityIds.Population)
            {
                for (int i = 0; i < world.Buckets.Count; i++)
                    stocks += world.Buckets[i].Count.Value;
            }
            else if (quantity == ConservedQuantityIds.Dwellings)
            {
                for (int i = 0; i < world.Housing.Count; i++)
                    stocks += world.Housing[i].Dwellings.Value;
            }
            else if (ConservedQuantityIds.IsGood(quantity))
            {
                GoodId good = ConservedQuantityIds.GoodOf(quantity);
                for (int i = 0; i < world.GoodStocks.Count; i++)
                    if (world.GoodStocks[i].Good == good)
                        stocks += world.GoodStocks[i].Amount.Value;
            }

            long sourced = 0, sunk = 0;
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity != quantity) continue;
                sourced += row.TotalSourced;
                sunk += row.TotalSunk;
            }
            return new QuantityAudit(quantity, stocks, sourced, sunk);
        }
    }

    private static string Describe(QuantityAudit a)
    {
        checked
        {
            return $"stocks {a.StockTotal} + sunk {a.TotalSunk} - sourced {a.TotalSourced} = " +
                   $"{a.StockTotal + a.TotalSunk - a.TotalSourced}";
        }
    }
}
