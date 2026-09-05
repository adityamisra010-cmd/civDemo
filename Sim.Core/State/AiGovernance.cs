using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// M5 — AUTONOMOUS GOVERNANCE FOR AI EMPIRES, and D-021's valve 6.
///
/// WHY THIS IS A PURE FUNCTION AND NOT A SYSTEM. The constitution requires that
/// player and AI use the SAME downstream pathway, and the player's pathway is the
/// order log. So the AI is an ORDER PRODUCER standing exactly where the player
/// stands — outside the turn, deciding between turns — rather than a privileged
/// system that reaches into state the player cannot touch. Its output is
/// `OrderRecord`s that go through `OrderValidation` and `GovernanceSystem` like
/// anyone else's. There is no AI-only verb and no AI-only write path.
///
/// D-021 VALVE 6 — "THE STATE ACTS BY DEFAULT". The doctrine is explicit that
/// player inaction is not world inaction: governors and local elites respond with
/// their own competence, so order gets restored badly rather than not at all.
/// That is precisely this function. An AI Empire watching its legitimacy fall
/// EASES the levy; one governing a contented realm tightens it. The valve is real
/// because the response is a real order with real downstream consequences, not a
/// number that decays on its own.
///
/// DETERMINISM. A pure function of `Prev` state and config: no RNG, no clock, no
/// dictionary iteration, no LINQ. Empires are visited in `Polities` row order and
/// the emitted orders are in that order, so the same world always yields the same
/// orders in the same sequence.
///
/// COMPETENCE, NOT CHEATING. The AI reads only what the player could read — its
/// own legitimacy, computed from settlements it controls. It gets no hidden
/// resources, no extra information and no actions the player lacks. It is
/// deliberately simple: a rule-based chooser is what M5 requires, and a deeper
/// strategic AI belongs to the milestone that has something strategic to decide.
/// </summary>
public static class AiGovernance
{
    /// <summary>
    /// The legitimacy above which an AI tightens the levy and below which it eases.
    /// Denominated on the same 0..100 scale as happiness; the midpoint is the
    /// neutral reading — a realm neither content nor disaffected.
    /// </summary>
    public const double ComfortableLegitimacy = 60.0;

    /// <summary>Legitimacy below which the state is in trouble and retrenches.</summary>
    public const double TroubledLegitimacy = 35.0;

    /// <summary>How much an AI moves its rate in one decision, in percentage points.
    /// Small and monotone: governments adjust taxes, they do not reinvent them
    /// every decade, and a small step keeps the response legible in the annals.</summary>
    public const double StepPercent = 5.0;

    /// <summary>The highest rate an AI will ever declare, in percent. Not a rule of
    /// the world — the player may legislate anything legal — but a statement that
    /// an AI does not tax itself into revolt on purpose.</summary>
    public const double MaxAiRatePercent = 40.0;

    /// <summary>
    /// The governance orders every AI Empire would issue for <paramref name="turn"/>.
    ///
    /// One order per AI Empire that wants to change its rate; an Empire already at
    /// the rate it wants emits NOTHING, so a settled world produces an empty batch
    /// rather than a stream of no-op orders cluttering the log.
    ///
    /// Extinct Empires are skipped: an Empire holding no settlements has no realm
    /// to tax and no legitimacy to read.
    /// </summary>
    public static List<OrderRecord> ChooseOrders(
        IReadOnlyWorldState world, SimConfig cfg, long turn)
    {
        var orders = new List<OrderRecord>();

        for (int p = 0; p < world.Polities.Count; p++)
        {
            PolityRow row = world.Polities[p];
            if (row.Source != CommandSource.Ai) continue;          // the human decides for themselves
            if (EmpireQuery.IsExtinct(world, row.Id)) continue;    // nothing to govern

            double current = Governance.NominalTaxRate(world, row.Id) * 100.0;
            double target = TargetRatePercent(world, row.Id, cfg, current);

            // Only speak when it changes something. The comparison is exact
            // because both sides are built from the same arithmetic.
            if (target == current) continue;

            orders.Add(OrderRecord.From(turn, row.Id, OrderKind.SetTaxRate, row.Id.Value, target));
        }

        return orders;
    }

    /// <summary>
    /// The rate this Empire wants next, in percent, given how it is regarded.
    ///
    /// A deliberately boring rule, and boring is the specification: raise the levy
    /// while the realm is content, ease it while the realm is disaffected, and sit
    /// still in between. The dead band between the two thresholds is what stops an
    /// Empire oscillating one step up and one step down forever — the same reason
    /// class emergence uses a hysteresis latch rather than a single threshold.
    /// </summary>
    public static double TargetRatePercent(
        IReadOnlyWorldState world, PolityId polity, SimConfig cfg, double currentPercent)
    {
        double legitimacy = Governance.Legitimacy(world, polity, cfg);

        double next = currentPercent;
        if (legitimacy >= ComfortableLegitimacy) next = currentPercent + StepPercent;
        else if (legitimacy < TroubledLegitimacy) next = currentPercent - StepPercent;

        return Math.Clamp(next, 0.0, MaxAiRatePercent);
    }
}
