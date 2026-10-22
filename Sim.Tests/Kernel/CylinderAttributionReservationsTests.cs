using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rig #16, review L3-15) — THE RESERVATIONS SWITCH (sim.json
/// construction.reservations, D-055's material holds) attributed turn-exactly on the SHIPPED CYLINDER, on a PLAYER rig: the
/// AI worlds never hold (no AI head survives its delivery step — CylinderAttributionAi1024Tests' #14 premise), so the rig is
/// the dev Q17 rig (<see cref="R.DevQ17"/>, X1's v33 fixture recipe in-process: a 30/70 split, granaries ordered at s0 and s2
/// on turn 0, s0's builders taken away on turn 1). ON = the shipped config; OFF = the single-layer twin
/// <see cref="TestConfigs.PreReservations"/>. Because ConstructionHolds is hashed, the switch has TWO first actions, pinned
/// separately:
/// <list type="number">
/// <item><b>The first publication</b> — the first world in which a hold row is published (read off the table, never a hash):
///   every unreleased head sets aside what it still needs, so the worlds first differ there, and in ConstructionHolds
///   ALONE (the write set is exactly that table: a published hold moves no good).</item>
/// <item><b>The first binding</b> — the first world whose V33 projection differs (<see cref="SchemaProjection.HashAtSchemaV33"/>:
///   the hold rows stripped, i.e. batch 3's world): the first step in which a hold changed a draw. There the younger head (s2)
///   has drawn less than batch 3 drew, the older, builder-less head (s0) sets aside exactly its remaining stone, and the
///   younger head's status reads HeldForEarlier, naming s0's head and its set-aside.</item>
/// </list>
/// T1a's own migration test pins the same binding turn on the recorded session (ConstructionHoldsV33FixtureTests.W = 3).
/// SEAM EXPOSURE (MEASURED): 0 seam-crossing pairs and 0 wrapped donor orders on the dev world; holds read no distance (the
/// purse key is the controller, the draw order the purse's — the purse's seam rig is CylinderAttributionSeamTests).
/// </summary>
public class CylinderAttributionReservationsTests : IClassFixture<CylinderAttributionReservationsTests.Arms>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe logs t2-out/s2/probe-a.txt, A6 and D1):
    //   the dev AI founding equals the fixture's `--size 256 --settlements 4 --ai-empires 1` founding;
    //   first publication: world 1 (one row: purse 1, timber, 66 — s0's 26 and s2's 40 still needed); first different
    //   world 1; write set {ConstructionHolds}; worlds 1 and 2 are equal through V33;
    //   first binding: world 3 — s2 at p = 150,000 where PreReservations reaches 450,000 (s0 at 700,000 in both); s0 sets
    //   aside Remaining(20, 700,000) = 6 stone; V33 write set {ConstructionLabor, ConstructionProgress, GoodStocks,
    //   LedgerFlows}; s2's head status on world 3 is HeldForEarlier naming s0's head (slot 0, the granary) with 6 set aside
    //   ahead; the logs agree before 3 (the player's four orders and the AI's research target).
    // ---------------------------------------------------------------------------------------------------------------
    private const int FirstPublication = 1;
    private const int FirstBinding = 3;
    private const int Horizon = FirstBinding + 2;
    private const long TimberSetAsideAtPublication = 66;
    private const long S0Progress = 700_000, S2ProgressOn = 150_000, S2ProgressOff = 450_000;
    private const long StoneHeldByS0 = 6;
    private const int Granary = 1;
    private static readonly SettlementId S0 = new(0), S2 = new(2);
    private static readonly string[] PublicationWrites = ["ConstructionHolds"];
    private static readonly string[] BindingWritesThroughV33 = ["ConstructionLabor", "ConstructionProgress", "GoodStocks", "LedgerFlows"];

    /// <summary>The ON arm, the OFF arm and a second ON run (determinism), each kept on every world to the horizon.</summary>
    public sealed class Arms
    {
        // ADR-043 (2026-10-22) CAUSE BLOCK: was TestConfigs.Sim(). The OFF twin (PreReservations) now strips the newer Track CP
        // layer too (the R4 rule), so the ON arm is the pre-CP world as well — the two arms still differ by the reservations
        // switch alone, and every measured value of this class stands.
        public SimConfig On { get; } = TestConfigs.PreTrackCp(TestConfigs.Sim());
        public SimConfig Off { get; }
        public R.Arm Live { get; }
        public R.Arm Twin { get; }
        public R.Arm Again { get; }

        public Arms()
        {
            Off = TestConfigs.PreReservations(On);
            int[] all = [0, 1, 2, 3, 4, 5];
            Live = R.Run(R.DevQ17(), "on", On, Horizon, keep: all, probes: [R.HoldRow]);
            Twin = R.Run(R.DevQ17(), "reservations-off", Off, Horizon, keep: all, probes: [R.HoldRow]);
            Again = R.Run(R.DevQ17(), "on-again", TestConfigs.PreTrackCp(TestConfigs.Sim()), Horizon);
        }
    }

    private readonly Arms _arms;

    public CylinderAttributionReservationsTests(Arms arms) => _arms = arms;

    [Fact]
    public void TheRig_IsTheFixtureRecipesWorld_OnTheShippedCylinder_Deterministic_AndSeamFree()
    {
        R.Arm live = _arms.Live;
        WorldState fixtureFounding = WorldFounding.Found(TestConfigs.Worldgen() with { SizePx = 256, AiEmpires = 1 }, _arms.On, 42, 4);
        Assert.Equal(WorldHash.ComputeHex(fixtureFounding), live.Hashes[0]);
        Assert.True(live.At(0).Terrain!.WrapX);
        Assert.True(_arms.On.Construction!.Reservations);
        Assert.False(_arms.Off.Construction!.Reservations);
        R.AssertWorld0Identical(live, _arms.Twin);
        R.AssertSameSeries(live, _arms.Again);
        Assert.Equal((0, 0), (R.SeamCrossingPairs(live.At(0)), R.WrappedDonorOrders(live.At(0))));
    }

    /// <summary>#16, pin 1 — THE FIRST PUBLICATION: the first hold row is published in world 1 and the worlds first differ
    /// there, in ConstructionHolds alone; through V33 (holds stripped) worlds 1 and 2 are still batch 3's.</summary>
    [Fact]
    public void Reservations_FirstPublication_DivergesExactlyThere_InConstructionHoldsAlone()
    {
        R.Arm live = _arms.Live, twin = _arms.Twin;
        Assert.Equal(FirstPublication, live.FirstHeld(R.HoldRow.Name));            // the independent reading: the table
        Assert.Equal(-1L, twin.FirstHeld(R.HoldRow.Name));                          // the switch off publishes nothing
        Assert.Equal(FirstPublication, R.FirstDifferent(live, twin));
        string[] written = R.WriteSet(live.At(FirstPublication), twin.At(FirstPublication));
        Assert.Equal(PublicationWrites, written);
        R.AssertNoTopologyTable(written);
        IReadOnlyWorldState w1 = live.At(FirstPublication);
        int timber = _arms.On.Goods!.IdOf("timber");
        Assert.Equal(1, w1.ConstructionHolds.Count);
        Assert.Equal(new ConstructionHoldRow(1, timber, TimberSetAsideAtPublication), w1.ConstructionHolds[0]);
        for (int t = FirstPublication; t < FirstBinding; t++)
            Assert.Equal(SchemaProjection.HashAtSchemaV33(live.At(t), out _), SchemaProjection.HashAtSchemaV33(twin.At(t), out _));
    }

    /// <summary>#16, pin 2 — THE FIRST BINDING: the first V33-projected divergence, where the younger head drew less because
    /// the older one holds, and the younger head's status says so (HeldForEarlier, naming the holder).</summary>
    [Fact]
    public void Reservations_FirstBinding_IsTheFirstV33ProjectedDivergence_WithHeldForEarlierOnTheYoungerHead()
    {
        R.Arm live = _arms.Live, twin = _arms.Twin;
        SimConfig on = _arms.On, off = _arms.Off;
        WorldState wOn = live.At(FirstBinding), wOff = twin.At(FirstBinding);

        // The draw the hold changed, read off the progress table (T2 step 3: the semantic readings come first, the
        // V33-projected hash search after them): s2 (the younger head in the building order) is behind batch 3; s0 is where
        // it was, and its remaining stone is set aside on the binding world.
        Assert.Equal((S0Progress, S2ProgressOn), (Progress(wOn, S0), Progress(wOn, S2)));
        Assert.Equal((S0Progress, S2ProgressOff), (Progress(wOff, S0), Progress(wOff, S2)));
        int stone = on.Goods!.IdOf("stone");
        Assert.Equal(StoneHeldByS0, ConstructionQuery.Remaining(20, S0Progress));
        Assert.True(HasHold(wOn, new ConstructionHoldRow(1, stone, StoneHeldByS0)), "s0's 6 stone is not set aside on the binding world");

        // The younger head's status on the binding world names the hold.
        double dt = live.At(FirstBinding + 1).Clock.DtYears;
        HeadStatusReport s2On = ConstructionQuery.HeadStatus(wOn, on, S2, Pending(live.Log, FirstBinding), dt)!;
        Assert.Equal(MaterialCause.HeldForEarlier, s2On.Cause);
        Assert.Equal(stone, s2On.Resolution.LimitGood);
        Assert.Equal(new QueueEntryRef(S0, 0, Granary), s2On.Holder);
        Assert.Equal(StoneHeldByS0, s2On.SetAsideAhead);
        HeadStatusReport s2Off = ConstructionQuery.HeadStatus(wOff, off, S2, Pending(twin.Log, FirstBinding), dt)!;
        Assert.NotEqual(MaterialCause.HeldForEarlier, s2Off.Cause);
        Assert.Null(s2Off.Holder);

        // The turn-exact pin: the binding world is the FIRST whose V33 projection (holds stripped: batch 3's world) differs.
        int binding = -1;
        for (int t = 0; t <= Horizon && binding < 0; t++)
            if (SchemaProjection.HashAtSchemaV33(live.At(t), out _) != SchemaProjection.HashAtSchemaV33(twin.At(t), out _)) binding = t;
        Assert.Equal(FirstBinding, binding);
        R.AssertLogsEqualBefore(live.Log, twin.Log, FirstBinding);

        WorldState sOn = wOn.Clone(), sOff = wOff.Clone();
        SchemaProjection.StripV34(sOn);
        SchemaProjection.StripV34(sOff);
        string[] written = R.WriteSet(sOn, sOff);
        Assert.Equal(BindingWritesThroughV33, written);
        R.AssertNoTopologyTable(written);
    }

    private static bool HasHold(IReadOnlyWorldState w, ConstructionHoldRow row)
    {
        for (int i = 0; i < w.ConstructionHolds.Count; i++) if (w.ConstructionHolds[i] == row) return true;
        return false;
    }

    private static long Progress(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.ConstructionProgress.Count; i++)
            if (w.ConstructionProgress[i].Settlement == s) return w.ConstructionProgress[i].Progress;
        return -1;
    }

    private static List<OrderRecord> Pending(OrderLog log, long turn)
    {
        OrderBatch b = log.BatchFor(turn);
        var list = new List<OrderRecord>();
        for (int i = 0; i < b.Count; i++) list.Add(b[i]);
        return list;
    }
}
