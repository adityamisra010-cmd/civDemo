using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M4 — REVOLT, and the T4.5 loop it closes.
///
/// The point of these tests is not that a row disappears. It is that
/// STATELESSNESS IS NOW REACHABLE FROM A GOVERNED WORLD, which is the
/// precondition T4.5's appropriation mechanism has been waiting on since M4-C
/// made every founded settlement controlled. So the file pins three things: the
/// mechanism fires on total deprivation, it does NOT fire on anything less, and
/// the state it produces is the one AppropriationSystem's own gate asks for.
///
/// R3 (Director R2-final §2) changed the third: a revolted settlement now becomes a
/// NEW AI-controlled polity at once, so revolt no longer produces statelessness
/// (docs/adr/cr-019-revolt-polity-vs-t45-raider.md records what that costs T4.5).
/// </summary>
public class RevoltTests
{
    private static SimConfig Cfg() => TestConfigs.Sim();

    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 10 } ] }""");

    /// <summary>Revolt alone — nothing else may move a person, a grain or a row.</summary>
    private static TurnExecutor RevoltOnly() =>
        new(FlatEra(), [SystemCatalog.Revolt(TestConfigs.Sim())]);

    /// <summary>
    /// A governed two-settlement world in which settlement 0's condition is set
    /// by the caller. Deprivation is expressed in the PRIMARY signals happiness
    /// reads — a food deficit and a dwelling stock — never by writing happiness,
    /// which is derived and cannot be written.
    /// </summary>
    private static WorldState Governed(double deficit0, long dwellings0)
    {
        var w = new WorldState(1);
        var ledger = new Ledger(w.LedgerFlows);
        var polity = new PolityId(1);
        w.Polities.Add(new PolityRow(polity, CommandSource.Player));

        for (int i = 0; i < 2; i++)
        {
            var s = new SettlementId(i);
            w.Settlements.Add(new SettlementRow(s, i, 0));
            w.Controls.Add(new ControlRow(polity, s, 1.0));

            w.Buckets.Add(new BucketRow(
                s, new CultureId(0), new ReligionId(0), new ClassId(0), 0,
                Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, 600, FlowDirection.Source, OverdrawPolicy.Throw);

            // Settlement 1 is always comfortable: fed and fully housed.
            double deficit = i == 0 ? deficit0 : 0.0;
            long dwellings = i == 0 ? dwellings0 : 100;

            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, deficit, 600));
            w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (dwellings > 0)
            {
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings,
                    ReasonIds.InitialEndowment, dwellings, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }

        return w;
    }

    [Fact]
    public void TotalDeprivationCostsThePolityTheSettlement()
    {
        WorldState w = Governed(deficit0: 1.0, dwellings0: 0);
        Assert.Equal(2, w.Controls.Count);

        w = RevoltOnly().Step(w);

        // R3 (Director R2-final §2): the player LOSES the place, and it becomes a NEW AI polity at once.
        Assert.Equal(2, w.Controls.Count);
        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(0), out PolityId founded));
        Assert.Equal(2, founded.Value);
        Assert.True(EmpireQuery.TryGetCommandSource(w, founded, out CommandSource source));
        Assert.Equal(CommandSource.Ai, source);
        Assert.Equal(2, w.Polities.Count);
        Assert.False(EmpireQuery.TryGetCapital(w, founded, out _));   // no seat is invented (§5)
        // ...and the comfortable neighbour is untouched, so this is not a purge.
        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out PolityId keeper));
        Assert.Equal(1, keeper.Value);
    }

    [Fact]
    public void ASettlementShortOfBothButNotDestituteDoesNotRevolt()
    {
        // The anti-vacuity companion, and the one that makes the threshold mean
        // something: badly off is not the same as at zero.
        WorldState w = Governed(deficit0: 0.9, dwellings0: 5);
        Assert.True(SettlementHappiness.Of(w, new SettlementId(0), Cfg()) > 0.0);

        w = RevoltOnly().Step(w);

        Assert.Equal(2, w.Controls.Count);
    }

    [Fact]
    public void AComfortableWorldIsLeftBitForBitAlone()
    {
        WorldState w = Governed(deficit0: 0.0, dwellings0: 100);
        w = RevoltOnly().Step(w);

        Assert.Equal(2, w.Controls.Count);
        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(0), out _));
        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out _));
    }

    [Fact]
    public void RevoltIsIdempotent_AnAlreadyStatelessPlaceIsNotRevoltedTwice()
    {
        WorldState w = Governed(deficit0: 1.0, dwellings0: 0);
        w = RevoltOnly().Step(w);
        int afterFirst = w.Controls.Count;

        int politiesAfterFirst = w.Polities.Count;
        w = RevoltOnly().Step(w);

        // The new polity IS the place: it has no ruler to throw off, so it does not split again (R3).
        Assert.Equal(afterFirst, w.Controls.Count);
        Assert.Equal(politiesAfterFirst, w.Polities.Count);
    }

    [Fact]
    public void SurvivingControlRowsKeepTheirRelativeOrder()
    {
        // The canonical stream serializes this table in row order, so a rebuild
        // that reshuffled survivors would move every world hash for no reason.
        var w = new WorldState(1);
        var ledger = new Ledger(w.LedgerFlows);
        var polity = new PolityId(1);
        w.Polities.Add(new PolityRow(polity, CommandSource.Player));

        for (int i = 0; i < 4; i++)
        {
            var s = new SettlementId(i);
            w.Settlements.Add(new SettlementRow(s, i, 0));
            w.Controls.Add(new ControlRow(polity, s, 1.0));
            w.Buckets.Add(new BucketRow(
                s, new CultureId(0), new ReligionId(0), new ClassId(0), 0,
                Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, 600, FlowDirection.Source, OverdrawPolicy.Throw);

            // Only settlement 1 is destitute.
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, i == 1 ? 1.0 : 0.0, 600));
            w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (i != 1)
            {
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings,
                    ReasonIds.InitialEndowment, 100, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }

        w = RevoltOnly().Step(w);

        Assert.Equal(4, w.Controls.Count);
        Assert.Equal(0, w.Controls[0].Place.Value);
        Assert.Equal(2, w.Controls[1].Place.Value);
        Assert.Equal(3, w.Controls[2].Place.Value);
        // R3: the new polity's row is appended after the survivors.
        Assert.Equal(1, w.Controls[3].Place.Value);
        Assert.Equal(2, w.Controls[3].Polity.Value);
    }

    [Fact]
    public void RevoltNoLongerProducesStatelessness_TheRaiderGateLosesItsOnlyProducer_CR019()
    {
        // Before R3 this test pinned the opposite: revolt was the one path to a STATELESS settlement, the
        // precondition of T4.5's raider. Director R2-final §2 rules that a revolted settlement becomes a NEW
        // AI-CONTROLLED POLITY immediately, so revolt now produces a governed place and the raider gate has no
        // producer in a founded world. The conflict is recorded in docs/adr/cr-019-revolt-polity-vs-t45-raider.md;
        // this test pins the ruled state so the change cannot go unnoticed in either direction.
        WorldState w = Governed(deficit0: 1.0, dwellings0: 0);
        var revolted = new SettlementId(0);
        w = RevoltOnly().Step(w);

        bool stateless = true;
        for (int i = 0; i < w.Controls.Count; i++)
            if (w.Controls[i].Place == revolted) stateless = false;
        Assert.False(stateless);
    }
}
