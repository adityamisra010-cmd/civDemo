using Sim.Core.Observability;
using Sim.Core.State;

namespace Sim.Tests.Observability;

/// <summary>
/// §3 — THE POPULATION RESIDUAL. ColonistsDeparted = Opening + Births − Deaths +
/// Inflow − Outflow − Closing absorbs ONLY colonization: it is 0 for every
/// settlement on every turn no settlement was founded, equals the colony's
/// population on the source's record on a founding turn, is its negative on
/// the founding record, and sums to 0 over all records on EVERY turn because a
/// transfer is zero-sum. Each of those is asserted, on worlds where births,
/// deaths and migration all flow (turn 2 onward, measured).
/// </summary>
public class SettlementIdentityTests
{
    private static void AssertResidualIsZeroEverywhere(ObservationLog log, string world)
    {
        int nonTrivial = 0;
        for (int i = 0; i < log.Observations.Count; i++)
        {
            TurnObservation o = log.Observations[i];
            Assert.Equal(0, o.Turn.Flows.SettlementsFounded);
            long sum = 0;
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                SettlementRecord r = o.Settlements[s];
                Assert.False(r.Founded);
                Assert.Equal(0, r.Population.ColonistsDeparted);
                Assert.Equal(Observer.ColonistsDepartedIdentity, r.Population.ColonistsDepartedIdentity);
                sum += r.Population.ColonistsDeparted;
                if (r.Population.Births > 0 && r.Population.Deaths > 0
                    && (r.Population.Inflow > 0 || r.Population.Outflow > 0)) nonTrivial++;

                // The sums the residual is built from agree with each other.
                long classes = 0;
                for (int c = 0; c < r.Population.ClassCounts.Length; c++) classes += r.Population.ClassCounts[c].Count;
                Assert.Equal(r.Population.Closing, classes + r.Population.Notables);
                Assert.Equal(r.Population.Closing,
                    r.Population.Children + r.Population.Adults + r.Population.Elders + r.Population.Notables);
                Assert.Equal(0, r.Population.Notables);   // NotableLifecycle is never called in the shipped pipeline
            }
            Assert.Equal(0, sum);

            // Per-settlement vitals and flows sum to the world ledger — the
            // READ rows the residual leans on are the same units the Ledger moved.
            long births = 0, deaths = 0, inflow = 0, outflow = 0, closing = 0, opening = 0;
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                births += o.Settlements[s].Population.Births;
                deaths += o.Settlements[s].Population.Deaths;
                inflow += o.Settlements[s].Population.Inflow;
                outflow += o.Settlements[s].Population.Outflow;
                closing += o.Settlements[s].Population.Closing;
                opening += o.Settlements[s].Population.Opening;
            }
            Assert.Equal(o.Turn.Population.Births, births);
            Assert.Equal(o.Turn.Population.NaturalDeaths + o.Turn.Population.Starvation, deaths);
            Assert.Equal(inflow, outflow);
            Assert.Equal(o.Turn.Flows.MigrantsMoved, inflow);
            Assert.Equal(o.Turn.Population.Closing, closing);
            Assert.Equal(o.Turn.Population.Opening, opening);
        }
        Assert.True(nonTrivial > 1000, $"{world}: only {nonTrivial} settlement-turns carried births, deaths AND migration");
    }

    [Fact]
    public void Founded_NoFounding_ResidualIsZeroForEverySettlementEveryTurn()
        => AssertResidualIsZeroEverywhere(ObservedWorlds.Founded300.Value.Log, "founded");

    [Fact]
    public void Driven_NoFounding_ResidualIsZeroForEverySettlementEveryTurn()
        => AssertResidualIsZeroEverywhere(ObservedWorlds.Driven300.Value.Log, "driven");

    [Fact]
    public void OnAFoundingTurn_TheResidualIsTheColonyOnTheSource_AndItsNegativeOnTheColony()
    {
        ObservationLog log = ObservedWorlds.Founding5.Value.Log;
        Assert.Equal(2, log.FirstTurn);   // one warm-up catchment step precedes the observed run
        TurnObservation o = log.At(2)!;
        Assert.Equal(1, o.Turn.Flows.SettlementsFounded);
        Assert.Equal(13, o.Settlements.Length);

        SettlementRecord colony = o.Settlements[12];
        SettlementRecord source = o.Settlements[0];
        Assert.True(colony.Founded);
        Assert.False(source.Founded);
        Assert.Equal(12, colony.Settlement);
        Assert.Equal(2, colony.FoundedTurn);

        // THE FOUNDING-TURN RECORD SHAPE: population = party, grain = provisions,
        // nothing else — the systems iterated prev.Settlements and wrote no row.
        Assert.Equal(0, colony.Population.Opening);
        Assert.True(colony.Population.Closing > 0, "the colony has no people");
        Assert.Equal(0, colony.Population.Births);
        Assert.Equal(0, colony.Population.Deaths);
        Assert.Equal(0, colony.Population.Inflow);
        Assert.Equal(0, colony.Population.Outflow);
        Assert.Equal(-colony.Population.Closing, colony.Population.ColonistsDeparted);
        Assert.Equal(0, colony.Food.GrainOpening);
        Assert.True(colony.Food.GrainClosing > 0, "the colony carries no provisions");
        Assert.Equal(0, colony.Food.Harvest);
        Assert.Equal(0, colony.Food.Eaten);
        Assert.Equal(-colony.Food.GrainClosing, colony.Food.StoreLosses);
        Assert.False(colony.Housing.HasRow);           // colonists start homeless and build
        Assert.Equal(0.0, colony.Housing.Sufficiency); // people and no housing row: unhoused
        Assert.Equal(0, colony.Food.DemandUnits);      // no consumption row yet
        Assert.Empty(colony.Orders);

        // THE SOURCE: its residual is exactly the party that left it (one
        // founding this turn, from settlement 0 — RE-MEASURED on the merged
        // T4.21-2 + T4.21-3 tree: 135 people, 143 pre-packet).
        Assert.Equal(colony.Population.Closing, source.Population.ColonistsDeparted);
        Assert.True(source.Population.Deaths > 0 && source.Population.Inflow > 0,
            "the source turn is not a real turn — deaths and inflow should both flow on it");

        // Zero-sum over ALL records, founding turn included.
        long sum = 0;
        for (int s = 0; s < o.Settlements.Length; s++) sum += o.Settlements[s].Population.ColonistsDeparted;
        Assert.Equal(0, sum);
        for (int s = 1; s < 12; s++) Assert.Equal(0, o.Settlements[s].Population.ColonistsDeparted);

        // And every later turn is an ordinary turn again: 13 settlements, no
        // founding, residual 0 everywhere — including on the colony, which now
        // has vitals and flows of its own.
        for (long turn = 3; turn <= 6; turn++)
        {
            TurnObservation later = log.At(turn)!;
            Assert.Equal(0, later.Turn.Flows.SettlementsFounded);
            Assert.Equal(13, later.Settlements.Length);
            for (int s = 0; s < later.Settlements.Length; s++)
            {
                Assert.False(later.Settlements[s].Founded);
                Assert.Equal(0, later.Settlements[s].Population.ColonistsDeparted);
            }
        }
        Assert.True(log.At(3)!.Settlements[12].Housing.HasRow, "the colony has no housing row on its second turn");
    }

    [Fact]
    public void FirstReign_FoundsNothing_SoItCannotBeTheFoundingWorld()
    {
        // The lane brief names FirstReign as "1 -> 17 settlements over turns
        // 6-19". MEASURED on this tree: that trajectory was a defect of an
        // earlier T4.4 revision (daughters founded with zero provisions —
        // FirstReignTests, history block) and does not occur now. Recorded here
        // so the choice of the stranded-source rig above is evidence, not taste.
        WorldState w = Sim.Tests.Systems.FirstReignTests.Replay(40, out _);
        Assert.Equal(1, w.Settlements.Count);
    }
}
