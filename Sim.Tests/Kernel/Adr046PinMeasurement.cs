using Sim.Core;
using Sim.Core.Kernel;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>ADR-046 (2026-10-23) — MEASUREMENT, not a gate (skipped): the AI pins the review batch moved, on the shipped config
/// and on the single-layer twin with the cheapest-route closure off (TestConfigs.PreAiRoutes — so the remainder is the
/// Prospect bill). Written to $ADR046_OUT; the asserted values live in the tests that pin them.</summary>
public class Adr046PinMeasurement
{
    private const string Why = "measurement (ADR-046); run by hand: set ADR046_OUT and remove Skip";

    private static void Write(string line) =>
        File.AppendAllText(Environment.GetEnvironmentVariable("ADR046_OUT") ?? Path.Combine(Path.GetTempPath(), "adr046.txt"), line + "\n");

    private static string Pins(AiEmpireIntegrationTests.CanonicalAiPins p) =>
        $"targets [{string.Join(",", p.FirstTargets!)}] target1 {p.Target1} build {p.FirstBuild} advance {p.Advance}/{p.AdvancedAt} " +
        $"road {p.Road}/{p.FirstRoad} tax {p.Tax}/{p.FirstTax} a3 {p.A3At} founds [{string.Join(",", p.Founds)}] controlled {p.Controlled}";

    [Fact(Skip = Why)]
    public void CanonicalAiPins_ShippedAndRoutesOff()
    {
        Write("shipped     " + Pins(AiEmpireIntegrationTests.RunCanonicalAi(TestConfigs.Worldgen(), TestConfigs.Sim())));
        Write("routes-off  " + Pins(AiEmpireIntegrationTests.RunCanonicalAi(TestConfigs.Worldgen(), TestConfigs.PreAiRoutes(TestConfigs.Sim()))));
    }

    [Fact(Skip = Why)]
    public void AiWorldTurn300_ShippedAndRoutesOff()
    {
        Write("t300 shipped    " + WorldHash.ComputeHex(CivicsPointsAttributionTests.RunAiWorld(TestConfigs.Sim())));
        Write("t300 routes-off " + WorldHash.ComputeHex(CivicsPointsAttributionTests.RunAiWorld(TestConfigs.PreAiRoutes(TestConfigs.Sim()))));
    }

    [Fact(Skip = Why)]
    public void FromSaveWorld_AiOrders()
    {
        using var runs = new FromSaveRuns();
        for (int i = 0; i < runs.RunLog.Count; i++)
            if (runs.RunLog[i].ActorId == 2) Write("fromsave ai " + runs.RunLog[i]);
    }
}
