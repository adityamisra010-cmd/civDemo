using System.Text.RegularExpressions;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>F3 item 5 (M5 hardening): <c>sim-ui --help</c> lists every flag the program reads — the play options,
/// the headless checks and the developer previews. The flags are taken from the source the program is built from
/// (Program.cs and the headless commands), so a flag added later without a usage line fails here.</summary>
public class UiArgsUsageTests
{
    private static string RepoRoot()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Sim.slnx"))) return d.FullName;
        throw new InvalidOperationException("repository root (Sim.slnx) not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Usage_lists_every_flag_the_program_reads()
    {
        string root = RepoRoot();
        var sources = new[]
        {
            Path.Combine(root, "Sim.Ui", "Program.cs"),
            Path.Combine(root, "Sim.Ui", "UiArgs.cs"),
            Path.Combine(root, "Sim.Ui", "Headless", "SmokeCommand.cs"),
            Path.Combine(root, "Sim.Ui", "Headless", "FrameCost.cs"),
            Path.Combine(root, "Sim.Ui", "Headless", "MapFrameCost.cs"),
        };
        var flags = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string f in sources)
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"(--[a-z0-9][a-z0-9-]*)\""))
                flags.Add(m.Groups[1].Value);
        Assert.Contains("--research-preview", flags); // the scan found the preview flags at all
        Assert.Contains("--smoke", flags);
        foreach (string flag in flags)
            Assert.True(Regex.IsMatch(UiArgs.Usage, Regex.Escape(flag) + @"(?![a-z0-9-])"), "--help omits " + flag);
    }

    /// <summary>P4: a <c>--frame-cost</c> MODE is a word, not a flag, so the scan above cannot see it; the map mode the
    /// smoke command dispatches on is listed with its own options.</summary>
    [Fact]
    public void Usage_lists_the_map_frame_cost_mode_with_its_options()
    {
        Match line = Regex.Match(UiArgs.Usage, @"--frame-cost " + Regex.Escape(Sim.Ui.Headless.MapFrameCost.Mode) + @"(?![a-z0-9-])[^\n]*");
        Assert.True(line.Success, "--help omits --frame-cost " + Sim.Ui.Headless.MapFrameCost.Mode);
        foreach (string option in new[] { "--rounds", "--settlements", "--turns" })
            Assert.Contains(option, line.Value, StringComparison.Ordinal);
        // W2 review F3: the help names the settlement default the tool actually uses (it was 400, past the cylinder's capacity).
        Assert.Contains("(default " + Sim.Ui.Headless.MapFrameCost.DefaultSettlements.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",",
            UiArgs.Usage, StringComparison.Ordinal);
    }
}
