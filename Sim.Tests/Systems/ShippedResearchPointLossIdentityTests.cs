using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// M6 CIV track (ADR-057; m6-spec §4 note: exit criterion 2's "two conservation pins" are the CP identity and the RP-loss
/// identity) — THE RP-LOSS IDENTITY ON SHIPPED CONTENT, EVERY STEP, SEEDS 1, 2, 42 AND 7. Until now only rig-level pins held it
/// (ResearchPointLifecycleTests (a)–(h)); its shipped-content case (l) is a skipped measurement. Research Points are stored
/// nowhere (D-044 R20-D; the shipped content has neither overflow nor reserve), so a step's RP pool R is either spent on
/// technology progress or lost. Per step and per polity, with "spent" measured from state (Σ technology progress added, a node
/// completed or levelled in the step counting its effective cost on PREV, acceleration credits excluded):
/// <list type="number">
/// <item>0 ≤ spent ≤ R — RP is never created and never spent twice;</item>
/// <item>when the step's technology lane funds a TECHNOLOGY node (the engine's own funding, reconstructed from PREV and the
///   step's orders exactly as ResearchSystem computes it) and that node neither completes nor levels, spent = R: nothing is
///   lost — RP is lost ONLY as surplus past a completion, or when the lane funds no technology node (an empty or assisting
///   lane, D-044 R20-D);</item>
/// <item>the CP identity balance = generated − spent holds for every account (ADR-043).</item>
/// </list>
/// RP is a double rate quantity, not a conserved stock (law 7): equality is to the rounding of a double progress difference,
/// a relative 1e-9 of the pool. 100 steps per seed on the canonical world with one AI Empire (the player polity sets no target:
/// its whole pool is lost, which (1) admits and (2) does not test).
/// </summary>
public sealed class ShippedResearchPointLossIdentityTests
{
    private const int Turns = 100;

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    [InlineData(7UL)]
    public void RpLossIdentity_HoldsEveryStep_OnShippedContent(ulong seed)
    {
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent rc = cfg.Research!;
        Assert.False(rc.Tuning.ResearchOverflow);
        Assert.Null(rc.Tuning.Reserve);
        WorldgenConfig wg = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), log);
        WorldState w = WorldFounding.Found(wg with { AiEmpires = 1 }, cfg, seed);
        int n = rc.Nodes.Count;
        int fundedSteps = 0, lossySteps = 0;
        for (int t = 0; t < Turns; t++)
        {
            OrderRecord[] orders = AiOrders.For(w, cfg, wg);
            foreach (OrderRecord o in orders) log.Append(o);
            WorldState prev = w;
            var polities = new List<(PolityId Id, bool[] Completed, double Pool, double[] Before, double[] Cost, double Credits, int Current, bool Assisting)>();
            for (int p = 0; p < prev.Polities.Count; p++)
            {
                PolityId polity = prev.Polities[p].Id;
                bool[] completed = ResearchQuery.CompletedMask(prev, rc, polity);
                bool[] available = ResearchQuery.AvailableMask(rc, completed, ResearchQuery.LevelMask(prev, rc, polity, completed));
                int[] dest = ResearchQuery.Destinations(prev, rc, polity, completed);
                foreach (OrderRecord o in orders)
                {
                    if (o.Kind != OrderKind.SetResearchTarget || o.ActorId != polity.Value) continue;
                    if (o.TargetId == ResearchQuery.ClearTechnologyTargetId) { dest[0] = -1; continue; }
                    if (o.TargetId == ResearchQuery.ClearCivicsTargetId) { dest[1] = -1; continue; }
                    int idx = rc.IndexOf(new ResearchNodeId(o.TargetId));
                    if (ResearchQuery.IsDestinationAdmissible(rc, idx, completed)) dest[ResearchRoutePlanner.LaneOf(rc, idx, true)] = idx;
                }
                LaneFunding[] lanes = ResearchRoutePlanner.Fund(rc, completed, available, ResearchQuery.RemainingCosts(prev, rc, polity), dest, true);
                var cost = new double[n];
                for (int i = 0; i < n; i++) cost[i] = ResearchQuery.EffectiveCost(prev, rc, polity, i);
                polities.Add((polity, completed, ResearchQuery.ResearchPointPool(prev, rc, polity), ProgressOf(prev, rc, polity), cost,
                    TechCredits(prev, rc, polity), lanes[0].Current, lanes[0].Assisting));
            }

            w = exec.Step(prev);

            foreach ((PolityId polity, bool[] completed, double pool, double[] before, double[] cost, double credits, int current, bool assisting) in polities)
            {
                bool[] done = ResearchQuery.CompletedMask(w, rc, polity);
                double[] after = ProgressOf(w, rc, polity);
                double added = 0.0;
                bool fundedFinished = false;
                for (int i = 0; i < n; i++)
                {
                    if (rc.Nodes[i].Tree != ResearchTree.Technology) continue;
                    bool levelled = ResearchQuery.Level(w, polity, rc.Nodes[i].Key) > ResearchQuery.Level(prev, polity, rc.Nodes[i].Key);
                    bool finished = (done[i] && !completed[i]) || levelled;
                    if (finished && i == current) fundedFinished = true;
                    added += (finished ? cost[i] : after[i]) - before[i];
                }
                double spent = added - (TechCredits(w, rc, polity) - credits);
                double tol = 1e-9 * Math.Max(1.0, pool);
                string at = $"seed {seed}, turn {prev.Clock.Turn}, polity {polity.Value}: pool {pool:R}, spent {spent:R}";
                Assert.True(spent >= -tol, "RP spent is negative: " + at);
                Assert.True(spent <= pool + tol, "RP spent exceeds the pool: " + at);
                bool fundsTechnology = current >= 0 && !assisting && rc.Nodes[current].Tree == ResearchTree.Technology;
                if (fundsTechnology && !fundedFinished)
                {
                    fundedSteps++;
                    Assert.True(Math.Abs(pool - spent) <= tol, "RP lost while the funded technology node did not complete: " + at);
                }
                if (pool - spent > tol) lossySteps++;
            }
            Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));
        }
        Assert.True(fundedSteps > 0, "the identity's no-loss arm was never exercised");
        Assert.True(lossySteps > 0, "no step lost RP: the loss arm was never exercised");
    }

    private static double[] ProgressOf(IReadOnlyWorldState w, ResearchContent rc, PolityId polity)
    {
        var p = new double[rc.Nodes.Count];
        for (int r = 0; r < w.ResearchProgress.Count; r++)
        {
            ResearchProgressRow row = w.ResearchProgress[r];
            if (row.Polity.Value != polity.Value) continue;
            int i = rc.IndexOf(row.Node);
            if (i >= 0) p[i] = row.Progress;
        }
        return p;
    }

    private static double TechCredits(IReadOnlyWorldState w, ResearchContent rc, PolityId polity)
    {
        double tech = 0.0;
        for (int r = 0; r < w.ResearchCredits.Count; r++)
        {
            ResearchCreditRow row = w.ResearchCredits[r];
            if (row.Polity.Value != polity.Value) continue;
            int i = rc.IndexOf(row.Node);
            if (i >= 0 && rc.Nodes[i].Tree == ResearchTree.Technology) tech += row.Amount;
        }
        return tech;
    }
}
