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

    /// <summary><see cref="Wrapped"/>, recording each token's rect on the line it is painted on (whole words only).</summary>
    private double WrappedInfo(DrawList d, ITextMeasure m, string text, double x, double y, double w, double size, Rgba color,
        FontRole role, List<(string Token, InfoSubject Subject)> tokens)
    {
        foreach (string line in ThemeText.Wrap(m, T, text, size, w, role))
        {
            d.Write(T, x, y, line, size, color, TextAlign.Left, role);
            foreach ((string token, InfoSubject subject) in tokens)
            {
                int at = 0;
                while (token.Length > 0 && (at = line.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
                {
                    bool before = at == 0 || !char.IsLetterOrDigit(line[at - 1]) && line[at - 1] != '-';
                    bool after = at + token.Length >= line.Length || !char.IsLetterOrDigit(line[at + token.Length]) && line[at + token.Length] != '-';
                    if (before && after)
                    {
                        double x0 = x + m.Width(T, line[..at], size, role);
                        Info(new RectD(x0, y, m.Width(T, token, size, role), L(size)), subject, token);
                    }
                    at += token.Length;
                }
            }
            y += L(size);
        }
        return y;
    }

    /// <summary>The LOCKED governing domains (verify G2): each with a locked mark, its name and the gating predicate's
    /// reason, e.g. "Tax edict — needs Taxation (Civics) and the Bronze Age (Age III)". No control: Shift+click opens
    /// its card, which names the research to do.</summary>
    private double PaintLocked(DrawList d, ITextMeasure m, ActionSurfaceModel model, double x, double y, double w, bool flat)
    {
        EraTheme t = T;
        y = Heading(d, m, flat ? "Not open to us yet" : "Locked - not open yet", x, y, w, flat, 975);
        int k = 0;
        foreach (LockedDomain l in model.Locked)
        {
            EraMarks.State(d, t, x + 7, y + L(13) / 2, 5, MarkKind.Locked, 0, 976 + k, dim: true);
            Info(new RectD(x + 18, y, Math.Min(w - 18, m.Width(t, l.Label, 13, FontRole.Heading)), L(13)), l.Subject, l.Label);
            y = Wrapped(d, m, l.Label, x + 18, y, w - 18, 13, t.Ink.TextSoft, FontRole.Heading);
            y = Wrapped(d, m, l.Reason, x + 18, y, w - 18, 11.5, t.Semantic.Progress);
            y += Math.Max(2, t.Density.Gap * 0.3);
            k++;
        }
        return Wrapped(d, m, "Shift+click a name for what it needs.", x, y, w, 11, t.Ink.TextDim);
    }

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);
}
