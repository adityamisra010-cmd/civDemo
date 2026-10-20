using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Info;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Actions;

/// <summary>
/// M5 polish (directive §9, §4 "visibly disabled") — the action surface's INSPECTABLE REGIONS and its LOCKED LINES. Every
/// nameable thing the surface paints (a labour activity, a good in a production or blocker line, a project, a craft, a
/// formation, the research target, a standing capability, the tax edict, a road class) is recorded with its
/// <see cref="InfoSubject"/> as it is painted; the host opens the card on Shift+click instead of dispatching the click.
/// The locked block names each governing domain the query does not list, with the gating predicate's reason.
/// </summary>
public sealed partial class ActionSurfaceScreen
{
    private List<InfoHit> _info = [];
    private (SimConfig? Cfg, List<(string Token, InfoSubject Subject)> Tokens) _goodTokens;

    /// <summary>The inspectable regions painted last frame (screen coordinates).</summary>
    public IReadOnlyList<InfoHit> InfoHits => _info;

    /// <summary>The top-most inspectable region at a point, or null.</summary>
    public InfoHit? InfoAt(double x, double y) => InfoRegistry.At(_info, x, y);

    private void Info(RectD r, InfoSubject subject, string label)
    {
        if (r.W > 0.5 && r.H > 0.5) _info.Add(new InfoHit(r, subject, label));
    }

    /// <summary>Every good's name as a token (goods.json names; data, never a literal).</summary>
    private List<(string Token, InfoSubject Subject)> GoodTokens()
    {
        if (ReferenceEquals(_goodTokens.Cfg, _cfg) && _goodTokens.Tokens is not null) return _goodTokens.Tokens;
        var list = new List<(string, InfoSubject)>();
        if (_cfg?.Goods is { } goods) foreach (GoodEntry g in goods.Goods) list.Add((g.Name, InfoSubject.OfGood(g.Id)));
        _goodTokens = (_cfg, list);
        return list;
    }

    private static List<(string Token, InfoSubject Subject)> StandingTokens(StandingBlock s)
    {
        var list = new List<(string, InfoSubject)>();
        if (s.Subjects.IsDefault) return list;
        for (int i = 0; i < s.Items.Length && i < s.Subjects.Length; i++) list.Add((s.Items[i], s.Subjects[i]));
        return list;
    }

    /// <summary>The recipe name a Production entry stands for (its descriptor key, "production." + recipe name).</summary>
    private static string RecipeOf(ProductionEntry e) =>
        e.Action.Key.StartsWith("production.", StringComparison.Ordinal) ? e.Action.Key["production.".Length..] : e.Name;

    /// <summary><see cref="Wrapped"/>, recording each token's rect on the line(s) it is painted on (whole words only; a
    /// token the wrap splits — UR-6 sets these lines at the body role, so a long standing item can break — is recorded
    /// on each line it occupies).</summary>
    private double WrappedInfo(DrawList d, ITextMeasure m, string text, double x, double y, double w, double size, Rgba color,
        FontRole role, List<(string Token, InfoSubject Subject)> tokens)
    {
        List<string> lines = ThemeText.Wrap(m, T, text, size, w, role);
        var starts = new int[lines.Count];
        int len = 0;
        for (int i = 0; i < lines.Count; i++) { starts[i] = len; len += lines[i].Length + 1; }
        string joined = string.Join(" ", lines);
        for (int i = 0; i < lines.Count; i++) d.Write(T, x, y + i * L(size), lines[i], size, color, TextAlign.Left, role);
        foreach ((string token, InfoSubject subject) in tokens)
        {
            int at = 0;
            while (token.Length > 0 && (at = joined.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
            {
                int end = at + token.Length;
                bool before = at == 0 || !char.IsLetterOrDigit(joined[at - 1]) && joined[at - 1] != '-';
                bool after = end >= joined.Length || !char.IsLetterOrDigit(joined[end]) && joined[end] != '-';
                if (before && after)
                    for (int i = 0; i < lines.Count; i++)
                    {
                        int a = Math.Max(at, starts[i]) - starts[i], b = Math.Min(end, starts[i] + lines[i].Length) - starts[i];
                        if (b <= a) continue;
                        double x0 = x + m.Width(T, lines[i][..a], size, role);
                        Info(new RectD(x0, y + i * L(size), m.Width(T, lines[i][a..b], size, role), L(size)), subject, token);
                    }
                at = end;
            }
        }
        return y + lines.Count * L(size);
    }

    /// <summary>The LOCKED governing domains (verify G2): each with a locked mark, its name and the gating predicate's
    /// reason, e.g. "Tax edict — needs Taxation (Civics) and the Bronze Age (Age III)". No control: Shift+click opens
    /// its card, which names the research to do. Set by role (UR-6): the name bold at Body, the reason at Body in the
    /// progress family's text ink, the hint at Caption.</summary>
    private double PaintLocked(DrawList d, ITextMeasure m, ActionSurfaceModel model, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Not open to us yet" : "Locked - not open yet", x, y, w, flat, 975);
        int k = 0;
        double ind = Sp(22);
        foreach (LockedDomain l in model.Locked)
        {
            EraMarks.State(d, t, x + Sp(8), y + L(Body) / 2, Sp(5.5), MarkKind.Locked, 0, 976 + k, dim: true);
            Info(new RectD(x + ind, y, Math.Min(w - ind, m.Width(t, l.Label, BodyBold, FontRole.Heading)), L(BodyBold)), l.Subject, l.Label);
            y = Wrapped(d, m, l.Label, x + ind, y, w - ind, BodyBold, t.Ink.TextSoft, FontRole.Heading);
            y = Wrapped(d, m, l.Reason, x + ind, y, w - ind, Second, t.TextInk.Progress);
            y += Math.Max(Sp(2), t.Density.Gap * Scale * 0.3);
            k++;
        }
        return Wrapped(d, m, "Shift+click a name for what it needs.", x, y, w, Caption, t.Ink.TextSoft);
    }

    /// <summary>The rect a block heading's text occupies (the role and face <see cref="Heading"/> sets it in).</summary>
    private RectD HeadingRect(ITextMeasure m, string text, double x, double y, double w, bool flat)
    {
        FontRole role = flat ? FontRole.Heading : FontRole.Caps;
        double size = Px(TypeRole.Heading, role);
        return new RectD(x, y, Math.Min(w, m.Width(T, text, size, role) + Sp(8)), L(size));
    }

    /// <summary>Records the good tokens on one painted line of a rich <see cref="Blocker"/> (words at their x; whole
    /// words only, as <see cref="WrappedInfo"/>).</summary>
    private void BlockerTokens(ITextMeasure m, List<(RichWord Word, double X, double W)> line, double y, double size,
        List<(string Token, InfoSubject Subject)>? tokens)
    {
        if (tokens is null || line.Count == 0) return;
        var starts = new int[line.Count];
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < line.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            starts[i] = sb.Length;
            sb.Append(line[i].Word.Text);
        }
        string text = sb.ToString();
        double XAt(int offset)
        {
            int i = line.Count - 1;
            while (i > 0 && starts[i] > offset) i--;
            int within = Math.Min(offset - starts[i], line[i].Word.Text.Length);
            FontRole role = line[i].Word.Bold ? FontRole.Heading : FontRole.Body;
            return line[i].X + (within <= 0 ? 0 : m.Width(T, line[i].Word.Text[..within], size, role));
        }
        foreach ((string token, InfoSubject subject) in tokens)
        {
            int at = 0;
            while (token.Length > 0 && (at = text.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
            {
                bool before = at == 0 || !char.IsLetterOrDigit(text[at - 1]) && text[at - 1] != '-';
                bool after = at + token.Length >= text.Length || !char.IsLetterOrDigit(text[at + token.Length]) && text[at + token.Length] != '-';
                if (before && after)
                {
                    double x0 = XAt(at), x1 = XAt(at + token.Length);
                    if (x1 > x0) Info(new RectD(x0, y, x1 - x0, L(size)), subject, token);
                }
                at += token.Length;
            }
        }
    }

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
}
