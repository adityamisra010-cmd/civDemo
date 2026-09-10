using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Kernel;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// The worlds the T4.19 observability tests run on, and the ONE way they are
/// stepped: the production executor, with an <see cref="ObservationLog"/> fed
/// the (prev, next) pair after every step exactly as UiSession.EndTurn and
/// `sim inspect --telemetry` feed it. Three worlds:
///
///   FOUNDED  — WorldFounding seed 42, no orders (the default-mix canonical world).
///   DRIVEN   — the same world under DrivenGoldenTests.DrivingOrders (T3.11): the
///              three asymmetric sector mixes, so goods flow and prices move.
///   FOUNDING — a world in which colonization FIRES under the FULL production
///              pipeline, built with ColonizationTests' stranded-source rig
///              (every settlement but one has its grain and last harvest zeroed
///              by hand, the one is put in deficit). The rig's hand edits happen
///              BEFORE the observed step, so the step itself is conservative and
///              every identity below is asserted on it unchanged.
///
/// WHY NOT FIRSTREIGN. The lane brief cites "FirstReign founds 1 -> 17
/// settlements over turns 6-19 per docs". Measured on this tree: that trajectory
/// was a DEFECT of an earlier T4.4 revision (4d11c02) — daughters founded with
/// zero provisions — and FirstReignTests records that once the clearing cost was
/// made binding "no founding is possible here at all". FirstReign stays at 1
/// settlement for all 40 turns on this tree (measured in
/// SettlementIdentityTests.FirstReign_FoundsNothing_SoItCannotBeTheFoundingWorld).
/// </summary>
internal static class ObservedWorlds
{
    internal sealed record Run(ObservationLog Log, WorldState Final, SimConfig Cfg);

    internal static WorldState Founded(ulong seed = 42UL) =>
        WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), seed);

    internal static TurnExecutor Executor(SimConfig cfg, OrderLog? orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())), orders);
    }

    /// <summary>Steps <paramref name="world"/> for <paramref name="turns"/> turns,
    /// observing every step. The observation call sits AFTER the step and reads
    /// both worlds; the Step call is the same one the unobserved twin makes.</summary>
    internal static Run Observed(WorldState world, OrderLog? orders, int turns)
    {
        SimConfig cfg = TestConfigs.Sim();
        OrderLog log = orders ?? new OrderLog();
        TurnExecutor exec = Executor(cfg, orders);
        var observations = new ObservationLog();
        for (int t = 1; t <= turns; t++)
        {
            WorldState prev = world;
            world = exec.Step(prev);
            observations.Observe(prev, world, cfg, OrderApplied.For(log, prev.Clock.Turn));
        }
        return new Run(observations, world, cfg);
    }

    internal static Run FoundedRun(int turns) => Observed(Founded(), null, turns);

    /// <summary>The three worlds at the packet's horizon, stepped ONCE per test
    /// process and shared: 300 founded + 300 driven turns cost ~90 s, and every
    /// identity below is asserted over the same run rather than a re-run.
    /// MEASURED on this tree (the numbers the tests pin as non-vacuity):
    ///   FOUNDED 300: births, deaths, migrants, harvest and spoilage are ALL
    ///     non-zero on turn 2 (turn 1 harvests zero — the T4.18 warm-up artefact);
    ///     overflow first on turn 1, starvation first on turn 55, trade on 41,
    ///     dwellings never decay; no settlement is founded (12 throughout).
    ///   DRIVEN 300: the same five non-zero on turn 2; starvation on 7, trade on
    ///     7, decay on 48 (25 on the pre-lane-C founding vector; re-measured at
    ///     T4.19-A under CR-014 with the unfixed cap as the control arm — same
    ///     48 — so the founding vector alone moved it); no founding; 56 policy
    ///     changes, all on turn 3.
    ///   FOUNDING 5 (turns 2..6): settlement 12 founded on turn 2 from
    ///     settlement 0, party 143, provisions 128; nothing founded after.</summary>
    internal static readonly Lazy<Run> Founded300 =
        new(() => FoundedRun(300), LazyThreadSafetyMode.ExecutionAndPublication);
    internal static readonly Lazy<Run> Driven300 =
        new(() => DrivenRun(300), LazyThreadSafetyMode.ExecutionAndPublication);
    internal static readonly Lazy<Run> Founding5 =
        new(() => FoundingRun(5), LazyThreadSafetyMode.ExecutionAndPublication);

    internal static Run DrivenRun(int turns)
    {
        WorldState world = Founded();
        OrderLog orders = DrivenGoldenTests.DrivingOrders(world.Settlements.Count);
        OrderValidation.ValidateAgainstWorld(orders, world);
        return Observed(world, orders, turns);
    }

    /// <summary>The stranded-source rig (ColonizationTests.StrandedSource), then
    /// <paramref name="turns"/> FULL-pipeline steps. Founding fires on the first
    /// step: migration finds no viable destination for the deficit source's
    /// famine flight and leaves the demand unplaced; colonization draws the
    /// party from it and outfits it from the source's own granary.</summary>
    internal static Run FoundingRun(int turns, ulong seed = 1UL)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Founded(seed);
        // Warm: one catchment turn so SettlementDistances exists in prev.
        using (var eraStream = Sim.Data.DataFiles.OpenEraPacing())
        {
            w = new TurnExecutor(EraTableLoader.Load(eraStream), [SystemCatalog.Catchment(cfg)]).Step(w);
        }
        SettlementId src = w.Settlements[0].Id;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            if (w.Settlements[s].Id == src) continue;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Settlement == w.Settlements[s].Id)
                    w.GoodStocks[i] = w.GoodStocks[i] with { Amount = Conserved.Zero, LastProducedUnits = 0 };
        }
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(src, 0.40, 1000));
        return Observed(w, null, turns);
    }
}
