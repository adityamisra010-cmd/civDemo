using System.Globalization;
using System.Text;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Proto.M7.Econ;

/// <summary>
/// Headless calibration command. Usage:
///   dotnet run --project Proto.M7.Econ -c Release -- [--seeds 1,2,42,7] [--turns 0,1,25,50,100,200] [--twin-at 25]
///       [--twin-horizon 10] [--out path.md]
/// Prints a Markdown measurement report (tables only; interpretation lives in the slice report). Stopwatch-free: wall time
/// via Environment.TickCount64, reported, never fed back into a world.
/// </summary>
public static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Main(string[] args)
    {
        ulong[] seeds = ParseList(Arg(args, "--seeds") ?? "1,2,42,7", s => ulong.Parse(s, Inv));
        long[] turns = ParseList(Arg(args, "--turns") ?? "0,1,25,50,100,200", s => long.Parse(s, Inv));
        long[] twinAts = ParseList(Arg(args, "--twin-at") ?? "25", x => long.Parse(x, Inv));
        int horizon = int.Parse(Arg(args, "--twin-horizon") ?? "10", Inv);
        string? outPath = Arg(args, "--out");
        string report = Run(seeds, turns, twinAts, horizon);
        if (outPath is not null) File.WriteAllText(outPath, report);
        Console.Out.Write(report);
        return 0;
    }

    public static string Run(ulong[] seeds, long[] turns, long[] twinAts, int horizon)
    {
        SimConfig cfg = EconWorld.SimCfg;
        double ration = cfg.Military!.UpkeepGrainPerPersonYear;
        var sb = new StringBuilder();
        sb.Append("# Proto.M7.Econ calibration measurements (generated)\n\n");
        sb.Append("Recipe: EconWorld (shipped sim/worldgen/era/pipeline, WorldFounding.Found, TurnExecutor with no orders = the FOUNDED golden recipe).\n\n");

        long[] allTurns = turns;
        for (int i = 0; i < twinAts.Length; i++) allTurns = Union(allTurns, twinAts[i]);
        var perSeed = new List<List<Checkpoint>>();
        for (int s = 0; s < seeds.Length; s++)
        {
            List<Checkpoint> cps = Calibration.RunBaseline(seeds[s], allTurns);
            perSeed.Add(cps);
            sb.Append("## Seed ").Append(seeds[s].ToString(Inv)).Append("\n\n");
            sb.Append("| turn | dt y | wall ms | polity | player | setl | pop | adults | soldiers | grain stock | food prod/step | food eaten/step | civ req/step | surplus/yr | food/adult/yr | troops from surplus | headroom pers/yr | max deficit | unfed |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            for (int c = 0; c < cps.Count; c++)
                for (int p = 0; p < cps[c].Polities.Length; p++)
                {
                    PolitySnapshot x = cps[c].Polities[p];
                    sb.Append("| ").Append(L(cps[c].Turn)).Append(" | ").Append(F(cps[c].DtYears)).Append(" | ").Append(L(cps[c].ElapsedMs))
                      .Append(" | ").Append(x.Polity.ToString(Inv)).Append(" | ").Append(x.Player ? "P" : "").Append(" | ").Append(x.Settlements.ToString(Inv))
                      .Append(" | ").Append(L(x.Population)).Append(" | ").Append(L(x.Adults)).Append(" | ").Append(L(x.Soldiers))
                      .Append(" | ").Append(L(x.GrainStock)).Append(" | ").Append(L(x.FoodProducedLastStep)).Append(" | ").Append(L(x.FoodEatenLastStep))
                      .Append(" | ").Append(L(x.CivilianRequirementLastStep)).Append(" | ").Append(F(x.FoodSurplusPerYear))
                      .Append(" | ").Append(F(x.FoodPerAdultYear, 2)).Append(" | ").Append(F(x.TroopsFromSurplus(ration)))
                      .Append(" | ").Append(x.GrowthHeadroomUnbounded ? "unbounded" : F(x.GrowthHeadroomPersonsPerYear))
                      .Append(" | ").Append(F(x.MaxDeficitRatio, 3)).Append(" | ").Append(L(x.UnfedUnits)).Append(" |\n");
                }
            sb.Append("\nWorld ledger, last step before each checkpoint (grain units / people):\n\n");
            sb.Append("| turn | hash (first 16) | harvest | eaten | spoilage | granary overflow | formation upkeep | fodder | births | deaths | starvation |\n|---|---|---|---|---|---|---|---|---|---|---|\n");
            for (int c = 0; c < cps.Count; c++)
            {
                LedgerTotals d = cps[c].LastStep;
                sb.Append("| ").Append(L(cps[c].Turn)).Append(" | ").Append(cps[c].Hash[..16]).Append(" | ").Append(L(d.Harvest)).Append(" | ").Append(L(d.GrainEaten))
                  .Append(" | ").Append(L(d.Spoilage)).Append(" | ").Append(L(d.GranaryOverflow)).Append(" | ").Append(L(d.FormationUpkeep))
                  .Append(" | ").Append(L(d.Fodder)).Append(" | ").Append(L(d.Births)).Append(" | ").Append(L(d.Deaths)).Append(" | ").Append(L(d.Starvation)).Append(" |\n");
            }
            sb.Append('\n');

            for (int ti = 0; ti < twinAts.Length; ti++)
            {
            long twinAt = twinAts[ti];
            Checkpoint? atTwin = null;
            for (int c = 0; c < cps.Count; c++) if (cps[c].Turn == twinAt) atTwin = cps[c];
            if (atTwin is null) continue;

            // Movement table at the twin turn: 1,000 and 50,000 personnel from the player's capital.
            sb.Append("Movement food at turn ").Append(L(twinAt)).Append(" from the player's capital (route over the real lattice; FULL / MEAN haul basis):\n\n");
            sb.Append("| to | path nodes | route cost u | route km-eq | straight km | terrain factor | days | rations 1k | haul 1k full/mean | rations 50k | haul 50k full/mean | road days built(3) | paved(4) |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            var t1k = Calibration.MovementTable(atTwin.World, 1_000);
            var t50k = Calibration.MovementTable(atTwin.World, 50_000);
            MovementParams mp = MovementParams.From(cfg, atTwin.World);
            for (int i = 0; i < t1k.Count; i++)
            {
                var a = t1k[i]; var b = t50k[i];
                sb.Append("| ").Append(a.To.Value.ToString(Inv)).Append(" | ").Append(a.Full.PathNodes.ToString(Inv)).Append(" | ").Append(F(a.Full.RouteCostUnits, 2))
                  .Append(" | ").Append(F(a.Full.RouteKmEquivalent)).Append(" | ").Append(F(a.Full.StraightLineKm)).Append(" | ").Append(F(a.Full.TerrainFactor, 2))
                  .Append(" | ").Append(F(a.Full.TravelDays)).Append(" | ").Append(L(a.Full.RationUnits)).Append(" | ").Append(L(a.Full.HaulUnits)).Append('/').Append(L(a.Mean.HaulUnits))
                  .Append(" | ").Append(L(b.Full.RationUnits)).Append(" | ").Append(L(b.Full.HaulUnits)).Append('/').Append(L(b.Mean.HaulUnits))
                  .Append(" | ").Append(F(MovementFood.RoadDays(cfg, mp, a.Full.StraightLineKm, EdgeTypes.BuiltRoad)))
                  .Append(" | ").Append(F(MovementFood.RoadDays(cfg, mp, a.Full.StraightLineKm, 4))).Append(" |\n");
            }
            sb.Append('\n');

            // Allocator on the player's measured numbers at the twin turn.
            PolitySnapshot? pl = null;
            for (int p = 0; p < atTwin.Polities.Length; p++) if (atTwin.Polities[p].Player) pl = atTwin.Polities[p];
            if (pl is not null && t1k.Count > 0)
            {
                int far = 0;
                for (int i = 1; i < t1k.Count; i++) if (t1k[i].Full.TravelDays > t1k[far].Full.TravelDays) far = i;
                (long, int, MovementEstimate)[] columns = [(1, 0, t1k[far].Full), (2, 1, t50k[far].Full)];
                LogisticsDecision d = MeasuredAllocation.Resolve(pl, atTwin.LastStep, AllocatorReading.AsShipped, columns);
                sb.Append("Allocator on the player's turn-").Append(L(twinAt)).Append(" numbers (free = grain stock + last step's grain harvest; R = last step's civilian requirement; G = Σ FoodHeadroom.Vacancy × dt; moves = 1k and 50k columns to the farthest settlement, FULL haul):\n\n");
                sb.Append("| free | R | G | civ granted | growth granted | growth cut | mil requested | mil granted | unallocated | stage | move 1k | move 50k |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
                AllocatorRow(sb, d);
                sb.Append('\n');

                // Verification F1: the verdict depends on three open input readings (MeasuredAllocation). The world-ledger
                // readings are the player's only when the player is the world's only polity.
                sb.Append("Allocator input sensitivity at turn ").Append(L(twinAt)).Append(" (verification F1; world-ledger spoilage ").Append(L(atTwin.LastStep.Spoilage))
                  .Append(", grain eaten ").Append(L(atTwin.LastStep.GrainEaten)).Append(", granary overflow ").Append(L(atTwin.LastStep.GranaryOverflow)).Append(" — overflow is not deducted in any reading):\n\n");
                if (atTwin.Polities.Length != 1)
                    sb.Append("n/a: ").Append(atTwin.Polities.Length.ToString(Inv)).Append(" polities — the world ledger is not per polity.\n\n");
                else
                {
                    sb.Append("| reading | free | R | G | civ granted | growth granted | growth cut | mil requested | mil granted | unallocated | stage | move 1k | move 50k |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
                    AllocatorReading[] readings = MeasuredAllocation.ReportedReadings;
                    for (int r = 0; r < readings.Length; r++)
                    {
                        sb.Append("| ").Append(readings[r].Label).Append(' ');
                        AllocatorRow(sb, MeasuredAllocation.Resolve(pl, atTwin.LastStep, readings[r], columns));
                    }
                    sb.Append('\n');
                }
            }

            // Recruitment twins: 1 and 4 formations per settlement, and as many as the existing rules accept.
            long[] caps = [1, 4, long.MaxValue];
            for (int ci = 0; ci < caps.Length; ci++)
            {
                long t0 = Environment.TickCount64;
                RecruitTwin tw = Calibration.MaxRecruitment(seeds[s], atTwin.World, horizon, caps[ci]);
                long twinMs = Environment.TickCount64 - t0;
                sb.Append("Recruitment twin from turn ").Append(L(twinAt)).Append(" — cap ").Append(caps[ci] == long.MaxValue ? "none (max the rules accept)" : L(caps[ci]) + " formation(s) per settlement")
                  .Append("; identity ").Append(tw.Identity).Append(", ").Append(L(tw.PersonnelPerFormation))
                  .Append(" per formation; ").Append(tw.OrdersIssued.ToString(Inv)).Append(" orders; first identity refused: ").Append(tw.FirstRefusal.Length == 0 ? "none" : tw.FirstRefusal)
                  .Append("; ").Append(L(twinMs)).Append(" ms for both branches x ").Append(tw.Horizon.ToString(Inv)).Append(" turns.\n");
                sb.Append("Predicate at T+1 (what binds): ").Append(string.Join(" | ", tw.RefusalsAfter)).Append("\n\n");
                sb.Append("| branch | soldiers T+1 | pop T+1 | adults T+1 | food prod step T+1 | grain stock T+1 | soldiers T+k | pop T+k | adults T+k | food prod step T+k | grain stock T+k | sum harvest | sum upkeep | sum starvation | sum births | unfed T+k |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
                Row(sb, "baseline", tw.BaseAtT1, tw.BaseAtTk, tw.BaseDelta);
                Row(sb, "recruit", tw.TwinAtT1, tw.TwinAtTk, tw.TwinDelta);
                sb.Append('\n');
            }
            }
        }
        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string name, PolitySnapshot a, PolitySnapshot k, LedgerTotals d)
    {
        sb.Append("| ").Append(name).Append(" | ").Append(L(a.Soldiers)).Append(" | ").Append(L(a.Population)).Append(" | ").Append(L(a.Adults))
          .Append(" | ").Append(L(a.FoodProducedLastStep)).Append(" | ").Append(L(a.GrainStock)).Append(" | ").Append(L(k.Soldiers)).Append(" | ").Append(L(k.Population))
          .Append(" | ").Append(L(k.Adults)).Append(" | ").Append(L(k.FoodProducedLastStep)).Append(" | ").Append(L(k.GrainStock))
          .Append(" | ").Append(L(d.Harvest)).Append(" | ").Append(L(d.FormationUpkeep)).Append(" | ").Append(L(d.Starvation)).Append(" | ").Append(L(d.Births))
          .Append(" | ").Append(L(k.UnfedUnits)).Append(" |\n");
    }

    private static void AllocatorRow(StringBuilder sb, LogisticsDecision d)
    {
        sb.Append("| ").Append(L(d.Free)).Append(" | ").Append(L(d.CivilianRequirement)).Append(" | ").Append(L(d.GrowthDemand)).Append(" | ").Append(L(d.CivilianGranted))
          .Append(" | ").Append(L(d.GrowthGranted)).Append(" | ").Append(L(d.GrowthCut)).Append(" | ").Append(L(d.MilitaryRequested)).Append(" | ").Append(L(d.MilitaryGranted))
          .Append(" | ").Append(L(d.Unallocated)).Append(" | ").Append(d.Stage.ToString()).Append(" | ").Append(Move(d.Moves, 1)).Append(" | ").Append(Move(d.Moves, 2)).Append(" |\n");
    }

    private static string Move(MoveDecision[] moves, long id)
    {
        for (int i = 0; i < moves.Length; i++)
            if (moves[i].Id == id)
                return moves[i].Outcome + " " + L(moves[i].Granted) + "/" + L(moves[i].Requested) + " (" + L(moves[i].GrantedDays) + "/" + L(moves[i].RequestedDays) + " d)";
        return "";
    }

    private static long[] Union(long[] a, long b)
    {
        var list = new List<long>(a);
        if (!list.Contains(b)) list.Add(b);
        list.Sort();
        return [.. list];
    }

    private static string? Arg(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    private static T[] ParseList<T>(string text, Func<string, T> parse)
    {
        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var r = new T[parts.Length];
        for (int i = 0; i < parts.Length; i++) r[i] = parse(parts[i]);
        return r;
    }

    private static string F(double v, int d = 1) => Calibration.F(v, d);
    private static string L(long v) => Calibration.L(v);
}
