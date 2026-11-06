using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Proto.M7.Tactical;

/// <summary>
/// [P fix r1, P-V9] The lane-P tactical-outcome cache (the bench's <c>parityp run</c> CSV, re-read by <c>eval</c>, <c>fit</c> and
/// <c>accept</c> to compute the acceptance verdict and the battery hash): its row format, parser and outcome digest in ONE place,
/// round-trip tested with a populated table (ParityPTests). Integer columns only, invariant culture; the trailing timing
/// column (wall-clock milliseconds, reporting only) is written by the bench and ignored by the parser.
/// </summary>
public static class ParityPCsv
{
    public const string Header = "scenario,seed,winner,casA,casB,pulses,durationMs,reason,events,ms";

    /// <summary>One data row (no line terminator): the case, its outcome, the tactical event count and the timing field.</summary>
    public static string Row(PCase c, POutcome o, long events, string msField)
        => string.Create(CultureInfo.InvariantCulture,
            $"{(int)c.Scenario},{c.Seed},{o.Winner},{o.CasA},{o.CasB},{o.Pulses},{o.DurationMs},{(int)o.Reason},{events},{msField}");

    /// <summary>Parses one data row (the columns after <c>reason</c> are not part of the outcome).</summary>
    public static (PCase Case, POutcome Outcome) Parse(string line)
    {
        var p = line.Split(',');
        if (p.Length < 8) throw new FormatException("parity CSV row: expected at least 8 columns");
        long L(int k) => long.Parse(p[k], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return (new PCase((PScenario)L(0), ulong.Parse(p[1], NumberStyles.None, CultureInfo.InvariantCulture)),
                new POutcome((int)L(2), L(3), L(4), L(5), L(6), (BattleEndReason)L(7)));
    }

    /// <summary>Parses a whole file's lines: the first line is the header; empty lines are skipped.</summary>
    public static (PCase[] Cases, POutcome[] Outcomes) ParseAll(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0 || lines[0] != Header) throw new FormatException("parity CSV: missing or unexpected header");
        var c = new List<PCase>(); var o = new List<POutcome>();
        for (int i = 1; i < lines.Count; i++)
        {
            if (lines[i].Length == 0) continue;
            var (pc, po) = Parse(lines[i]);
            c.Add(pc); o.Add(po);
        }
        return (c.ToArray(), o.ToArray());
    }

    /// <summary>SHA-256 over the canonical outcome text of every case in order (the timing and event columns excluded).</summary>
    public static string Digest(IReadOnlyList<PCase> cases, IReadOnlyList<POutcome> o)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cases.Count; i++)
            sb.Append(CultureInfo.InvariantCulture, $"{(int)cases[i].Scenario},{cases[i].Seed},{o[i].Winner},{o[i].CasA},{o[i].CasB},{o[i].Pulses},{o[i].DurationMs},{(int)o[i].Reason};");
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(sb.ToString())));
    }
}
