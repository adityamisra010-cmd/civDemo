using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T3.9b item 1: REAL PER-SECTOR CONTROL, at the view-model and session seam
/// (headless — no Game, no window). The retired farm-% slider could only
/// express "farming X, construction 100−X, everything else zero"; the gate
/// session's 100/0/0/0/0 is exactly that instrument's failure and is what this
/// control replaces.
///
/// The order kind itself is NOT new — OrderKind.SectorAllocation shipped with
/// T3.3 (D-032) complete with load validation, a consumer, and a turn-exact
/// delivery pin (Sim.Tests PathBuildTests). What is new is the UI's ability to
/// issue it, so these tests cover the ENCODER, the Σ = 0 guard, and the
/// session seam — not the sim mechanism, which is already pinned.
/// </summary>
public class SectorControlTests
{
    private static SimConfig SimCfg() => MarketPanelModelTests.SimCfg();
    private static WorldgenConfig DevCfg() => MarketPanelModelTests.DevCfg();

    private static int[] Mix(int farming, int herding, int extraction, int crafting, int construction)
        => [farming, herding, extraction, crafting, construction];

    // --- the encoder ---------------------------------------------------------

    [Fact]
    public void Batch_FiveOrders_PayloadExact_AllStampedWithTheCurrentTurn()
    {
        // Five DISTINCT weights: a transposed or dropped sector cannot satisfy
        // this (the T3.3 precedent's reasoning, applied at the UI end).
        int[] weights = Mix(55, 15, 10, 12, 8);
        IReadOnlyList<OrderRecord> batch = SectorOrderFactory.Create(
            currentTurn: 17, new SettlementId(3), weights);

        Assert.Equal(Sectors.Count, batch.Count);
        for (int s = 0; s < Sectors.Count; s++)
        {
            OrderRecord o = batch[s];
            Assert.Equal(17, o.Turn);                                  // §3.9 stamping
            Assert.Equal(SectorOrderFactory.UiActorId, o.ActorId);
            Assert.Equal(OrderKind.SectorAllocation, o.Kind);
            Assert.Equal(3 * 8 + s, o.TargetId);                       // D-032 packing
            Assert.Equal((double)weights[s], o.Amount);                // int → exact double
        }
    }

    [Fact]
    public void Batch_SurvivesTheOrderLogRoundTrip_LoadValidationPasses()
    {
        // The encoder's output must satisfy the SIM's load-time validation —
        // the UI cannot be the one component that writes logs the sim refuses.
        var log = new OrderLog();
        foreach (OrderRecord o in SectorOrderFactory.Create(2, new SettlementId(0), Mix(55, 15, 10, 12, 8)))
            log.Append(o);

        using var buffer = new MemoryStream();
        log.Save(buffer);
        buffer.Position = 0;
        OrderLog loaded = OrderLog.Load(buffer);   // throws if validation fails

        Assert.Equal(Sectors.Count, loaded.Count);
        Assert.Equal(OrderKind.SectorAllocation, loaded[0].Kind);
    }

    // --- the preview: normalization is never invisible -----------------------

    [Fact]
    public void Preview_ShowsWhatTheSimWillApply_NotWhatWasTyped()
    {
        // Weights that do NOT sum to 100 — the case where "as typed" and "as
        // applied" genuinely differ, which is the whole reason the preview
        // exists. 10/10/10/10/10 sums to 50 and applies as five equal 20%
        // shares; a UI that echoed the typed numbers would show 10% each and
        // be wrong about the world.
        IReadOnlyList<SectorBarRow> preview =
            SectorOrderFactory.Preview(new SettlementId(0), Mix(10, 10, 10, 10, 10));

        Assert.Equal(Sectors.Count, preview.Count);
        foreach (SectorBarRow row in preview) Assert.Equal(0.2, row.Fraction);
        Assert.Equal("applies as 20% / 20% / 20% / 20% / 20%",
            SectorOrderFactory.PreviewLine(new SettlementId(0), Mix(10, 10, 10, 10, 10)));
    }

    [Fact]
    public void Preview_EqualsTheSharesTheSimActuallyHolds_AfterTheOrderLands()
    {
        // The preview's CLAIM, measured end to end rather than asserted: what
        // the panel showed before submit equals what Sectors.Share reports on
        // the row the sim holds after the batch is delivered. Bit-exact — the
        // preview runs the same operations in the same order the sim does.
        int[] weights = Mix(40, 20, 20, 10, 10);
        IReadOnlyList<SectorBarRow> preview = SectorOrderFactory.Preview(new SettlementId(0), weights);

        SimConfig cfg = SimCfg();
        var log = new OrderLog();
        foreach (OrderRecord o in SectorOrderFactory.Create(2, new SettlementId(0), weights))
            log.Append(o);

        using var eraStream = global::Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = global::Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), log);
        WorldState world = WorldFounding.Found(DevCfg(), cfg, 42);
        for (int t = 1; t <= 3; t++) world = exec.Step(world);

        SectorAllocationRow applied = default;
        bool found = false;
        for (int i = 0; i < world.SectorAllocations.Count; i++)
            if (world.SectorAllocations[i].Settlement.Value == 0)
            { applied = world.SectorAllocations[i]; found = true; break; }
        Assert.True(found, "the batch never landed — the rig proves nothing");

        for (int s = 0; s < Sectors.Count; s++)
        {
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(preview[s].Fraction),
                BitConverter.DoubleToInt64Bits(Sectors.Share(applied, s)));
        }
    }

    // --- the Σ = 0 guard -----------------------------------------------------

    [Fact]
    public void AllZeroAllocation_IsRefused_NeverRecordedAndObeyed()
    {
        // An all-zero allocation normalizes to five zero shares: every sector
        // pool empty, the settlement silently working at nothing, and no log
        // line saying so. The sim SURVIVES it (Sectors.Share returns 0.0, not
        // NaN) which is exactly what makes it dangerous — config-fails-quietly
        // as an order. Refused at the source, actionably.
        var e = Assert.Throws<ArgumentException>(
            () => SectorOrderFactory.Create(1, new SettlementId(0), Mix(0, 0, 0, 0, 0)));
        Assert.Contains("all-zero", e.Message);
        Assert.Contains("55/15/10/12/8", e.Message);   // the message names the way out

        Assert.False(SectorOrderFactory.CanSubmit(Mix(0, 0, 0, 0, 0)));
        // One positive weight is enough — the guard bounds the degenerate case
        // only, never the director's policy.
        Assert.True(SectorOrderFactory.CanSubmit(Mix(0, 0, 0, 0, 1)));
    }

    [Fact]
    public void WeightsOutOfRange_AndWrongShape_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SectorOrderFactory.Create(1, new SettlementId(0), Mix(101, 0, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SectorOrderFactory.Create(1, new SettlementId(0), Mix(-1, 50, 0, 0, 0)));
        Assert.Throws<ArgumentException>(
            () => SectorOrderFactory.Create(1, new SettlementId(0), [50, 50]));
        Assert.False(SectorOrderFactory.CanSubmit([50, 50]));
    }

    // --- the session seam ----------------------------------------------------

    [Fact]
    public void Session_EmitsTheBatchForTheSelection_AndRefusesGhostIds()
    {
        UiSession session = UiSession.Start(42);
        int before = session.Orders.Count;

        // A settlement that does not exist emits NOTHING: an order for a ghost
        // settlement poisons the log at replay validation (the T2.4 rule the
        // labor order already follows).
        Assert.False(session.EmitSectorOrders(Mix(55, 15, 10, 12, 8), 9999));
        Assert.Equal(before, session.Orders.Count);

        // An all-zero allocation is refused at the session too, so the guard
        // cannot be bypassed by calling the seam directly.
        Assert.False(session.EmitSectorOrders(Mix(0, 0, 0, 0, 0), 0));
        Assert.Equal(before, session.Orders.Count);

        Assert.True(session.EmitSectorOrders(Mix(55, 15, 10, 12, 8), 0));
        Assert.Equal(before + Sectors.Count, session.Orders.Count);
        for (int i = 0; i < Sectors.Count; i++)
        {
            OrderRecord o = session.Orders[before + i];
            Assert.Equal(OrderKind.SectorAllocation, o.Kind);
            Assert.Equal(session.World.Clock.Turn, o.Turn);   // stamped with the CURRENT turn
            Assert.Equal(0 * 8 + i, o.TargetId);
        }
    }
}
