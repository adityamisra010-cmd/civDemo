using System.Text;
using Sim.Core.State;
using Sim.Ui.Actions;
using Sim.Ui.Ages;
using Sim.Ui.Info;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Headless;

/// <summary>One left-button press the harness made: the frame, the point, whether Shift was held, and the coverage keys
/// (<see cref="UiCoverage"/>) of what the player saw under the pointer — the last frame's controls and regions.</summary>
public sealed record UiPress(int Frame, int X, int Y, bool Shift, IReadOnlyList<string> Under);

/// <summary>A drawn control family, hit kind or inspectable kind the gate deliberately does not press, and why.</summary>
public sealed record CoverageAllowance(string Key, string Reason);

/// <summary>
/// M5 workstream P6 — THE GATE'S CONTROL COVERAGE. The gate's checks are a hand-written script (StateRun.Sweep); this
/// is what keeps the script honest. Everything the UI draws that a player can act on has a KEY:
/// <list type="bullet">
/// <item><c>control:&lt;family&gt;</c> — an ImGui widget recorded in <see cref="UiControls"/>, by FAMILY: a run of digits
/// in its name (a settlement, good, need or metric id) reads '#', and the TURN audit's per-account lines are one
/// family, <c>audit-*</c> (the gate unfolds the first);</item>
/// <item><c>hit:&lt;surface&gt;.&lt;kind&gt;</c> — a painted hit region of a screen that is showing: the research screen
/// (<see cref="HitKind"/>), the Age panel and flow (<see cref="AgeHit"/>), the POLICY action surface
/// (<see cref="ActionHitKind"/>), the pinned info card (<see cref="InfoHitKind"/>);</item>
/// <item><c>info:&lt;kind&gt;</c> — an inspectable region (<see cref="InfoRegistry"/>) of an <see cref="InfoKind"/>.</item>
/// </list>
/// The harness collects every key DRAWN in any frame of a state's run (sweep and monkey alike) and records, at every
/// press, the keys under the pointer. A gate row COVERS the keys pressed while it ran — a plain press covers controls
/// and hit kinds, a Shift press covers inspectable kinds (it inspects; it does not perform the click) — and only a
/// PASSING row counts. A key drawn in a state but covered by no passing row of that state, and not on
/// <see cref="Allowlist"/> with a reason, is a FAIL: a control shipped without a gate row would otherwise be exercised
/// only by the monkey's luck.
/// </summary>
public static class UiCoverage
{
    public const string ControlPrefix = "control:";
    public const string HitPrefix = "hit:";
    public const string InfoPrefix = "info:";

    /// <summary>
    /// Keys a state may draw without a gate row pressing them, each with its reason. Every entry is a deliberate,
    /// reviewed exception; the coverage tests fail on an entry without a reason or a key that is not a key.
    /// </summary>
    public static IReadOnlyList<CoverageAllowance> Allowlist { get; } =
    [
    ];

    /// <summary>The coverage family of a control name: digit runs read '#'; the TURN audit's lines are <c>audit-*</c>.</summary>
    public static string Family(string name)
    {
        if (name.StartsWith("audit-", StringComparison.Ordinal)) return "audit-*";
        var sb = new StringBuilder(name.Length);
        bool digits = false;
        foreach (char c in name)
        {
            bool d = c is >= '0' and <= '9';
            if (d && !digits) sb.Append('#');
            else if (!d) sb.Append(c);
            digits = d;
        }
        return sb.ToString();
    }

    // The keys are built once (every frame of every gate state asks for them).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ControlKeys = new(StringComparer.Ordinal);
    private static readonly string[] InfoKeys = Keys<InfoKind>(InfoPrefix);
    private static readonly string[] ResearchKeys = Keys<HitKind>(HitPrefix + "research.");
    private static readonly string[] AgeKeys = Keys<AgeHit>(HitPrefix + "age.");
    private static readonly string[] PolicyKeys = Keys<ActionHitKind>(HitPrefix + "policy.");
    private static readonly string[] CardKeys = Keys<InfoHitKind>(HitPrefix + "card.");

    private static string[] Keys<T>(string prefix) where T : struct, Enum
    {
        T[] values = Enum.GetValues<T>();
        int max = 0;
        foreach (T v in values) max = Math.Max(max, Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture));
        var keys = new string[max + 1];
        foreach (T v in values) keys[Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture)] = prefix + v.ToString();
        return keys;
    }

    public static string ControlKey(string name) => ControlKeys.GetOrAdd(name, n => ControlPrefix + Family(n));

    public static string InfoKey(InfoKind kind) => InfoKeys[(int)kind];

    public static string ResearchKey(HitKind kind) => ResearchKeys[(int)kind];

    public static string AgeKey(AgeHit kind) => AgeKeys[(int)kind];

    public static string PolicyKey(ActionHitKind kind) => PolicyKeys[(int)kind];

    public static string CardKey(InfoHitKind kind) => CardKeys[(int)kind];

    /// <summary>The keys of the controls a <see cref="UiControls"/> register recorded in its last frame.</summary>
    public static void Controls(UiControls controls, ISet<string> into)
    {
        foreach (UiControl c in controls.Last) into.Add(ControlKey(c.Name));
    }

    /// <summary>Every key the UI drew in its last frame: controls, the hit regions of the screens that are showing,
    /// and the inspectable regions.</summary>
    public static void Drawn(GameUi ui, ISet<string> into)
    {
        Controls(ui.Controls, into);
        foreach ((RectD _, string key) in Hits(ui)) into.Add(key);
        foreach (InfoHit h in ui.InfoRegistry.Hits) into.Add(InfoKey(h.Subject.Kind));
    }

    /// <summary>
    /// What a press at (<paramref name="x"/>, <paramref name="y"/>) lands on, from the last frame's registers (what the
    /// player saw): with Shift held, the inspectable region a Shift+click opens; without, every control and screen hit
    /// region containing the point.
    /// </summary>
    public static IReadOnlyList<string> Under(GameUi ui, double x, double y, bool shift)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        if (shift)
        {
            if (ui.InfoRegistry.HitTest(x, y) is { } hit) keys.Add(InfoKey(hit.Subject.Kind));
            return [.. keys];
        }
        foreach (UiControl c in ui.Controls.Last)
            if (x >= c.X0 && x < c.X1 && y >= c.Y0 && y < c.Y1) keys.Add(ControlKey(c.Name));
        foreach ((RectD r, string key) in Hits(ui))
            if (r.Contains(x, y)) keys.Add(key);
        return [.. keys];
    }

    /// <summary>The hit regions of the screens showing in the last frame (a screen keeps its last list while closed).</summary>
    private static IEnumerable<(RectD Rect, string Key)> Hits(GameUi ui)
    {
        if (ui.ProgressionOpen && ui.Progression is { } screen)
            foreach (HitRegion r in screen.Hits) yield return (r.Rect, ResearchKey(r.Kind));
        if (!ui.ProgressionOpen && (ui.AgePanelOpen || ui.Age.FlowOpen))
            foreach (AgeHitRegion r in ui.Age.Hits) yield return (r.Rect, AgeKey(r.Kind));
        if (!ui.ProgressionOpen && !ui.Inspector.IsOpen && ui.OpenSection == Section.Policy)
            foreach (ActionHit r in ui.Actions.Hits) yield return (r.Rect, PolicyKey(r.Kind));
        if (ui.Inspector.IsOpen)
            foreach (InfoPanelHit r in ui.Inspector.Hits) yield return (r.Rect, CardKey(r.Kind));
    }

    /// <summary>
    /// The keys <paramref name="seen"/> that no PASSING row of <paramref name="rows"/> covers and that
    /// <paramref name="allow"/> does not excuse, in ordinal order. A not-offered or failed row covers nothing.
    /// </summary>
    public static IReadOnlyList<string> Uncovered(IEnumerable<string> seen, IEnumerable<GateRow> rows, IReadOnlyList<CoverageAllowance> allow)
    {
        var covered = new SortedSet<string>(StringComparer.Ordinal);
        foreach (GateRow r in rows)
            if (r.Result == GateResult.Pass)
                foreach (string k in r.Covers) covered.Add(k);
        foreach (CoverageAllowance a in allow) covered.Add(a.Key);
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string k in seen) if (!covered.Contains(k)) missing.Add(k);
        return [.. missing];
    }
}
