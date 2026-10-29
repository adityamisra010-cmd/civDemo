using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Core.Systems.Diffusion;

/// <summary>The table owned by <see cref="DiffusionSystem"/> (ADR-058): the foreign-exposure seam of ADR-029 addendum A,
/// of which it is the ONE writer.</summary>
public sealed record DiffusionTables(Table<ResearchExposureRow> Exposures);

/// <summary>
/// ADR-058 (D-066 A7) — CONTACT-CARRIED FOREIGN EXPOSURE: the writer of <see cref="ResearchExposureRow"/>, the input seam
/// ResearchSystem has paid from since ADR-029 addendum A. SystemId 31, last in pipeline.json: it reads only PREV and writes
/// only ResearchExposures, which ResearchSystem reads from PREV — so its slot changes no other system's result, and an
/// offer written in step t→t+1 is paid in step t+1→t+2 (the one-step lag of single-writer ownership, pinned turn-exactly).
///
/// THE STEP, everything from PREV, for every Empire of the roster in table order (a doubled roster row is one Empire):
/// <list type="number">
/// <item><b>Contact.</b> The partner settlements of its foreign trade flows that it KNOWS
///   (<see cref="DiffusionQuery.ContactPartners"/>: no exposure from a settlement it has not sighted — no information
///   leak), and the union of their knowledge (<see cref="DiffusionQuery.ContactKnowledge"/>). No contact: nothing.</item>
/// <item><b>Offer.</b> For every node it may research now (ResearchQuery.AvailableMask — the same availability the engine
///   pays on) that a contact partner knows, in content index order: the cumulative offer rises by
///   tuning.foreignExposure.ratePerYear × dtYears × BaseCost (law 3), capped at the acceleration pool's ceiling and never
///   lowered (<see cref="DiffusionQuery.NextOffered"/>). The polity's row for the node carries the new total (one row per
///   (polity, node), appended on the first offer). ResearchSystem then pays the unpaid part from the ONE pool shared with Eureka.</item>
/// </list>
///
/// Exposure is offered only while the node is available, so it is a bounded advance on work the polity can do now (the
/// spec's brake: ≤ the 0.40 pool, ≤ ratePerYear of BaseCost a sim-year), never a completion and never knowledge the
/// polity lacks the prerequisites for. City states are carriers (their knowledge reaches a trading Empire) but not
/// receivers in M6. Conquest (DW-021) and every other pathway are later (D-066 A7).
///
/// INERT with research.json tuning.foreignExposure absent or disabled — no row is ever written (the attribution twin) —
/// and without research content. No RNG, no Ledger (knowledge is none of people, money or goods), no dictionary, no
/// ordering over doubles (law 5).
/// </summary>
public sealed class DiffusionSystem(SimConfig cfg) : ISimSystem<DiffusionTables>
{
    public static readonly SystemId WellKnownId = new(31);
    public const string Name = "diffusion";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<DiffusionTables> ctx)
    {
        if (_cfg.Research is not { } content || content.Tuning.ForeignExposure is not { Enabled: true } rule) return;
        IReadOnlyWorldState prev = ctx.Prev;
        Table<ResearchExposureRow> exposures = ctx.Owned.Exposures;
        double ceiling = content.Tuning.AccelerationCreditCeilingFraction;
        var processed = new List<int>();
        for (int p = 0; p < prev.Polities.Count; p++)
        {
            PolityId receiver = prev.Polities[p].Id;
            if (processed.Contains(receiver.Value)) continue;
            processed.Add(receiver.Value);

            bool[]? carried = DiffusionQuery.ContactKnowledge(prev, content, _cfg, receiver);
            if (carried is null) continue;
            bool[] completed = ResearchQuery.CompletedMask(prev, content, receiver);
            bool[] available = ResearchQuery.AvailableMask(content, completed, ResearchQuery.LevelMask(prev, content, receiver, completed));
            for (int i = 0; i < content.Nodes.Count; i++)
            {
                if (!available[i] || !carried[i]) continue;
                ResearchNode node = content.Nodes[i];
                double offered = ResearchQuery.ExposureOffered(prev, receiver, node.Key);
                double next = DiffusionQuery.NextOffered(offered, node.BaseCost, ceiling, rule.RatePerYear, ctx.DtYears);
                if (!(next > offered)) continue;
                Set(exposures, receiver, node.Key, next);
            }
        }
    }

    /// <summary>Sets the polity's cumulative offer for the node to <paramref name="total"/>: its FIRST row carries total
    /// minus whatever its other rows hold (ResearchQuery.ExposureOffered sums every row; with one row — the writer never
    /// makes a second — that row IS the total, exactly), or a new row is appended.</summary>
    private static void Set(Table<ResearchExposureRow> exposures, PolityId polity, ResearchNodeId node, double total)
    {
        int first = -1;
        double others = 0.0;
        for (int r = 0; r < exposures.Count; r++)
        {
            ResearchExposureRow row = exposures[r];
            if (row.Polity.Value != polity.Value || row.Node.Value != node.Value) continue;
            if (first < 0) first = r;
            else others += row.Offered;
        }
        if (first < 0) exposures.Add(new ResearchExposureRow(polity, node, total));
        else exposures.Ref(first).Offered = total - others;
    }
}
