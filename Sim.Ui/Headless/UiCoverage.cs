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
/// (<see cref="UiCoverage"/>) of what the press REACHED: a plain press, the controls ImGui activated and the hit regions
/// the screens' own Click resolved, from the press to its release; a Shift press, the inspectable region it opens.</summary>
public sealed record UiPress(int Frame, int X, int Y, bool Shift, IReadOnlyList<string> Reached);

/// <summary>The painted screens' resolved-click counts (<see cref="ResolvedClick{TKind}"/>) when a press began: what
/// they resolve after it is the press's.</summary>
public readonly record struct ClickMarks(object? Research, long ResearchSerial, long Age, long Policy, long Card);

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
/// The harness collects every key DRAWN in any frame of a state's run (sweep and monkey alike: a control is drawn when
/// it is submitted, scrolled out of view or not — conservative, a row must reach it) and records, at every press, the
/// keys the press REACHED. A plain press reaches what the UI itself routed it to, never what merely lies under the
/// point: the controls ImGui ACTIVATED (<see cref="UiControl.Activated"/>: hovered in the window on top, inside the
/// visible clip, not disabled) and the hit region each painted screen's own Click resolved
/// (<see cref="ResolvedClick{TKind}"/>), from the press to its release. A control scrolled out of its panel, covered
/// by another window or disabled under the modal ADVANCE AGE flow is not reached, whatever its laid-out rect contains.
/// A Shift press reaches the inspectable kind it opens (it inspects; it does not perform the click). A gate row COVERS
/// the keys its presses reached, and only a PASSING row counts. A key drawn in a state but covered by no passing row of
/// that state, and not on <see cref="Allowlist"/> with a reason, is a FAIL: a control shipped without a gate row would
/// otherwise be exercised only by the monkey's luck.
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

    /// <summary>What a Shift press at (<paramref name="x"/>, <paramref name="y"/>) inspects: the inspectable region the
    /// last frame registered there (what the player saw; the registry is cut to each panel's visible rect and is
    /// top-most first, as the Shift+click itself resolves it).</summary>
    public static IReadOnlyList<string> Inspected(GameUi ui, double x, double y) =>
        ui.InfoRegistry.HitTest(x, y) is { } hit ? [InfoKey(hit.Subject.Kind)] : [];

    /// <summary>The screens' resolved-click counts now (taken when a press begins).</summary>
    public static ClickMarks Marks(GameUi ui) =>
        new(ui.Progression, ui.Progression?.LastClick.Serial ?? 0, ui.Age.LastClick.Serial, ui.Actions.LastClick.Serial, ui.Inspector.LastClick.Serial);

    /// <summary>
    /// What a plain press reached in the frame just drawn (called after each of its frames, press to release): every
    /// control ImGui ACTIVATED in that frame, and the hit region each painted screen's own Click resolved since
    /// <paramref name="marks"/>, which it then advances. Nothing is inferred from geometry: a laid-out rect that merely
    /// contains the point (a row scrolled away, a control under another window, a disabled one) is not reached.
    /// </summary>
    public static void Reached(GameUi ui, ref ClickMarks marks, ISet<string> into)
    {
        foreach (UiControl c in ui.Controls.Last)
            if (c.Activated) into.Add(ControlKey(c.Name));
        if (ui.Progression is { } screen
            && screen.LastClick.Serial != (ReferenceEquals(screen, marks.Research) ? marks.ResearchSerial : 0))
            into.Add(ResearchKey(screen.LastClick.Kind));
        if (ui.Age.LastClick.Serial != marks.Age) into.Add(AgeKey(ui.Age.LastClick.Kind));
        if (ui.Actions.LastClick.Serial != marks.Policy) into.Add(PolicyKey(ui.Actions.LastClick.Kind));
        if (ui.Inspector.LastClick.Serial != marks.Card) into.Add(CardKey(ui.Inspector.LastClick.Kind));
        marks = Marks(ui);
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
