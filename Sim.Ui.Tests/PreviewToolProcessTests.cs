using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// THE DEVELOPER PREVIEW TOOLS CANNOT ROT SILENTLY AGAIN (M5 hardening H3; Director 2026-10-05 §12).
///
/// At <c>bc87ef8</c> both <c>sim-ui --action-preview</c> and <c>sim-ui --player-views-preview</c> aborted (exit 134,
/// "action preview rig: could not declare a 20% levy") because their rig granted <c>arithmetic_babylonian</c> as "a
/// taxation node", which stopped opening the tax gate when Taxation became its own Civics node (R5, <c>9f7c82b</c>) —
/// and no test ran either tool, so the suite stayed green. These tests run BOTH tools end to end, each as a SEPARATE
/// PROCESS (the exact command a developer types: <c>dotnet Sim.Ui.dll --action-preview DIR</c>), and check what they
/// wrote: every state's SVGs, a log whose hashes match the files, and the tax edict present exactly where the rig
/// puts it. A rig that stops satisfying the gate fails here, not in a developer's terminal months later.
///
/// The rig itself is pinned in-process too: its tax gate is read from content (<c>governance.taxationRequires</c>),
/// it stands at an Age at or past the Taxation node's own Age (Taxation is an A3 capability, Director §7), and the
/// gate is open in each rig state. Each process run is bounded (a hang is reported as non-termination, never waited
/// on: ADR-015 §7.1).
/// </summary>
public sealed class PreviewToolProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-preview-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Runs <c>dotnet Sim.Ui.dll args…</c> from the test's output directory (where the project reference puts
    /// Sim.Ui.dll, its runtimeconfig and the shipped assets), bounded by <paramref name="limit"/>.</summary>
    private static (int Exit, string Out, string Err) RunSimUi(TimeSpan limit, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sim.Ui.dll"));
        foreach (string a in args) psi.ArgumentList.Add(a);
        using Process p = Process.Start(psi)!;
        Task<string> o = p.StandardOutput.ReadToEndAsync(), e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(limit))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException("Sim.Ui " + string.Join(' ', args) + " did not finish in " + limit + " (non-termination)");
        }
        return (p.ExitCode, o.Result, e.Result);
    }

    private static string Tail(string s) => s.Length <= 4000 ? s : s[^4000..];

    /// <summary>Every "  NAME  sha256 HEX[  …]" line of a preview log names a file in <paramref name="dir"/> whose
    /// bytes hash to HEX; with <paramref name="everySvgListed"/>, every SVG in the directory is listed too — the log
    /// is evidence of what was written. Returns how many files the log hashes.</summary>
    private static int AssertLogMatchesFiles(string dir, string[] log, bool everySvgListed = true)
    {
        var listed = new List<string>();
        foreach (string line in log)
        {
            int at = line.IndexOf("  sha256 ", StringComparison.Ordinal);
            if (at < 0) continue;
            string name = line[..at].Trim();
            string rest = line[(at + "  sha256 ".Length)..].TrimStart();
            int end = rest.IndexOf(' ');
            string hex = end < 0 ? rest.TrimEnd() : rest[..end];
            string path = Path.Combine(dir, name);
            Assert.True(File.Exists(path), "the log lists " + name + ", which was not written");
            Assert.Equal(hex, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
            listed.Add(name);
        }
        if (everySvgListed)
        {
            string[] svgs = Directory.GetFiles(dir, "*.svg");
            Assert.Equal(svgs.Length, listed.Count);
            foreach (string svg in svgs) Assert.Contains(Path.GetFileName(svg), listed);
        }
        return listed.Count;
    }

    private static string StateLine(string[] log, string stem) =>
        Array.Find(log, l => l.StartsWith(stem + ": ", StringComparison.Ordinal))
        ?? throw new Xunit.Sdk.XunitException("the preview log has no state line for " + stem + "\n" + string.Join("\n", log));

    /// <summary>The display names of the nodes the tax gate names, from content.</summary>
    private static string[] GateNames(SimConfig cfg)
    {
        string[] ids = ActionSurfacePreview.TaxationGate(cfg);
        var names = new string[ids.Length];
        for (int i = 0; i < ids.Length; i++) names[i] = cfg.Research!.Nodes[cfg.Research.IndexOfId(ids[i])].Name;
        return names;
    }

    // ------------------------------------------------------------------ the tools, as separate processes

    [Fact]
    public void ActionPreview_AsASeparateProcess_ExitsZero_PaintsEveryState_TheTaxEdictWhereTheRigPutsIt()
    {
        string dir = Path.Combine(_root, "action");
        (int exit, string output, string err) = RunSimUi(TimeSpan.FromMinutes(15), "--action-preview", dir);
        Assert.True(exit == 0, "sim-ui --action-preview exit " + exit + "\n" + Tail(output) + "\n" + Tail(err));

        string[] stems = ["turn-1-a1", "later-a3", "later-a8", "researched-a4"];
        foreach (string stem in stems)
            foreach (string kind in new[] { "screen", "panel" })
            {
                string svg = Path.Combine(dir, stem + "-" + kind + ".svg");
                Assert.True(File.Exists(svg), "not written: " + svg);
                Assert.Contains("action preview: " + Path.GetFullPath(svg), output, StringComparison.Ordinal);
            }
        string[] log = File.ReadAllLines(Path.Combine(dir, "preview-log.txt"));
        AssertLogMatchesFiles(dir, log);

        // Turn 1 (Age I, nothing researched): no tax edict anywhere on the surface. Every rig past the gate: the
        // Governance domain, the edict's heading and its provenance — the node(s) sim.json names, learned.
        Assert.DoesNotContain("Governance", StateLine(log, "turn-1-a1"), StringComparison.Ordinal);
        // M5 polish (directive §4, verify G2): the edict is named there only as a locked line with Governance.GateOf's
        // reason, never as the Governance block ("The tax edict") or a control.
        string a1Panel = File.ReadAllText(Path.Combine(dir, "turn-1-a1-panel.svg"));
        Assert.DoesNotContain("THE TAX EDICT", a1Panel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Tax edict<", a1Panel, StringComparison.Ordinal);
        Assert.Contains("needs Taxation (Civics)", a1Panel, StringComparison.Ordinal);
        string[] gate = GateNames(UiFounding.ProductionConfig());
        foreach (string stem in new[] { "later-a3", "later-a8", "researched-a4" })
        {
            Assert.Contains("/Governance/", StateLine(log, stem), StringComparison.Ordinal);
            string panel = File.ReadAllText(Path.Combine(dir, stem + "-panel.svg"));
            Assert.Contains("THE TAX EDICT", panel, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("learned: " + gate[0], panel, StringComparison.Ordinal);
        }
        // The later rig DECLARED its levy through the real order path; the researched rig declared none.
        Assert.Contains(">A levy is declared.<", File.ReadAllText(Path.Combine(dir, "later-a3-panel.svg")), StringComparison.Ordinal);
        Assert.Contains(">Declared levy: 20%.<", File.ReadAllText(Path.Combine(dir, "later-a8-panel.svg")), StringComparison.Ordinal);
        Assert.Contains(">No levy declared yet.<", File.ReadAllText(Path.Combine(dir, "researched-a4-panel.svg")), StringComparison.Ordinal);
        Assert.Contains("era Bronze", StateLine(log, "later-a3"), StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerViewsPreview_AsASeparateProcess_ExitsZero_PaintsEveryView_TheEmpireLeviesInTheDevelopedState()
    {
        string dir = Path.Combine(_root, "views");
        (int exit, string output, string err) = RunSimUi(TimeSpan.FromMinutes(15), "--player-views-preview", dir);
        Assert.True(exit == 0, "sim-ui --player-views-preview exit " + exit + "\n" + Tail(output) + "\n" + Tail(err));

        foreach (string stem in new[] { "turn-1-a1", "developed-a3" })
            foreach (string kind in new[] { "settlement", "empire", "institutions" })
            {
                string svg = Path.Combine(dir, stem + "-" + kind + ".svg");
                Assert.True(File.Exists(svg), "not written: " + svg);
                Assert.Contains("player views preview: " + Path.GetFullPath(svg), output, StringComparison.Ordinal);
            }
        string[] log = File.ReadAllLines(Path.Combine(dir, "preview-log.txt"));
        AssertLogMatchesFiles(dir, log);
        StateLine(log, "turn-1-a1");
        StateLine(log, "developed-a3");

        // The EMPIRE view states the levy in words: none known at turn 1, the rig's declared levy when developed.
        Assert.Contains("do not yet know how to levy a tax", File.ReadAllText(Path.Combine(dir, "turn-1-a1-empire.svg")), StringComparison.Ordinal);
        Assert.Contains(">You levy a tax (20% declared;", File.ReadAllText(Path.Combine(dir, "developed-a3-empire.svg")), StringComparison.Ordinal);
    }

    /// <summary>
    /// THE OTHER FOUR PREVIEW TOOLS, the same way (adjacent hardening, 2026-10-05: none of them was run by any test
    /// either). Each must exit 0, print one line per file it wrote, and write exactly the SVGs it prints — the
    /// count is each tool's fixed set of states. <c>--r2a-preview</c> carries the same tax-gate rig as the action
    /// preview (its pre-/post-Trade states know the Taxation civic) and a 600-turn city-state (~1.5 min).
    /// </summary>
    [Theory]
    [InlineData("--research-preview", "research preview: ", 10)]
    [InlineData("--age-preview", "age preview: ", 9)]
    [InlineData("--era-preview", "era preview: ", 27)]
    [InlineData("--r2a-preview", "r2a preview: ", 8)]
    public void EveryOtherPreviewTool_AsASeparateProcess_ExitsZero_AndWritesWhatItPrints(string flag, string prefix, int svgs)
    {
        string dir = Path.Combine(_root, flag.TrimStart('-'));
        (int exit, string output, string err) = RunSimUi(TimeSpan.FromMinutes(15), flag, dir);
        Assert.True(exit == 0, "sim-ui " + flag + " exit " + exit + "\n" + Tail(output) + "\n" + Tail(err));

        var printed = new List<string>();
        foreach (string line in output.Split('\n'))
            if (line.StartsWith(prefix, StringComparison.Ordinal)) printed.Add(line[prefix.Length..].TrimEnd('\r'));
        Assert.Equal(svgs, printed.Count);
        foreach (string path in printed)
        {
            Assert.True(File.Exists(path), flag + " printed " + path + ", which does not exist");
            Assert.StartsWith(Path.GetFullPath(dir), path, StringComparison.Ordinal);
        }
        Assert.Equal(svgs, Directory.GetFiles(dir, "*.svg").Length);
        // Where the tool logs hashes (era, r2a), each hashed file must match; the age log records the run, not hashes.
        string log = Path.Combine(dir, "preview-log.txt");
        if (File.Exists(log)) AssertLogMatchesFiles(dir, File.ReadAllLines(log), everySvgListed: false);
    }

    // ------------------------------------------------------------------ the rig, in process

    [Fact]
    public void TheRigsTaxGate_IsReadFromContent_EveryNodeTheRequirementNames()
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        string[] gate = ActionSurfacePreview.TaxationGate(cfg);
        Sim.Core.Systems.ClassMobility.Predicate requirement = ResearchContentLoader.ParseRequirement(
            cfg.Research!, cfg.Governance!.TaxationRequires, "test");
        Assert.Equal(requirement.AtomIds.Count, gate.Length);
        for (int i = 0; i < gate.Length; i++) Assert.Equal(cfg.Research!.Nodes[requirement.AtomIds[i]].Id, gate[i]);
        // The refinement the rig used to grant is not the gate (R5): granting it alone leaves the edict closed.
        Assert.DoesNotContain("arithmetic_babylonian", gate);
    }

    [Fact]
    public void TheRigsAge_IsAtOrPastEveryGateNodesOwnAge()
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        foreach (string id in ActionSurfacePreview.TaxationGate(cfg))
        {
            string age = cfg.Research!.Nodes[cfg.Research.IndexOfId(id)].Age;   // "A3"
            Assert.StartsWith("A", age, StringComparison.Ordinal);
            int nodeAge = int.Parse(age.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture);
            Assert.True(ActionSurfacePreview.RigAge >= nodeAge,
                "the rig stands at Age " + ActionSurfacePreview.RigAge + " but the gate node " + id + " is an " + age + " capability");
        }
    }

    [Fact]
    public void TheLaterRig_OpensTheGate_DeclaresItsLevyThroughTheOrderPath_AndStandsAtTheRigAge()
    {
        ActionSurfacePreview.State later = ActionSurfacePreview.LaterRig();
        SimConfig cfg = later.Session.Config;
        PolityId me = UiPlayer.Empire;
        Assert.True(Governance.CanLevyTax(later.World, cfg, me));
        Assert.Equal(ActionSurfacePreview.RigAge, AgeQuery.CurrentAge(later.World, cfg.Ages!, me));
        // The levy was legislated by the order the rig emitted and is in force: 20 % declared.
        Assert.True(Governance.HasPolicy(later.World, me));
        Assert.Equal(0.20, Governance.NominalTaxRate(later.World, me));
        foreach (string id in ActionSurfacePreview.TaxationGate(cfg))
            Assert.True(ResearchQuery.IsCompleted(later.World, me, cfg.Research!.Nodes[cfg.Research.IndexOfId(id)].Key), id + " not completed");
    }

    [Fact]
    public void TheR2aPreTradeRig_KnowsTaxation_ButNotTrade()
    {
        // Its description says taxation is known; until 2026-10-05 that was false (it granted a refinement).
        ActionSurfacePreview.State pre = R2aPreview.PreTrade();
        SimConfig cfg = pre.Session.Config;
        Assert.True(Governance.CanLevyTax(pre.World, cfg, UiPlayer.Empire));
        Assert.Contains("Taxation", pre.Description, StringComparison.Ordinal);
        Assert.False(TradeQuery.KnowsTrade(pre.World, cfg, UiPlayer.Empire));
    }
}
