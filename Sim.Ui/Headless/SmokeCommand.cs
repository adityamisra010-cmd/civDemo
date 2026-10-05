using System.Globalization;
using System.Runtime.InteropServices;

namespace Sim.Ui.Headless;

/// <summary>
/// <c>Sim.Ui --smoke [--ai-empires N] [--monkey N] [--report PATH] [--state NAME]...</c> — the playability gate
/// (<see cref="PlayabilityGate"/>) with no window and no GPU: a real native ImGui context, the real per-frame UI,
/// scripted input. Exit 0 when every control passed or is not offered with a stated reason and no frame had a
/// defect; 1 otherwise; a native ImGui assertion aborts the process (non-zero) — which is why the tests run this
/// as a SEPARATE process. <c>--smoke-control-prefix</c> is the control experiment: the same UI with the renderer
/// capability the fix declares switched OFF, driven to the Research screen — it must abort with ImGui's 16-bit
/// assertion, proving the gate can see the crash the Director hit.
/// </summary>
public static class SmokeCommand
{
    public static bool Wants(string[] args) =>
        Array.IndexOf(args, "--smoke") >= 0 || Array.IndexOf(args, "--smoke-control-prefix") >= 0 || Array.IndexOf(args, "--frame-cost") >= 0;

    public static int Run(string[] args, int? aiEmpires)
    {
        QuietNativeAsserts();
        StartWatchdog(TimeSpan.FromMinutes(Arg(args, "--timeout-minutes") ?? 45));
        string assets = Sim.Ui.Art.AssetManifest.DefaultRoot();
        string work = Path.Combine(Path.GetTempPath(), "sim-ui-smoke-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        try
        {
            if (Array.IndexOf(args, "--smoke-control-prefix") >= 0) return ControlPrefix(assets, work);
            if (Array.IndexOf(args, "--frame-cost") >= 0)
            {
                Console.Write(FrameCost.Run(assets, Arg(args, "--rounds") ?? 5));
                return 0;
            }
            var states = new List<string>();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--state") states.Add(args[i + 1]);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            GateReport report = PlayabilityGate.Run(new GateOptions(assets, work, aiEmpires,
                MonkeySteps: Arg(args, "--monkey") ?? 400,
                Log: line => Console.WriteLine("[" + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s] " + line),
                OnlyStates: states.Count > 0 ? states : null));
            Console.WriteLine();
            Console.Write(report.Markdown());
            Console.WriteLine();
            Console.Write(report.Details());
            foreach (string p in report.Problems) Console.WriteLine("PROBLEM: " + p);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"smoke: {report.States.Count} states, {report.Rows.Count} checks: {report.Count(GateResult.Pass)} pass, {report.Count(GateResult.NotOffered)} not offered, {report.Count(GateResult.Fail)} FAIL; {report.Problems.Count} problems; {report.Frames} frames, {report.MonkeyActions} monkey actions; largest draw list {report.MaxListVertices} vertices; {report.Milliseconds / 1000.0:0.0} s"));
            if (ReportPath(args) is { } path) File.WriteAllText(path, ReportFile(report));
            Console.WriteLine(report.Ok ? "SMOKE PASSED" : "SMOKE FAILED");
            return report.Ok ? 0 : 1;
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The pre-fix configuration through the real UI: open Research at 1280×800 on the canonical turn-1
    /// world and move over the tree. Expected: ImGui aborts the process with its 16-bit assertion.</summary>
    private static int ControlPrefix(string assets, string work)
    {
        UiSession session = UiSession.Start(42);
        using UiFrameHarness h = UiFrameHarness.Start(session, work, assets, vtxOffset: false);
        Console.WriteLine("control: RendererHasVtxOffset NOT declared (the pre-fix renderer); opening Research");
        Console.Out.Flush();
        h.ClickControl("band-research");
        for (int y = 180; y < 800; y += 20)
            for (int x = 0; x < 876; x += 24)
            {
                h.MoveTo(x, y);
                Console.Out.Flush();
            }
        Console.WriteLine("CONTROL DID NOT REPRODUCE: largest draw list " + h.MaxListVertices.ToString(CultureInfo.InvariantCulture));
        return 2;
    }

    private static string? ReportPath(string[] args)
    {
        int at = Array.IndexOf(args, "--report");
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static int? Arg(string[] args, string name)
    {
        int at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;
    }

    /// <summary>The coverage report file: the table, the reasons, the problems, the states and how each was made.</summary>
    public static string ReportFile(GateReport r)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<!-- generated by `Sim.Ui --smoke --report`; do not edit by hand -->\n\n");
        sb.Append("### States\n\n");
        foreach (string n in r.Notes) sb.Append("- ").Append(n).Append('\n');
        sb.Append("\n### Coverage (control x state)\n\nPASS = clicked, no crash, the intended transition happened. ")
          .Append("n/o = not offered in that state, with the reason the UI gives (below). FAIL = crash, silent no-op or wrong transition.\n\n");
        sb.Append(r.Markdown());
        sb.Append("\n### Not offered (with the reason the player sees) and failures\n\n").Append(r.Details());
        sb.Append("\n### Frame defects\n\n").Append(r.Problems.Count == 0 ? "none\n" : "");
        foreach (string p in r.Problems) sb.Append("- ").Append(p).Append('\n');
        sb.Append(string.Create(CultureInfo.InvariantCulture,
            $"\n### Totals\n\n{r.States.Count} states, {r.Rows.Count} checks: {r.Count(GateResult.Pass)} pass, {r.Count(GateResult.NotOffered)} not offered, {r.Count(GateResult.Fail)} fail; {r.Problems.Count} frame defects; {r.Frames} frames; {r.MonkeyActions} monkey actions; largest single draw list {r.MaxListVertices} vertices.\n"));
        return sb.ToString();
    }

    // ------------------------------------------------------------------ headless process hygiene

    /// <summary>
    /// On Windows a failed C-runtime assert opens a MODAL DIALOG ("Microsoft Visual C++ Runtime Library —
    /// Assertion failed!", the dialog of the Director's crash), which on a CI runner blocks forever instead of
    /// failing. ImGui.NET's win-x64 cimgui.dll links the C runtime STATICALLY (its imports are IMM32, KERNEL32,
    /// USER32, SHELL32 only), so the process-wide <c>_set_error_mode</c> of ucrtbase cannot reach its asserts —
    /// MEASURED: CI run 37291800840's control hung until its 5-minute watchdog. So a watcher thread looks for that
    /// dialog among this process's windows, copies its text to stderr and exits 134 (the Linux abort code), so a
    /// native assertion fails the gate in under a second with the assertion's own words. The ucrtbase setting is
    /// still applied for any code that uses the shared runtime.
    /// </summary>
    private static void QuietNativeAsserts()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            _set_error_mode(1);                       // _OUT_TO_STDERR
            _set_abort_behavior(0, 0x1 | 0x2);        // clear _WRITE_ABORT_MSG | _CALL_REPORTFAULT
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        var watcher = new Thread(WatchForAssertDialogs) { IsBackground = true, Name = "smoke-assert-dialog-watcher" };
        watcher.Start();
    }

    private static void WatchForAssertDialogs()
    {
        uint me = (uint)Environment.ProcessId;
        while (true)
        {
            Thread.Sleep(200);
            IntPtr found = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid != me) return true;
                string title = WindowText(hwnd);
                if (!title.Contains("Runtime Library", StringComparison.Ordinal) && !title.Contains("Assert", StringComparison.OrdinalIgnoreCase)) return true;
                found = hwnd;
                return false;
            }, IntPtr.Zero);
            if (found == IntPtr.Zero) continue;
            var text = new System.Text.StringBuilder(WindowText(found));
            EnumChildWindows(found, (child, _) => { string t = WindowText(child); if (t.Length > 0) text.Append(" | ").Append(t); return true; }, IntPtr.Zero);
            Console.Error.WriteLine("NATIVE ASSERTION DIALOG: " + text.ToString().Replace('\n', ' ').Replace('\r', ' '));
            Console.Error.Flush();
            Console.Out.Flush();
            Environment.Exit(134);
        }
    }

    private static string WindowText(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(2048);
        GetWindowTextW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hwnd, System.Text.StringBuilder text, int max);

    [DllImport("ucrtbase.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int _set_error_mode(int mode);

    [DllImport("ucrtbase.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint _set_abort_behavior(uint flags, uint mask);

    /// <summary>A hung run is a failure, not a wait: exit 3 after <paramref name="limit"/>.</summary>
    private static void StartWatchdog(TimeSpan limit)
    {
        var t = new Thread(() =>
        {
            Thread.Sleep(limit);
            Console.Error.WriteLine("SMOKE TIMED OUT after " + limit.TotalMinutes.ToString(CultureInfo.InvariantCulture) + " min (non-termination)");
            Environment.Exit(3);
        }) { IsBackground = true, Name = "smoke-watchdog" };
        t.Start();
    }
}
