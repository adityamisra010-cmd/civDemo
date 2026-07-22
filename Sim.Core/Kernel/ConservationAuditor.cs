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

    /// <summary>Audits every known quantity; true only if ALL conserve exactly.</summary>
    public static bool IsConserved(IReadOnlyWorldState world, out string report)
    {
        QuantityAudit biomass = AuditQuantity(world, ConservedQuantityIds.Biomass);
        QuantityAudit toyGood = AuditQuantity(world, ConservedQuantityIds.ToyGood);
        QuantityAudit population = AuditQuantity(world, ConservedQuantityIds.Population);
        QuantityAudit food = AuditQuantity(world, ConservedQuantityIds.Food);

        bool ok = biomass.IsConserved && toyGood.IsConserved
                  && population.IsConserved && food.IsConserved;
        report = ok
            ? "conserved: all quantities balance exactly."
            : "CONSERVATION VIOLATION — " +
              $"biomass: {Describe(biomass)}; toyGood: {Describe(toyGood)}; " +
              $"population: {Describe(population)}; food: {Describe(food)}";
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
                for (int i = 0; i < world.PopBands.Count; i++)
                    stocks += world.PopBands[i].Count.Value;
            }
            else if (quantity == ConservedQuantityIds.Food)
            {
                for (int i = 0; i < world.FoodStores.Count; i++)
                    stocks += world.FoodStores[i].Store.Value;
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
