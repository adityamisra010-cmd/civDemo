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

    private static TurnExecutor FreshExecutor(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline(); // PRODUCTION preset
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(TestConfigs.Sim())),
            orders);
    }

    /// <summary>Fresh 1024² founded world — everything constructed anew (twin
    /// equality therefore also proves founding + system statelessness).</summary>
    private static WorldState FreshFounded() =>
        WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), Seed);

    /// <summary>Orders shaped like a real session: labor swings incl. both boundaries.</summary>
    private static OrderLog SessionLog()
    {
        var log = new OrderLog();
        double[] pcts = [60.0, 30.0, 80.0, 0.0, 100.0, 45.0];
        for (int i = 0; i < pcts.Length; i++)
            log.Append(new OrderRecord(3 + i * 30, ActorId: 1, OrderKind.LaborAllocation, 0, pcts[i]));
        return log;
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
        TurnExecutor execA = FreshExecutor(SessionLog()), execB = FreshExecutor(SessionLog());
        WorldState a = FreshFounded(), b = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            a = execA.Step(a);
            b = execB.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        }
        // Anti-vacuity (adversarial pass): prove the ORDERS actually fired —
        // the last SessionLog order (45%) must be the live allocation. Edges
        // alone can't prove it (path labor exists without orders too).
        Assert.Equal(0.45, a.LaborAllocations[0].FarmShare);
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
        Assert.Equal(0.60, uninterrupted.LaborAllocations[0].FarmShare); // turn-235 order landed pre-save

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
            if (t == 246) Assert.Equal(0.80, loaded.LaborAllocations[0].FarmShare);
            if (t == 270) Assert.Equal(0.80, loaded.LaborAllocations[0].FarmShare); // not yet
            if (t == 271) Assert.Equal(0.35, loaded.LaborAllocations[0].FarmShare); // post-gate order lands
        }
        Assert.Equal(5.0 * SimClock.YearDays, loaded.Clock.DtDays); // Bronze dt REACHED — the crossing happened
    }
}
