using System.Globalization;
using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// THE LIVE TURN TRACE — one line per turn, written by the session as it is
/// played, recording what the world actually looked like at that turn.
///
/// WHY IT EXISTS WHEN REPLAY EXISTS. `sim replay` can reconstruct any turn from
/// the manifest and the order log, and it produces far richer output than this.
/// So this file is not here to carry data — it is here so that the played
/// session and the replayed session can be COMPARED. Each line ends in the world
/// hash the director's machine actually computed. Replay the same log and hash
/// each turn: if any line disagrees, the session did not reproduce, and the
/// first disagreeing turn names exactly where it stopped reproducing. Without a
/// live record there is nothing to compare a replay AGAINST, and "it replays
/// fine" would be a claim about the replay only.
///
/// It is also the cheapest possible answer to "what happened around turn 85" —
/// a text file the director can open himself, with no tool between him and it.
///
/// CSV, not JSONL, and the choice is the opposite of ReplayReport's for the same
/// reason: this data is RECTANGULAR and fixed-width. Six columns that never vary
/// with the goods roster or the class registry. ReplayReport's data is ragged,
/// so it is JSON; this is a table, so it is a table.
///
/// AN OBSERVER, LIKE ReplayReport. Handed a world, it reads. It never writes
/// world state and nothing consults it.
/// </summary>
public static class SessionTrace
{
    /// <summary>The header, written once at the top of the file.</summary>
    public const string Header = "turn,year,population,settlements,food,hash";

    /// <summary>
    /// One CSV line for the world as it stands. Every number is formatted with
    /// InvariantCulture — a trace written on a machine with comma decimals would
    /// not parse anywhere else, and this file's whole purpose is to be read
    /// somewhere else.
    /// </summary>
    public static string Line(WorldState world, int grainGoodId)
    {
        long population = 0;
        for (int i = 0; i < world.Buckets.Count; i++) population += world.Buckets[i].Count.Value;

        long food = 0;
        for (int i = 0; i < world.GoodStocks.Count; i++)
            if (world.GoodStocks[i].Good.Value == grainGoodId) food += world.GoodStocks[i].Amount.Value;

        return string.Join(',',
            world.Clock.Turn.ToString(CultureInfo.InvariantCulture),
            world.Clock.WorldDateYears.ToString(CultureInfo.InvariantCulture),
            population.ToString(CultureInfo.InvariantCulture),
            world.Settlements.Count.ToString(CultureInfo.InvariantCulture),
            food.ToString(CultureInfo.InvariantCulture),
            WorldHash.ComputeHex(world));
    }

    /// <summary>
    /// A parsed trace line. Only the fields a comparison needs are surfaced;
    /// the file is the artifact, this is just the reader.
    /// </summary>
    public readonly record struct Row(long Turn, long Year, long Population, int Settlements, long Food, string Hash);

    /// <summary>
    /// Reads a trace file, skipping the header. A malformed line throws with its
    /// number, rather than being skipped — a trace with holes in it would make a
    /// divergence report quietly incomplete.
    /// </summary>
    public static IReadOnlyList<Row> Parse(IEnumerable<string> lines, string describedAs)
    {
        var rows = new List<Row>();
        int number = 0;
        foreach (string raw in lines)
        {
            number++;
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (number == 1 && line == Header) continue;

            string[] parts = line.Split(',');
            if (parts.Length != 6)
            {
                throw new InvalidDataException(
                    $"{describedAs} line {number}: expected 6 columns ({Header}), found {parts.Length}.");
            }

            rows.Add(new Row(
                long.Parse(parts[0], CultureInfo.InvariantCulture),
                long.Parse(parts[1], CultureInfo.InvariantCulture),
                long.Parse(parts[2], CultureInfo.InvariantCulture),
                int.Parse(parts[3], CultureInfo.InvariantCulture),
                long.Parse(parts[4], CultureInfo.InvariantCulture),
                parts[5]));
        }
        return rows;
    }
}
