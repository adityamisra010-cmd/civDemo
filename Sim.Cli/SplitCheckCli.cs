using Sim.Core.Observability;

namespace Sim.Cli;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — `sim split-check REPORT [--control CONTROL] [--expect-hold-at K]
/// [--twin TWIN --order-at T:B]`: a thin verb over
/// <see cref="SplitReportCheck"/>, which holds the judgement (the CorridorStatus precedent: the CI leg's assertions in tested
/// C#, never in an untested script). Prints what the report witnesses for each existence property, then PASS, or one
/// "FAIL Pn: reason" line per failed property; exit 0 when every property holds, 1 when any fails (the failed ids on
/// stderr), and 2 for a report it cannot read.
/// </summary>
internal static class SplitCheckCli
{
    internal static int Run(string[] args)
    {
        if (args.Length < 2 || args[1].StartsWith("--", StringComparison.Ordinal))
            throw new CliUsageException("split-check takes a REPORT file (sim run --split-report PATH)");
        string reportPath = args[1];
        string[] rest = new string[args.Length - 1];
        rest[0] = args[0];
        Array.Copy(args, 2, rest, 1, args.Length - 2);
        var opts = Options.Parse(rest, flags: [], valued: ["--control", "--expect-hold-at", "--twin", "--order-at"]);
        long k = opts.LongOr("--expect-hold-at", -1);
        (long Turn, int Percent)? orderAt = OrderAt(opts);

        SplitReportRow[] report = SplitReport.Parse(File.ReadAllText(reportPath));
        SplitReportRow[]? control = opts.Get("--control") is { } cp ? SplitReport.Parse(File.ReadAllText(cp)) : null;
        SplitTwin? twin = opts.Get("--twin") is { } tp && orderAt is { } o
            ? new SplitTwin(SplitReport.Parse(File.ReadAllText(tp)), o.Turn, o.Percent) : null;
        SplitCheckResult result = SplitReportCheck.Check(report, control, k >= 0 ? k : null, twin);

        Console.WriteLine($"split-check: {reportPath}: {report.Length} rows"
            + (control is not null ? $"; control {opts.Get("--control")}: {control.Length} rows" : "")
            + (k >= 0 ? $"; expecting a binding set-aside in world {k}" : "")
            + (twin is not null ? $"; twin {opts.Get("--twin")}: {twin.Rows.Count} rows, without the order stamped {twin.OrderTurn} (b = {twin.BuildingsPercent})" : ""));
        for (int i = 0; i < result.Findings.Length; i++) Console.WriteLine("  " + result.Findings[i]);
        if (result.Passed)
        {
            Console.WriteLine("PASS");
            return 0;
        }
        var ids = new List<string>(result.Failures.Length);
        for (int i = 0; i < result.Failures.Length; i++)
        {
            Console.WriteLine("FAIL " + result.Failures[i]);
            int colon = result.Failures[i].IndexOf(':', StringComparison.Ordinal);
            ids.Add(colon > 0 ? result.Failures[i][..colon] : result.Failures[i]);
        }
        Console.Error.WriteLine("split-check: FAILED - " + string.Join(", ", ids));
        return 1;
    }

    /// <summary>`--twin TWIN --order-at T:B` (T3 round 2, PT): both or neither; T the kind-12 order's stamp turn (≥ 0), B the
    /// building share it set (a whole percentage in [0, 100]).</summary>
    private static (long Turn, int Percent)? OrderAt(Options opts)
    {
        string? twin = opts.Get("--twin"), at = opts.Get("--order-at");
        if (twin is null && at is null) return null;
        if (twin is null || at is null)
            throw new CliUsageException("--twin TWIN and --order-at T:B go together (the twin is the run without the kind-12 order " +
                                        "stamped T that set the building share B)");
        int colon = at.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0
            || !long.TryParse(at[..colon], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long turn)
            || !int.TryParse(at[(colon + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int percent)
            || percent > 100)
            throw new CliUsageException($"--order-at takes T:B (the kind-12 order's stamp turn and the building share 0-100 it set), not '{at}'");
        return (turn, percent);
    }
}
