using System.Globalization;
using System.Text;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Observability.Forensic;

/// <summary>
/// ROUTING THE EXPLAIN LAYER HEADLESSLY.
///
/// The deepest causal machinery in the tree — CausalChain, HappinessExplanation,
/// MigrationExplanation, the needs explanation, Levers — was SCREENSHOT-ONLY: a
/// grep of Sim.Cli for those types returned zero. It is the largest mismatch
/// between what the tree already contains and what a headless reviewer can see,
/// and closing it requires no new state, no new arithmetic and no new mechanics.
/// This class is plumbing: it calls the existing public queries and renders
/// them. It computes nothing of its own.
///
/// IT NEEDS TWO WORLDS, and says so. Every explanation is a pure function of
/// (prev, next, cfg) — they read tables off a world, not off a saved record — so
/// unlike everything else in the inspector these answers are RECONSTRUCTED by
/// replaying to the turn asked for. That is stated on the output rather than
/// glossed: an answer derived from a replay is a different kind of evidence from
/// one read off the artifact, and a reviewer must be able to tell them apart.
///
/// IT LIVES IN Sim.Core/Observability because check-read-isolation.sh allowlists
/// that path prefix and scans Sim.Cli — including PROSE. Sim.Cli passes a string
/// and prints what comes back.
/// </summary>
public static class ExplainPrinter
{
    /// <summary>The kinds `--explain` accepts.</summary>
    public static string[] Kinds => ["happiness", "needs", "migration", "chain"];

    public const string ReplayNote =
        "RECONSTRUCTED BY REPLAY, not read from the saved record: the explain layer is a pure function of the "
        + "(prev, next) world pair and this build persists no world. The reconstruction is exact only because "
        + "the replay reproduced the session's own hashes turn for turn — the verdict printed above.";

    /// <summary>
    /// Renders one explanation. <paramref name="cls"/> selects the class for the
    /// needs explanation; the others ignore it.
    /// </summary>
    public static string Render(
        string kind, IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId settlement, ClassId cls, long turn)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        var sb = new StringBuilder();
        sb.Append("--- explain ").Append(kind)
          .Append(": settlement ").Append(settlement.Value.ToString(CultureInfo.InvariantCulture))
          .Append(", turn ").Append(turn.ToString(CultureInfo.InvariantCulture))
          .Append(" ---").Append('\n');
        sb.Append("[DERIVABLE] ").Append(ReplayNote).Append('\n').Append('\n');

        switch (kind)
        {
            case "happiness": Happiness(sb, next, cfg, settlement); break;
            case "needs": Needs(sb, prev, next, cfg, settlement, cls); break;
            case "migration": Migration(sb, prev, next, cfg, settlement); break;
            case "chain": Chain(sb, prev, next, cfg, settlement, cls); break;
            default:
                throw new ArgumentException(
                    $"unknown explain kind '{kind}' — one of: {string.Join(", ", Kinds)}", nameof(kind));
        }
        return sb.ToString();
    }

    private static void Happiness(StringBuilder sb, IReadOnlyWorldState next, SimConfig cfg, SettlementId s)
    {
        HappinessExplanation x = HappinessExplanation.For(next, cfg, s);
        sb.Append("happiness  ").Append(Fmt(x.Happiness))
          .Append("   [KNOWN — the PUBLIC SettlementHappiness.Of, the same reader the migration system asks]\n");
        sb.Append("revolt ready: ")
          .Append(SettlementHappiness.IsRevoltReady(next, s, cfg) ? "YES" : "no")
          .Append("   [KNOWN — the PUBLIC SettlementHappiness.IsRevoltReady, threshold ")
          .Append(Fmt(SettlementHappiness.RevoltThreshold)).Append("]\n\n");

        for (int i = 0; i < x.Factors.Length; i++)
        {
            HappinessFactor f = x.Factors[i];
            sb.Append("factor ").Append(f.Name).Append(" = ").Append(Fmt(f.Value))
              .Append("   [KNOWN — SettlementHappiness.Factors]\n");
            for (int j = 0; j < f.Chain.Length; j++) sb.Append("    ").Append(Link(f.Chain[j])).Append('\n');
            sb.Append('\n');
        }

        sb.Append("SCOPE: ").Append(HappinessExplanation.ScopeNote).Append("\n\n");
        sb.Append("[NOT RECORDED] the DECOMPOSITION of the value — raw factor, normalised factor, per-factor\n");
        sb.Append("weight, per-factor contribution, aggregate. ").Append(ForensicSchema.HappinessDecompositionWhy)
          .Append('\n');
    }

    private static void Needs(
        StringBuilder sb, IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId s, ClassId cls)
    {
        Sim.Core.Observability.Explain.GrievanceExplanation x =
            Sim.Core.Observability.Explain.GrievanceExplanation.For(prev, next, cfg, s, cls);
        sb.Append("class ").Append(x.ClassName)
          .Append("   population ").Append(x.ClassPopulation.ToString(CultureInfo.InvariantCulture))
          .Append(" of ").Append(x.SettlementPopulation.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("stock before ").Append(Fmt(x.Previous))
          .Append("   after ").Append(Fmt(x.Total))
          .Append("   delta ").Append(Fmt(x.Delta))
          .Append("   dtYears ").Append(Fmt(x.DtYears)).Append("   [KNOWN — READ from the owning system's rows]\n");
        sb.Append("stepped by the system this turn: ").Append(x.SteppedBySystem ? "yes" : "no").Append('\n');
        sb.Append("aggregate ").Append(Fmt(x.Aggregate))
          .Append("   accrual ").Append(Fmt(x.Accrual))
          .Append("   decay ").Append(Fmt(x.Decay))
          .Append("   recomputed ").Append(Fmt(x.Recomputed))
          .Append("   [DERIVABLE — a call to the PUBLIC pure function the system itself calls]\n\n");

        for (int i = 0; i < x.Needs.Length; i++)
        {
            NeedComponent n = x.Needs[i];
            sb.Append("  need ").Append(n.Name.PadRight(14))
              .Append(" satisfaction ").Append(Fmt(n.Satisfaction))
              .Append("   weight ").Append(Fmt(n.Weight))
              .Append(n.Bound ? "" : "   (UNBOUND: registered but not simulated)").Append('\n');
        }
        sb.Append('\n');
        sb.Append("[NOT RECORDED] a per-need SHARE of the shortfall. It is not a missing field but a missing\n");
        sb.Append("CONCEPT: the aggregate is a CES with rho < 0 and is non-additive, so any per-need share\n");
        sb.Append("would be defined by the observer, not by the simulation.\n");
        if (x.PrimaryChain is { } chain)
        {
            sb.Append("\nprimary chain (").Append(chain.NeedName).Append("):\n");
            for (int i = 0; i < chain.Links.Length; i++) sb.Append("    ").Append(Link(chain.Links[i])).Append('\n');
        }
    }

    private static void Migration(
        StringBuilder sb, IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, SettlementId s)
    {
        MigrationExplanation x = MigrationExplanation.For(prev, next, cfg, s);
        sb.Append("inflow ").Append(x.Inflow.ToString(CultureInfo.InvariantCulture))
          .Append("   outflow ").Append(x.Outflow.ToString(CultureInfo.InvariantCulture))
          .Append("   [KNOWN — MigrationFlowRow, AGGREGATE across every partner, cohort, class and channel]\n");
        sb.Append("push (source deficit) ").Append(Fmt(x.PushDeficit))
          .Append("   pull (smoothed attractiveness) ")
          .Append(x.PullRecorded ? Fmt(x.Pull) : "no row yet")
          .Append("   [KNOWN — the inputs the mechanism READ]\n\n");

        sb.Append("candidate destinations, by the inputs the mechanism read (NOT by where anyone went):\n");
        for (int i = 0; i < x.Others.Length; i++)
        {
            Destination d = x.Others[i];
            sb.Append("    settlement ").Append(d.Id.Value.ToString(CultureInfo.InvariantCulture).PadLeft(3))
              .Append("   pull ").Append(Fmt(d.SmoothedAttractiveness))
              .Append("   deficit ").Append(Fmt(d.DestinationDeficit))
              .Append("   grain ").Append(d.GrainStock.ToString(CultureInfo.InvariantCulture))
              .Append(" (present: ").Append(d.GrainPresent ? "yes" : "no").Append(')')
              .Append("   happiness ").Append(Fmt(d.Happiness))
              .Append('\n');
        }

        sb.Append("\n[NOT RECORDED] ").Append(ForensicSchema.MigrationPairwiseAnswer).Append('\n');
        sb.Append(ForensicSchema.MigrationPairwiseWhy).Append('\n');
        sb.Append("\nThe list above is the INPUT SET the mechanism read, printed in the explanation layer's own\n");
        sb.Append("order. It is NOT a ranking of where anyone actually went, and must not be read as one.\n");
        sb.Append("gap scale: ").Append(MigrationExplanation.GapScale).Append('\n');
    }

    private static void Chain(
        StringBuilder sb, IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId s, ClassId cls)
    {
        NeedsConfig? needs = cfg.Needs;
        if (needs is null)
        {
            sb.Append("[NOT RECORDED] the needs registry is not loaded in this configuration.\n");
            return;
        }
        for (int i = 0; i < needs.Needs.Length; i++)
        {
            CausalChain chain = CausalChain.ForNeed(prev, next, cfg, s, cls, needs.Needs[i].Id);
            sb.Append("need ").Append(chain.NeedName).Append(":\n");
            for (int j = 0; j < chain.Links.Length; j++) sb.Append("    ").Append(Link(chain.Links[j])).Append('\n');
            Lever lever = Levers.For(chain.Links.Length > 0 ? chain.Links[0].Node : ChainNode.NotSimulated);
            sb.Append("    LEVER: ").Append(lever.Reason).Append('\n').Append('\n');
        }
    }

    /// <summary>One causal link, with the world it was READ from stamped on it —
    /// the A2-LABEL property: a prev reading and a next reading of the same table
    /// are different numbers and each says which it is.</summary>
    private static string Link(Link link) =>
        link.Node.ToString().PadRight(26)
        + Fmt(link.Value).PadLeft(12)
        + "  [" + link.Kind.ToString().ToUpperInvariant() + " / " + link.World.ToString() + "]  "
        + link.Label;

    private static string Fmt(double value)
        => double.IsFinite(value) ? value.ToString("0.####", CultureInfo.InvariantCulture) : "n/a";
}
