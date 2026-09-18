using Sim.Core.Observability;

namespace Sim.Tests.Observability;

// TEMPORARY probe for T4.21-4 step 5. Deleted before the final commit.
public class ZzObsProbe
{
    [Fact]
    public void Probe()
    {
        var sb = new System.Text.StringBuilder();
        foreach ((string name, ObservationLog log) in new (string, ObservationLog)[]
        {
            ("founded", ObservedWorlds.Founded300.Value.Log),
            ("driven", ObservedWorlds.Driven300.Value.Log),
        })
        {
            long starv = 0; int firstStarv = -1, starvTurns = 0;
            long decayed = 0; int firstDecay = -1;
            int firstTrade = -1, firstMig = -1;
            for (int t = 1; t <= 300; t++)
            {
                TurnRecord r = log.At(t)!.Turn;
                starv += r.Population.Starvation;
                if (r.Population.Starvation > 0) { starvTurns++; if (firstStarv < 0) firstStarv = t; }
                decayed += r.Dwellings.Decayed;
                if (r.Dwellings.Decayed > 0 && firstDecay < 0) firstDecay = t;
                if (r.Flows.TradeUnits > 0 && firstTrade < 0) firstTrade = t;
                if (r.Flows.MigrantsMoved > 0 && firstMig < 0) firstMig = t;
            }
            sb.AppendLine($"{name}: starvationTotal={starv} firstStarvTurn={firstStarv} starvTurns={starvTurns} "
                + $"decayedTotal={decayed} firstDecayTurn={firstDecay} firstTrade={firstTrade} firstMig={firstMig} "
                + $"settlements300={log.At(300)!.Settlements.Length} t2Migrants={log.At(2)!.Turn.Flows.MigrantsMoved} "
                + $"t3Migrants={log.At(3)!.Turn.Flows.MigrantsMoved}");
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("T4214_OUT") ?? "/tmp/obs.txt", sb.ToString());
        Console.WriteLine(sb.ToString());
    }
}
