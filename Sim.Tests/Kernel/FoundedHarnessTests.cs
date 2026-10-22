using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

// T1.9 — the determinism harness extended to the PRODUCTION preset on the
// canonical founded world (1024², N = 12 since T2.3 — the world the director
// actually plays). Twin, ordered-twin, replay, per-turn conservation, and
// (T2.11) save/load-continue legs; the toy-preset kernel tests remain
// untouched alongside.
//
// TURN COUNT = 300 (T2.11, sized to the CI budget — STATED): 250 Neolithic
// turns (dt 10) plus 50 Bronze turns (dt 5) = 2,750 sim-years. The horizon
// deliberately CROSSES the era-pacing transition at turn 250: the dt switch
// is itself a determinism-relevant dynamic (ADR-011 micro-step integration,
// migration EMA, era-boundary growth continuity), and a 200-turn horizon
// never exercised it. COST (measured, Release, --no-build, adversarially
// re-measured at the packet commit): the five legs run ≈ 55–60s wall; the
// dominant terms are FOUNDING (~2s × 9 independent worlds — fresh worlds
// are the statelessness proof, so this is irreducible) and STEPPING
// (~1.6s × 9); per-turn WorldHash is ~3% of a run (measured 3.76s vs 3.87s
// with/without) and is kept because it names the FIRST divergent turn.
// Under full-suite parallel contention the class can read 3–4× slower —
// budgets below are ceilings, not expectations: the determinism CI job
// budget is ~300s (stated in ci.yml). T0.8's toy legs stay alongside
// (~30s) for kernel-invariant coverage.
[Trait("suite", "determinism")]
public class FoundedHarnessTests
{
    private const ulong Seed = 42;
    private const int Turns = 300;

    /// <summary>The canonical era table's Neolithic→Bronze gate (dt 10 → 5)
    /// sits at turn 250; the save/load leg saves BEFORE it and continues
    /// THROUGH it, so snapshot continuation across an era transition is
    /// pinned, not assumed.</summary>
    private const int EraBoundaryTurn = 250;

    private static TurnExecutor FreshExecutor(OrderLog? orders = null, SimConfig? cfg = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline(); // PRODUCTION preset
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg ?? TestConfigs.Sim())),
            orders);
    }

    /// <summary>Fresh 1024² founded world — everything constructed anew (twin
    /// equality therefore also proves founding + system statelessness).</summary>
    private static WorldState FreshFounded(SimConfig? cfg = null, WorldgenConfig? worldgen = null) =>
        WorldFounding.Found(worldgen ?? TestConfigs.Worldgen(), cfg ?? TestConfigs.Sim(), Seed);

    /// <summary>D-053 §2 RE-RIG (2026-10-13): the config of the ORDERED TWIN, whose turn-exact pins measure a
    /// PER-SETTLEMENT rule — settlement 0, starved by its own labour orders, revolts at <see cref="RevoltTurn"/> and
    /// every later order is refused. Intra-polity grain relief removes that regime only while the polity has spare
    /// grain (D-053 §2): on the shipped config settlement 0's eleven siblings feed it and it never revolts in 300 turns
    /// (MEASURED, 2026-10-13: the twin's revolt pin failed "Expected: Not 1, Actual: 1"). So the twin runs on
    /// TestConfigs.PreEmpireFoodAllocation, which reproduces the de86ace run exactly (RevoltTurn unchanged). The other
    /// ordered legs (replay, conservation, save/load) stay on the SHIPPED config: there the same SessionLog drives
    /// relief instead of revolt, so the determinism harness covers both.</summary>
    // M6 Track EX (2026-10-22): by the R4 rule the de86ace twin strips the newer discovery layer too (TestConfigs.PreDiscovery;
    // MEASURED on the shipped fog: "Expected: Not 1, Actual: 1" — concealed stone and clay change the starvation path).
    private static SimConfig OrderedTwinConfig() => TestConfigs.PreEmpireFoodAllocation(TestConfigs.PreDiscovery(TestConfigs.Sim()));

    /// <summary>Batch 3 (D-054, W1 fix F5): "the de86ace run" the ordered twin reproduces is also the FLAT world — the
    /// batch-2 twin strips every newer layer (the R4 rule), the cylinder (worldgen.json topology.wrapX) included. On the
    /// cylindrical world settlement 0 is not starved into revolt by <see cref="SessionLog"/> (MEASURED: the revolt pin
    /// failed "Expected: Not 1, Actual: 1"), so the twin founds flat and <see cref="RevoltTurn"/> stands.
    /// Integration R1 (2026-10-14) — the premise RE-MEASURED on the merged tree (this config, <see cref="SessionLog"/>, 300
    /// turns): FLAT — settlement 0 revolts at turn 39, the 4 orders landing after it (80, 30, 100, 45 %) are refused, 341
    /// network edges; the CYLINDER — no revolt in 300 turns, 318 edges. Kept flat on purpose: the config is a batch-2 layer
    /// twin, so the R4 rule strips the newer worldgen layer with it, and the property pinned (a revolted settlement's later
    /// orders are refused, turn-exactly) is topology-independent; the shipped config's ordered legs (replay, conservation,
    /// save/load) stay on the cylinder.</summary>
    private static WorldgenConfig OrderedTwinWorldgen() => TestConfigs.PreD054Worldgen();

    /// <summary>Orders shaped like a real session: labor swings incl. both boundaries.
    /// ADR-033 D2: the swings starve settlement 0 into revolt at <see cref="RevoltTurn"/>, so the orders landing
    /// after it are refused — the ordered twin exercises refusal too. D-053 §2: that holds on the per-settlement store
    /// (<see cref="OrderedTwinConfig"/>); on the shipped config relief feeds settlement 0 instead.</summary>
    private static OrderLog SessionLog()
    {
        var log = new OrderLog();
        for (int i = 0; i < SessionPcts.Length; i++)
            log.Append(new OrderRecord(3 + i * 30, ActorId: 1, OrderKind.LaborAllocation, 0, SessionPcts[i]));
        return log;
    }

    // ADR-035 §7 RE-RIG (2026-10-07, P-F2; MEASURED): with founding age composition at demographic scale, the old
    // swing set [60, 30, 80, 0, 100, 45] no longer starves settlement 0 into revolt in 300 turns (the 0 % order
    // lands at 94 and the 100 % one at 124 rescues it), which would leave the twin without its refusal leg and
    // CliRevoltPolityProducerTests without a revolt. The 0 % swing moves to the second slot (issued 33, lands 34):
    // [60, 0, ...] revolts settlement 0 at 39, so the 80, 30, 100 and 45 % orders after it are all REFUSED — both
    // boundaries (0 and 100) still appear in the log.
    internal static readonly double[] SessionPcts = [60.0, 0.0, 80.0, 30.0, 100.0, 45.0];

    /// <summary>The turn settlement 0 revolts on this world under <see cref="SessionLog"/> and
    /// <see cref="OrderedTwinConfig"/> (MEASURED; history on FoundedOrderedTwin_HashIdentical_EveryTurn).</summary>
    internal const int RevoltTurn = 39;

    /// <summary>The farming share in force at <paramref name="turn"/>: the last order landing (issue turn + 1) at or
    /// before it and before the revolt — orders landing at or after the revolt are refused.</summary>
    private static double LastObeyedFarming(int turn)
    {
        double farming = -1.0;
        for (int k = 0; k < SessionPcts.Length; k++)
        {
            int landing = 4 + k * 30;
            if (landing <= turn && landing < RevoltTurn) farming = SessionPcts[k] / 100.0;
        }
        return farming;
    }

    [Fact]
    public void FoundedTwin_HashIdentical_EveryTurn()
    {
        TurnExecutor execA = FreshExecutor(), execB = FreshExecutor();
        WorldState a = FreshFounded(), b = FreshFounded();
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b)); // founding itself twins

        for (int t = 1; t <= Turns; t++)
        {
            a = execA.Step(a);
            b = execB.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        }
    }

    [Fact]
    public void FoundedOrderedTwin_HashIdentical_EveryTurn()
    {
        // D-053 §2: the twin's revolt and refusal pins measure the per-settlement rule — OrderedTwinConfig states why.
        SimConfig cfg = OrderedTwinConfig();
        TurnExecutor execA = FreshExecutor(SessionLog(), cfg), execB = FreshExecutor(SessionLog(), cfg);
        WorldState a = FreshFounded(cfg, OrderedTwinWorldgen()), b = FreshFounded(cfg, OrderedTwinWorldgen());
        var settlement0 = new SettlementId(0);
        for (int t = 1; t <= Turns; t++)
        {
            a = execA.Step(a);
            b = execB.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
            // R4 RE-AIM (2026-10-04, the forager layer; MEASURED on this tree by the agent writing this line).
            // Previously (ADR-033 D2, 2026-10-02): the turn-93 0% order landed at 94 and settlement 0 revolted at
            // 97. With turn-1 food gathered wild, the turn-33 30% order (landing at 34) already starved settlement 0,
            // unfed and unhoused, into revolt at turn 58; the 80%, 0%, 100% and 45% orders after it were REFUSED
            // (RevoltSystem: it stops obeying). Turn-exact, on the twin.
            // ADR-035 RE-PIN (2026-10-07; MEASURED on each commit by the agent writing this line): P-F1 (the
            // founding-turn harvest) moved the revolt 58 -> 63; P-F0 (the founding death remainder) 63 -> 97 — the
            // 30 % and 80 % swings no longer starve it, and the turn-93 0 % order (landing at 94) does, as before R4;
            // P-F2 (founding composition) re-rigs the swing set (see SessionPcts): the 0 % order lands at 34, revolt 39.
            // The pin is now written over RevoltTurn: every order landing BEFORE the revolt is obeyed on its landing
            // turn, every order after it is refused (the allocation stays at the last obeyed order).
            for (int k = 0; k < SessionPcts.Length; k++)
            {
                int landing = 4 + k * 30;
                if (t == landing) Assert.Equal(LastObeyedFarming(landing), a.SectorAllocations[0].Farming);
            }
            if (t == RevoltTurn - 1) Assert.True(EmpireQuery.ControlsSettlement(a, new PolityId(1), settlement0));
            // R3 (Director R2-final §2): the revolted place becomes a NEW AI polity at once.
            if (t == RevoltTurn)
            {
                Assert.True(EmpireQuery.TryGetController(a, settlement0, out PolityId founded));
                Assert.NotEqual(1, founded.Value);
                Assert.True(EmpireQuery.TryGetCommandSource(a, founded, out CommandSource src) && src == CommandSource.Ai);
            }
        }
        // Anti-vacuity (adversarial pass): prove the ORDERS actually fired. Edges
        // alone can't prove it (path labor exists without orders too). Since
        // ADR-033 D2 the live allocation is the LAST order its issuer was entitled
        // to give; orders after the revolt target a settlement polity 1 no longer rules.
        Assert.Equal(settlement0, a.SectorAllocations[0].Settlement);
        double lastFarming = LastObeyedFarming(Turns);
        Assert.Equal(lastFarming, a.SectorAllocations[0].Farming);
        Assert.Equal(1.0 - lastFarming, a.SectorAllocations[0].Construction, 12);
        Assert.True(RevoltTurn > 4 + 30 && RevoltTurn <= Turns, "the revolt no longer falls inside the ordered run — re-aim the rig");
        Assert.True(a.NetworkEdges.Count > 0, "ordered run built nothing — vacuous twin");
    }

    [Fact]
    public void FoundedReplay_LogRoundTrip_ReproducesHashForHash()
    {
        // The D-008 recovery path on the production world: run once capturing
        // hashes, serialize the log to BYTES, reload, replay independently.
        var hashesA = new List<string>(Turns);
        TurnExecutor execA = FreshExecutor(SessionLog());
        WorldState a = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            a = execA.Step(a);
            hashesA.Add(WorldHash.ComputeHex(a));
        }

        using var buffer = new MemoryStream();
        SessionLog().Save(buffer);
        buffer.Position = 0;
        OrderLog reloaded = OrderLog.Load(buffer);

        TurnExecutor execB = FreshExecutor(reloaded);
        WorldState b = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            b = execB.Step(b);
            Assert.Equal(hashesA[t - 1], WorldHash.ComputeHex(b));
        }
    }

    [Fact]
    public void FoundedConservation_ExactEveryTurn()
    {
        TurnExecutor exec = FreshExecutor(SessionLog());
        WorldState world = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            world = exec.Step(world);
            Assert.True(ConservationAuditor.IsConserved(world, out string report),
                $"turn {t}: {report}");
        }
    }

    [Fact]
    public void FoundedSaveLoadContinue_Ordered_AcrossTheEraBoundary_HashIdenticalEveryTurn()
    {
        // T2.11: the toy world had a save/load-continue determinism leg since
        // T0.7; the PRODUCTION world had none — and no leg on ANY world
        // resumed a snapshot UNDER AN ORDER LOG, or delivered an order after
        // the era gate's dt switch (adversarial pass findings). This one does
        // all of it: saves 10 turns before the Neolithic→Bronze gate with
        // orders stamped before the save, between save and gate, and AFTER
        // the gate; continues through the dt change to the full horizon —
        // a snapshot must resume bit-identically (RNG stream states,
        // migration EMA filter, remainder accumulators: everything lives in
        // the state stream or the leg fails), and each pending order must
        // land turn-exactly on both branches (T1.9 precedent: order-delivery
        // semantics get their own turn-exact pins — an order stamped T
        // applies in the step computing turn T+1, BatchFor(prev.Turn)).
        int saveAt = EraBoundaryTurn - 10;
        static OrderLog ResumeLog()
        {
            var log = new OrderLog();
            log.Append(new OrderRecord(235, ActorId: 1, OrderKind.LaborAllocation, 0, 60.0));
            log.Append(new OrderRecord(245, ActorId: 1, OrderKind.LaborAllocation, 0, 80.0));
            log.Append(new OrderRecord(270, ActorId: 1, OrderKind.LaborAllocation, 0, 35.0));
            return log;
        }

        TurnExecutor exec = FreshExecutor(ResumeLog());
        WorldState uninterrupted = FreshFounded();
        for (int t = 1; t <= saveAt; t++) uninterrupted = exec.Step(uninterrupted);
        Assert.Equal(0.60, uninterrupted.SectorAllocations[0].Farming); // turn-235 order landed pre-save

        // ANTI-VACUITY (adversarial pass): the boundary crossing is ASSERTED,
        // not narrated — era-pacing.json is TUNE data, and a retune that
        // moves the gate must break this leg's name, loudly, here.
        Assert.Equal(10.0 * SimClock.YearDays, uninterrupted.Clock.DtDays); // still Neolithic at the save

        using var buffer = new MemoryStream();
        Snapshot.Save(uninterrupted, buffer);
        buffer.Position = 0;
        // ADR-008: terrain is not serialized — the load path REGENERATES it
        // from the same seed + worldgen config, exactly as a real mid-game
        // load would (reusing the in-memory reference would prove nothing
        // about that path).
        Sim.Core.Worldgen.TerrainSet regenerated =
            Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen(), Seed);
        WorldState loaded = Snapshot.Load(buffer, regenerated);
        Assert.Equal(WorldHash.ComputeHex(uninterrupted), WorldHash.ComputeHex(loaded));

        // Two INDEPENDENT executors from here, each with its OWN reloaded
        // order log (the loaded path must not borrow the original's executor
        // state — there must BE none; the pending 245/270 orders re-deliver
        // from the log alone).
        TurnExecutor execLoaded = FreshExecutor(ResumeLog());
        for (int t = saveAt + 1; t <= Turns; t++)
        {
            uninterrupted = exec.Step(uninterrupted);
            loaded = execLoaded.Step(loaded);
            Assert.Equal(WorldHash.ComputeHex(uninterrupted), WorldHash.ComputeHex(loaded));
            // Turn-exact delivery pins on the RESUMED branch (both branches
            // are hash-equal, so pinning one pins both).
            if (t == 246) Assert.Equal(0.80, loaded.SectorAllocations[0].Farming);
            if (t == 270) Assert.Equal(0.80, loaded.SectorAllocations[0].Farming); // not yet
            if (t == 271) Assert.Equal(0.35, loaded.SectorAllocations[0].Farming); // post-gate order lands
        }
        Assert.Equal(5.0 * SimClock.YearDays, loaded.Clock.DtDays); // Bronze dt REACHED — the crossing happened
    }
}
