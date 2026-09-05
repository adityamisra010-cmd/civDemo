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

        Assert.Equal(1, w.Controls.Count);
        Assert.False(EmpireQuery.TryGetController(w, new SettlementId(0), out _));
        // ...and the comfortable neighbour is untouched, so this is not a purge.
        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out _));
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

        w = RevoltOnly().Step(w);

        Assert.Equal(afterFirst, w.Controls.Count);
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

        Assert.Equal(3, w.Controls.Count);
        Assert.Equal(0, w.Controls[0].Place.Value);
        Assert.Equal(2, w.Controls[1].Place.Value);
        Assert.Equal(3, w.Controls[2].Place.Value);
    }

    [Fact]
    public void RevoltProducesEXACTLYTheStateT45sRaiderGateAsksFor()
    {
        // The loop this mechanism exists to close. AppropriationSystem's raider
        // must be STATELESS — and before revolt existed, no founded world could
        // ever contain one, so the gate could never open. This asserts the
        // produced state against that gate's own predicate rather than against a
        // restatement of it.
        WorldState w = Governed(deficit0: 1.0, dwellings0: 0);
        var revolted = new SettlementId(0);

        Assert.True(EmpireQuery.TryGetController(w, revolted, out _),
            "precondition: the settlement starts governed");

        w = RevoltOnly().Step(w);

        // "Stateless" is exactly "no control row names this place" — the same
        // condition AppropriationSystem.IsStateless evaluates.
        bool stateless = true;
        for (int i = 0; i < w.Controls.Count; i++)
            if (w.Controls[i].Place == revolted) stateless = false;

        Assert.True(stateless,
            "T4.5's raider precondition is still unreachable — revolt did not produce a "
            + "stateless settlement, so appropriation remains dead code in a founded world.");
    }
}
