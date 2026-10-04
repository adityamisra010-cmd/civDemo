using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// R4 item 2 — THE LEDGER CRASH (docs/r3-final-reconcile-record.md §1 "Defect found"): with the forager switch
/// ON, 100 founders per settlement and dev seed 42, ConsumptionSystem threw "Ledger amounts are never negative
/// (−1)" at turn 853. VIOLATED INVARIANT: a consumption request is never negative. The staple is asked for its
/// own demand PLUS each non-staple food's exact shortfall, a SIGNED quantity — a sub-unit fraction is lent to
/// the staple while it is banked in the non-staple's remainder and repaid (negative) when that remainder pays
/// out a whole unit. In a settlement whose demand had collapsed the repayment exceeded the staple's whole
/// request and was floored into a −1 ledger amount. FIX: the unabsorbed credit is carried in the staple's
/// remainder and settled against its next request (no clamp, nothing dropped); every non-negative request is
/// computed exactly as before, so no shipped world moves.
/// </summary>
public class SubstitutionCreditRegressionTests
{
    /// <summary>The crash configuration exactly as R3 recorded it: forager rates 1.0 / 2.0 (the R2a values,
    /// pinned here so a later retune of sim.json cannot make this test stop reaching the state), switch ON,
    /// founding cohorts re-apportioned to 100 (largest remainder over the shipped 400 vector) with foodStore
    /// 15 per founder.</summary>
    private static SimConfig CrashConfig()
    {
        SimConfig cfg = TestConfigs.Sim();
        return cfg with
        {
            Farming = cfg.Farming with
            {
                PreCultivation = new PreCultivationConfig(Enabled: true, YieldPerArableKm2PerYear: 1.0, OutputPerGathererPerYear: 2.0),
            },
            Founding = cfg.Founding with
            {
                CohortCounts = [14, 14, 13, 11, 10, 9, 7, 6, 5, 4, 3, 2, 1, 1, 0, 0],
                FoodStore = 1500,
            },
        };
    }

    [Fact]
    public void ForagerWorld_100Founders_DevSeed42_RunsPastTheCrashTurn_WithTheCreditCarried_AndTheAuditExact()
    {
        SimConfig cfg = CrashConfig();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())));
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42, null);
        int grain = cfg.Goods!.GrainId;

        int creditTurns = 0;
        for (int t = 1; t <= 900; t++)   // the recorded crash is at turn 853
        {
            world = exec.Step(world);
            for (int i = 0; i < world.GoodStocks.Count; i++)
            {
                GoodStockRow row = world.GoodStocks[i];
                Assert.True(row.Amount.Value >= 0, $"turn {t}: stock {row.Amount.Value} < 0");
                if (row.ConsumeRemainder < 0.0)
                {
                    // Only the staple ever carries a credit, and it is bounded by the non-staple food count
                    // (each exact shortfall is > −1).
                    Assert.Equal(grain, row.Good.Value);
                    Assert.True(row.ConsumeRemainder > -3.0, $"turn {t}: credit {row.ConsumeRemainder}");
                    creditTurns++;
                }
                else
                {
                    Assert.True(row.ConsumeRemainder < 1.0, $"turn {t}: remainder {row.ConsumeRemainder} >= 1");
                }
            }
        }

        // Non-vacuous: the failure state (a credit larger than the staple's request) was reached.
        Assert.True(creditTurns > 0, "no substitution credit was ever carried — the test no longer reaches the crash state");
        Assert.True(ConservationAuditor.IsConserved(world, out string report), report);
    }
}
