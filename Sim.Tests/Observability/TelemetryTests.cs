using System.Text;
using System.Text.Json;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// §7 and §9 — the artifact and the fence. The JSONL is byte-identical across
/// two identical sessions; the world hash is bit-identical with and without an
/// observation log attached; no system can reach a record; the schema is
/// untouched (v24); the history seam serves the series the UI graphs.
/// </summary>
public class TelemetryTests
{
    [Fact]
    public void NoTelemetryInducedChange_WorldHashAfter50Turns_IsIdenticalWithAndWithoutALog()
    {
        // THE FENCE. Two fresh executors over the same founded world: one
        // observed after every step, one never. Structural (the log holds no
        // world, no system holds the log) AND measured.
        ObservedWorlds.Run observed = ObservedWorlds.Observed(ObservedWorlds.Founded(), null, 50);
        WorldState quiet = ObservedWorlds.Executor(TestConfigs.Sim(), null).Run(ObservedWorlds.Founded(), 50);

        Assert.Equal(WorldHash.ComputeHex(quiet), WorldHash.ComputeHex(observed.Final));
        Assert.Equal(50, observed.Log.Observations.Count);
        Assert.True(observed.Log.At(50)!.Turn.Population.Births > 0, "vacuous: nothing happened in 50 turns");
        Assert.True(WorldStates.StateEquals(quiet, observed.Final));
    }

    [Fact]
    public void NoSystemAndNoKernelFile_ReferencesTheObservabilityNamespace()
    {
        // The other half of the fence, at the source level: observers are
        // consulted by NO system. A reference from Sim.Core/Systems or
        // Sim.Core/Kernel would be the only way a record could reach a hash.
        string root = RepoPaths.Root();
        foreach (string dir in new[] { "Sim.Core/Systems", "Sim.Core/Kernel", "Sim.Core/State", "Sim.Core/Worldgen" })
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            {
                Assert.DoesNotContain("Sim.Core.Observability", File.ReadAllText(file));
                Assert.DoesNotContain("ObservationLog", File.ReadAllText(file));
            }
        }
        Assert.Equal(25, CanonicalSchema.Version);   // nothing here is serialized into the schema (v25 is T4.21-1's Disasters table)
    }

    [Fact]
    public void TheJsonl_IsByteIdenticalAcrossTwoIdenticalSessions()
    {
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        TelemetryWriter.WriteAll(a, ObservedWorlds.DrivenRun(20).Log);
        TelemetryWriter.WriteAll(b, ObservedWorlds.DrivenRun(20).Log);
        Assert.True(a.Length > 0, "nothing written");
        Assert.Equal(a.ToArray(), b.ToArray());

        string[] lines = Encoding.UTF8.GetString(a.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(20, lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            using JsonDocument doc = JsonDocument.Parse(lines[i]);
            JsonElement root = doc.RootElement;
            Assert.Equal(TelemetryWriter.Schema, root.GetProperty("schema").GetString());
            Assert.Equal(i + 1, root.GetProperty("turn").GetProperty("turn").GetInt64());
            Assert.Equal(12, root.GetProperty("settlements").GetArrayLength());
            Assert.True(root.GetProperty("turn").GetProperty("grain").GetProperty("reconciles").GetBoolean());
        }
        // Doubles are round-trippable ("R"), not rounded: every double written
        // parses back to the record's value BIT FOR BIT — checked on the sector
        // shares (0.7, 0.1, 0.05, 0.05, 0.1 for group A: exact here, measured)
        // and on a ratio with a full mantissa (food_surplus_ratio).
        ObservationLog log = ObservedWorlds.DrivenRun(20).Log;
        using (JsonDocument last = JsonDocument.Parse(lines[19]))
        {
            JsonElement economy = last.RootElement.GetProperty("settlements")[0].GetProperty("economy");
            JsonElement shares = economy.GetProperty("sectorShares");
            SettlementRecord s0 = log.Settlement(20, 0)!;
            for (int i = 0; i < s0.Economy.SectorShares.Length; i++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(s0.Economy.SectorShares[i]), BitConverter.DoubleToInt64Bits(shares[i].GetDouble()));
            Assert.Equal(BitConverter.DoubleToInt64Bits(s0.Economy.FoodSurplusRatio),
                BitConverter.DoubleToInt64Bits(economy.GetProperty("foodSurplusRatio").GetDouble()));
            // T4.21-4: the "> 1.0" half of this guard is REPLACED by "> 0.0".
            // Its job was never the magnitude — it was a proxy for "this is a
            // real computed ratio, not a degenerate reading" — and with the
            // famine-class disaster armed a settlement below 1.0 at turn 20 is
            // an ordinary state of the world rather than a sign the rig broke.
            // MEASURED here: 0.9880226938432444, whose mantissa is exactly what
            // this check needs (it is not its own 3-dp rounding, 0.988). The
            // LONG-MANTISSA requirement — the part that actually makes the
            // bit-for-bit round-trip meaningful — is unchanged and still asserted.
            Assert.True(s0.Economy.FoodSurplusRatio > 0.0 && s0.Economy.FoodSurplusRatio != Math.Round(s0.Economy.FoodSurplusRatio, 3),
                "the ratio has no long mantissa — the round-trip check is weak");
        }
        Assert.Contains("\"sectorShares\":[0.7,0.1,0.05,0.05,0.1]", lines[19]);
        // Non-finite readings are strings, not silently dropped or zeroed: the
        // pull signal (SmoothedAttractiveness) has no row before migration's
        // first run, so on the 0 -> 1 step every settlement's read of PREV is NaN.
        Assert.Contains("\"pullAttractiveness\":\"NaN\"", lines[0]);
        Assert.DoesNotContain("\"pullAttractiveness\":\"NaN\"", lines[19]);
    }

    [Fact]
    public void ASettlementRecord_PrintsInFull_WithEverySection()
    {
        ObservationLog log = ObservedWorlds.Driven300.Value.Log;
        using var ms = new MemoryStream();
        TelemetryWriter.WriteSettlement(ms, log.Settlement(100, 4)!, log.At(100)!.Turn);
        string text = Encoding.UTF8.GetString(ms.ToArray());
        using JsonDocument doc = JsonDocument.Parse(text);
        JsonElement s = doc.RootElement.GetProperty("settlement");
        Assert.Equal(100, doc.RootElement.GetProperty("turn").GetInt64());
        Assert.Equal(4, s.GetProperty("settlement").GetInt32());
        foreach (string section in new[] { "population", "food", "housing", "economy", "social", "migration", "policy", "orders" })
            Assert.True(s.TryGetProperty(section, out _), $"section {section} missing");
        Assert.Equal(Observer.StoreLossesIdentity, s.GetProperty("food").GetProperty("storeLossesIdentity").GetString());
        Assert.StartsWith("GAP:", s.GetProperty("housing").GetProperty("builtDecayedSplit").GetString());
        Assert.StartsWith("GAP:", s.GetProperty("migration").GetProperty("pairwiseFlows").GetString());
        Assert.Equal(14, s.GetProperty("economy").GetProperty("goods").GetArrayLength());
        Assert.True(s.GetProperty("social").GetProperty("grievance").GetArrayLength() >= 2);
        Assert.True(s.GetProperty("social").GetProperty("needSatisfaction").GetArrayLength() >= 3);
        Assert.Equal(12, s.GetProperty("migration").GetProperty("allAttractiveness").GetArrayLength());
    }

    [Fact]
    public void TheHistorySeam_ServesEverySeries_FromTheRecordsThemselves()
    {
        ObservationLog log = ObservedWorlds.Founded300.Value.Log;
        Assert.Equal(1, log.FirstTurn);
        Assert.Equal(300, log.LastTurn);
        Assert.Null(log.At(0));
        Assert.Null(log.At(301));
        Assert.Null(log.Settlement(10, 99));

        var series = new double[300];
        foreach (SeriesKey key in Enum.GetValues<SeriesKey>())
        {
            Assert.Equal(300, log.Series(3, key, series, classId: 1));
            bool anyNonZero = false;
            for (int t = 0; t < 300; t++)
            {
                SettlementRecord r = log.Settlement(t + 1, 3)!;
                double expected = key switch
                {
                    SeriesKey.Population => r.Population.Closing,
                    SeriesKey.Food => r.Food.GrainClosing,
                    SeriesKey.Deficit => r.Food.DeficitRatio,
                    SeriesKey.Happiness => r.Social.Happiness,
                    SeriesKey.Grievance => r.Social.Grievance[0].Value,
                    SeriesKey.Inflow => r.Population.Inflow,
                    SeriesKey.Outflow => r.Population.Outflow,
                    SeriesKey.Births => r.Population.Births,
                    SeriesKey.Deaths => r.Population.Deaths,
                    SeriesKey.Harvest => r.Food.Harvest,
                    SeriesKey.Eaten => r.Food.Eaten,
                    SeriesKey.Dwellings => r.Housing.DwellingsClosing,
                    _ => throw new InvalidOperationException(),
                };
                Assert.Equal(expected, series[t]);
                if (series[t] != 0.0) anyNonZero = true;
            }
            Assert.True(anyNonZero || key == SeriesKey.Deficit, $"series {key} is identically zero over 300 turns");
        }
        Assert.Equal(1, log.Settlement(1, 3)!.Social.Grievance[0].Class);   // class 1 is the row the Grievance series read

        // A settlement that does not exist reads NaN, never 0.
        Assert.Equal(300, log.Series(99, SeriesKey.Population, series));
        Assert.True(double.IsNaN(series[0]) && double.IsNaN(series[299]));

        // Happiness is the public reader on next, recomputed — the same call
        // gives the same number on the final world.
        Assert.Equal(SettlementHappiness.Of(ObservedWorlds.Founded300.Value.Final, new SettlementId(3), TestConfigs.Sim()),
            log.Settlement(300, 3)!.Social.Happiness);
    }

    [Fact]
    public void ObservationsMustBeContiguous_AGapThrows()
    {
        WorldState w0 = ObservedWorlds.Founded();
        TurnExecutor exec = ObservedWorlds.Executor(TestConfigs.Sim(), null);
        WorldState w1 = exec.Step(w0);
        WorldState w2 = exec.Step(w1);
        WorldState w3 = exec.Step(w2);
        var log = new ObservationLog();
        log.Observe(w0, w1, TestConfigs.Sim(), []);
        Assert.Throws<InvalidOperationException>(() => log.Observe(w2, w3, TestConfigs.Sim(), []));
        Assert.Throws<InvalidOperationException>(() => log.Observe(w0, w1, TestConfigs.Sim(), []));
        log.Observe(w1, w2, TestConfigs.Sim(), []);
        Assert.Equal(2, log.Observations.Count);
    }
}
