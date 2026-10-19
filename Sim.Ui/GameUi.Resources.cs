using System.Globalization;
using ImGuiNET;
using Sim.Ui.Art;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// M5 resources view (the Director, 2026-10-19: "yes build resources view. Remove the trends thing actually. Or make it my
/// empire specific. i should not be able to see other empire's data. trends and resource view get merged into the same
/// view/button."; docs/m5-resources-view-proposal.md) — THE PLAYER'S OWN STORES, in three places:
/// <list type="number">
/// <item><b>The resource strip</b> in the status band after FOOD: Food stores · Materials · Goods, each the empire total of
///   its shown goods (<see cref="EmpireStores"/>) with a rising / falling / steady mark against the start of last turn.
///   Each group is a control (<c>strip:Food</c>, <c>strip:Materials</c>, <c>strip:Goods</c>) that opens RESOURCES.</item>
/// <item><b>The hover card</b> under a group the pointer rests on: per good, the empire total, the change since last turn,
///   what is set aside for projects and the largest of the player's own holding settlements (<see cref="StoresCard"/>).</item>
/// <item><b>RESOURCES</b> (the former TRENDS button, the same slot and key 6): the stores table — goods × the player's
///   settlements, an empire column, the change and the set-aside, each row a control (<c>store-&lt;good id&gt;</c>) that
///   charts that good — the building/roads split's line where the EMPIRE view shows it, and the trend graph over the
///   player's own settlements only (<see cref="TrendsModel.Empire"/>; scope <c>trend-empire</c> or <c>trend-place</c>, the
///   latter for one of the player's settlements only).</item>
/// </list>
/// Nothing here reads a foreign settlement or polity: the strip, the card and the table read the player's controlled
/// settlements and own purse; the graph sums a turn's records over the settlements that were the player's on that turn
/// (<see cref="UiSession.Ownership"/>). UI state only — no order, no write, no hash moves. Counts only, never a "%".
/// </summary>
public sealed partial class GameUi
{
    /// <summary>The prefix of a strip group's control (its <see cref="StoreGroup"/> follows).</summary>
    public const string StripControlPrefix = "strip:";

    /// <summary>The prefix of a stores-table row's control (its good id follows).</summary>
    public const string StoreRowPrefix = "store-";

    /// <summary>A strip group's control name.</summary>
    public static string StripControl(StoreGroup group) => StripControlPrefix + group.ToString();

    /// <summary>A stores-table row's control name.</summary>
    public static string StoreRowControl(int good) => StoreRowPrefix + good.ToString(CultureInfo.InvariantCulture);

    // The player's stores, rebuilt with the HUD (selection change, End Turn, every order).
    private EmpireStores? _stores;
    // The building/roads split's line, shown where the EMPIRE view shows it (null when that view shows none).
    private string? _storesSplitLine;
    // The good the stores metric charts (a good id; the first shown good until the player picks a row).
    private int _storeGood = -1;
    // Whether the RESOURCES graph sums the player's empire (true) or shows the selected settlement of the player's.
    private bool _trendEmpireScope = true;
    // The strip group the pointer rests on in this frame (its hover card is painted at the end of the frame), and where.
    private StoreGroup? _stripHover;
    private Sim.Ui.Render.RectD _stripHoverRect;

    /// <summary>The player's stores as the strip, the cards and RESOURCES show them (null before the first refresh).</summary>
    public EmpireStores? Stores => _stores;

    /// <summary>The good the RESOURCES stores metric charts.</summary>
    public int StoreGood => _storeGood;

    /// <summary>Whether the RESOURCES graph is in empire scope (else the selected settlement, when it is the player's).</summary>
    public bool TrendEmpireScope => _trendEmpireScope;

    /// <summary>The strip group whose hover card was painted in the last frame, or null.</summary>
    public StoreGroup? StripHover { get; private set; }

    /// <summary>The hover card's rect in the last frame (meaningful while <see cref="StripHover"/> is set).</summary>
    public Sim.Ui.Render.RectD StripCardRect { get; private set; }

    /// <summary>The hover card's lines in the last frame (empty when none was painted).</summary>
    public IReadOnlyList<string> StripCardLines { get; private set; } = [];

    /// <summary>Rebuilds the stores (RefreshHud's cadence).</summary>
    private void RefreshStores()
    {
        _stores = EmpireStores.Build(_world, _session.PreviousWorld, _session.Config, UiPlayer.Empire);
        _storesSplitLine = PlayerViews.MaterialsSplitListed(_world, _session.Config, UiPlayer.Empire)
            ? AllocationText.EmpireLine(Sim.Core.State.ResourceAllocation.EffectivePercent(_world, UiPlayer.Empire),
                Sim.Core.State.Reservations.Enabled(_session.Config))
            : null;
        StoreGood[] shown = _stores.Shown;
        bool stillShown = false;
        foreach (StoreGood g in shown) if (g.Good == _storeGood) stillShown = true;
        if (!stillShown) _storeGood = shown.Length > 0 ? shown[0].Good : -1;
    }

    /// <summary>Whether <paramref name="settlement"/> is one of the player's settlements now.</summary>
    private bool IsOwnSettlement(int settlement) => _stores is { } st && st.ColumnOf(settlement) >= 0;

    // ================================================================== the strip

    /// <summary>The strip's groups in the order drawn.</summary>
    private StoreGroupSummary[] StripGroups() => _stores?.Groups ?? [];

    /// <summary>The width the strip takes: per group its caps label (when <paramref name="labels"/>), its figure and its
    /// mark, the groups <paramref name="itemGap"/> apart.</summary>
    private float StripWidth(bool labels, float capPx, float valuePx, float gap, float itemGap, float markW)
    {
        StoreGroupSummary[] groups = StripGroups();
        float w = 0f;
        for (int i = 0; i < groups.Length; i++)
        {
            if (i > 0) w += itemGap;
            if (labels) w += StyledWidth(EmpireStores.Label(groups[i].Group), capPx, Sim.Ui.Render.FontRole.Caps) + gap;
            w += StyledWidth(EmpireStores.Value(groups[i]), valuePx, Sim.Ui.Render.FontRole.Numeric) + markW;
        }
        return w;
    }

    /// <summary>
    /// Draws the strip from <paramref name="x"/> along the band and returns the x after it. Each group is a caps label (or,
    /// in a band too narrow for the labels, its first letter) over the baseline of its figure in the data face, then its
    /// mark; an invisible button over the whole group is the control (a click opens RESOURCES; resting on it shows the
    /// group's card). Its text is recorded in FrameText by the run helpers, the mark's meaning by the card.
    /// </summary>
    private float DrawResourceStrip(ImDrawListPtr dl, float x, float bandY, float top, float labelY, float valueY,
        float capPx, float valuePx, float gap, float itemGap, float markW, bool labels, float hitHeight)
    {
        StoreGroupSummary[] groups = StripGroups();
        Sim.Ui.Theme.EraTheme t = _frameTheme;
        float s = Scale;
        for (int i = 0; i < groups.Length; i++)
        {
            StoreGroupSummary g = groups[i];
            float x0 = x;
            string label = labels ? EmpireStores.Label(g.Group) : EmpireStores.Label(g.Group)[..1];
            x += StyledRun(dl, x, bandY + labelY, label, capPx, Sim.Ui.Render.FontRole.Caps, t.Ink.TextSoft) + gap;
            x += StyledRun(dl, x, bandY + valueY, EmpireStores.Value(g), valuePx, Sim.Ui.Render.FontRole.Numeric, t.Ink.Text);
            StoreTrend trend = EmpireStores.TrendOf(g.HasPrevious, g.Change);
            var d = new Sim.Ui.Render.DrawList();
            StoresCard.Mark(d, trend, x + markW / 2f, bandY + valueY + valuePx * 0.55, Math.Max(6.0, valuePx * 0.5), t.Ink.TextSoft);
            Backend.Render(dl, d);
            x += markW;
            ImGui.SetCursorScreenPos(new System.Numerics.Vector2(x0, bandY + top));
            bool clicked = InvisibleButton("##strip-" + g.Group.ToString(), new System.Numerics.Vector2(Math.Max(1f, x - x0), hitHeight));
            Controls.Record(StripControl(g.Group));
            if (ImGui.IsItemHovered())
            {
                DottedUnderline(dl, x0, x, bandY + top + hitHeight + 1f * s, t.Ink.TextSoft);
                _stripHover = g.Group;
                System.Numerics.Vector2 min = ImGui.GetItemRectMin(), max = ImGui.GetItemRectMax();
                _stripHoverRect = new Sim.Ui.Render.RectD(min.X, min.Y, max.X - min.X, max.Y - min.Y);
            }
            if (clicked) OpenResources();
            if (i < groups.Length - 1) x += itemGap;
        }
        return x;
    }

    /// <summary>Opens RESOURCES (a strip click): the section is shown even when the panel was folded or open on it.</summary>
    private void OpenResources()
    {
        _openSection = Section.Resources;
        if (!_inspector.IsOpen) FoldContext(false);
    }

    /// <summary>Paints the hover card of the strip group the pointer rested on this frame (on the foreground list, the info
    /// hint's way), under the band; clears it otherwise.</summary>
    private void DrawStoresCard()
    {
        StripHover = null;
        StripCardLines = [];
        StoreGroup? hover = _stripHover;
        _stripHover = null;
        if (hover is not { } group || _stores?.Group(group) is not { } g) return;
        _surface = FrameSurface.Status;
        IReadOnlyList<string> lines = EmpireStores.CardLines(g, id => _session.Names.Name(id));
        var d = new Sim.Ui.Render.DrawList();
        StripCardRect = StoresCard.Paint(d, Backend, _frameTheme, _stripHoverRect.X, _stripHoverRect.Y + _stripHoverRect.H + 10f * Scale,
            _viewportWidth, _viewportHeight, EmpireStores.CardTitle(group), lines, Scale);
        Backend.Render(ImGui.GetForegroundDrawList(), d);
        StripHover = group;
        StripCardLines = lines;
    }

    // ================================================================== RESOURCES: the stores table, then the trends

    /// <summary>RESOURCES — the player's own empire: its stores (goods × its settlements), the building/roads split's
    /// line, and the trend graph over its own settlements only.</summary>
    private void DrawResourcesSection()
    {
        DrawStoresTable();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawOwnTrends();
    }

    /// <summary>The stores table: one row per shown good (a Selectable that charts it), the empire's total, the change
    /// since last turn, what is set aside for projects, then one column per settlement of the player's.</summary>
    private void DrawStoresTable()
    {
        HeadingRow("Stores of your empire");
        if (_stores is not { } st || st.Settlements.Length == 0)
        {
            Text("Your empire holds no settlement.");
            return;
        }
        StoreGood[] shown = st.Shown;
        if (shown.Length == 0)
        {
            Text("Your settlements hold nothing yet.");
            return;
        }
        Func<int, string> name = _session.Names.Name;
        float pad = 14f * Scale;
        string[] heads = new string[4 + st.Settlements.Length];
        heads[0] = "Good";
        heads[1] = "Empire";
        heads[2] = "Change";
        heads[3] = "Set aside";
        for (int c = 0; c < st.Settlements.Length; c++) heads[4 + c] = name(st.Settlements[c]);
        var widths = new float[heads.Length];
        for (int c = 0; c < heads.Length; c++) widths[c] = ImGui.CalcTextSize(heads[c]).X;
        var cells = new string[shown.Length][];
        for (int r = 0; r < shown.Length; r++)
        {
            StoreGood g = shown[r];
            int gi = Array.IndexOf(st.AllGoods, g);
            cells[r] = new string[heads.Length];
            cells[r][0] = g.Name;
            cells[r][1] = EmpireStores.Figure(g.Total);
            cells[r][2] = EmpireStores.ChangeText(g.HasPrevious, g.Change);
            cells[r][3] = g.SetAside > 0 ? EmpireStores.Figure(g.SetAside) : "-";
            for (int c = 0; c < st.Settlements.Length; c++) cells[r][4 + c] = EmpireStores.Figure(st.Amounts[gi][c]);
            for (int c = 0; c < heads.Length; c++) widths[c] = Math.Max(widths[c], ImGui.CalcTextSize(cells[r][c]).X);
        }
        var colX = new float[heads.Length];
        float start = ImGui.GetCursorPosX();
        for (int c = 0; c < heads.Length; c++) colX[c] = c == 0 ? start : colX[c - 1] + widths[c - 1] + pad;

        // The table scrolls sideways in its OWN child (one column per settlement can outgrow the panel), so the lines and
        // the graph under it keep the panel's width.
        PushDataFont();
        float tableH = (shown.Length + 1) * ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ScrollbarSize + 6f * Scale;
        ImGui.BeginChild("stores-table", new System.Numerics.Vector2(0f, tableH), ImGuiChildFlags.None,
            ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoBackground);
        for (int c = 0; c < heads.Length; c++)
        {
            if (c > 0) ImGui.SameLine(colX[c]);
            Text(heads[c]);
        }
        for (int r = 0; r < shown.Length; r++)
        {
            int good = shown[r].Good;
            bool picked = Selectable(cells[r][0] + "##" + StoreRowControl(good), _storeGood == good);
            Controls.Record(StoreRowControl(good));
            if (picked) PickStoreGood(good);
            for (int c = 1; c < heads.Length; c++)
            {
                ImGui.SameLine(colX[c]);
                Text(cells[r][c]);
            }
        }
        ImGui.EndChild();
        PopDataFont();
        ImGui.Spacing();
        ImGui.PushTextWrapPos(0f);
        Text(st.Groups.Length > 0 && HasSetAside(shown)
            ? "Set aside: what your queued projects keep back from your stores until they are built; other projects and road works may not use it."
            : "Nothing is set aside for projects.");
        if (_storesSplitLine is { } split) Text(split);
        Text("Change: since the start of last turn. Click a good to chart its stores below.");
        ImGui.PopTextWrapPos();
    }

    private static bool HasSetAside(StoreGood[] goods)
    {
        foreach (StoreGood g in goods) if (g.SetAside > 0) return true;
        return false;
    }

    /// <summary>A stores row's click: that good is charted (the stores metric is chosen).</summary>
    private void PickStoreGood(int good)
    {
        _storeGood = good;
        for (int i = 0; i < _trendMetrics.Count; i++) if (_trendMetrics[i].IsStock) { _trendIndex = i; break; }
    }

    /// <summary>
    /// The trend graph of the player's OWN empire: the metric buttons (the former TRENDS metrics, plus "stores" — the good
    /// picked in the table), the scope (your empire, or the selected settlement when it is yours), the readings and the
    /// plot. Every series reads only settlements that were the player's on the turn read.
    /// </summary>
    private void DrawOwnTrends()
    {
        HeadingRow("Trends of your empire");
        float rowX = 0f;
        for (int i = 0; i < _trendMetrics.Count; i++)
        {
            if (WrapButton(_trendMetrics[i].ButtonLabel, "trend-" + i.ToString(CultureInfo.InvariantCulture), _trendIndex == i, ref rowX))
                _trendIndex = i;
        }

        ImGui.Spacing();
        bool empire = RadioButton("your empire##trend-empire", _trendEmpireScope);
        Controls.Record("trend-empire");
        if (empire) _trendEmpireScope = true;
        ImGui.SameLine();
        bool own = _selected >= 0 && IsOwnSettlement(_selected);
        bool place = RadioButton((own ? _session.Names.Name(_selected) : "one of your settlements") + "##trend-place", !_trendEmpireScope);
        Controls.Record("trend-place");
        if (place) _trendEmpireScope = false;

        ImGui.Spacing();
        if (_trendMetrics.Count == 0) { Text("no data"); return; }
        if (!_trendEmpireScope && !own)
        {
            Text("Select one of your settlements on the map to chart it.");
            return;
        }
        TrendMetric metric = _trendMetrics[_trendIndex];
        Sim.Core.Observability.IObservationHistory history = _session.Observations;
        OwnershipHistory ownership = _session.Ownership;
        if (metric.IsPrice)
        {
            // Prices: the HistoryBuffer's per-(settlement, good) series, for the player's own settlement only.
            Text("price of the good selected under SETTLEMENT / Economy, at your settlement");
            float[] prices = !_trendEmpireScope ? _session.History.Price(_selected, _selectedGood, _session.Knowledge) : [];
            if (_trendEmpireScope) Text("prices are set settlement by settlement: choose one of your settlements");
            PlotDomain priceDomain = TrendAxisModel.PriceDomain(prices);
            PushDataFont();
            Text(TrendAxisModel.AxisLabel(priceDomain, ToDoubles(prices)));
            Text(TrendAxisModel.BandNote(PlotBand.Neutral));
            PopDataFont();
            PlotLarge("##trend", prices, priceDomain, PlotBand.Neutral);
            return;
        }
        if (metric.IsStock)
        {
            string goodName = _stores?.Good(_storeGood)?.Name ?? "no good";
            Text("stores of " + goodName);
            double[] stock = _storeGood < 0 ? []
                : _trendEmpireScope ? TrendsModel.EmpireStock(history, _storeGood, ownership)
                : TrendsModel.OwnSettlementStock(history, _selected, _storeGood, ownership);
            PlotDomain stockDomain = TrendAxisModel.Domain(Sim.Core.Observability.SeriesKey.Food, stock);
            PushDataFont();
            Text(TrendsModel.LastValueLine(stock));
            Text(TrendAxisModel.AxisLabel(stockDomain, stock));
            PopDataFont();
            ImGui.PushTextWrapPos(0f);
            Text(TrendsModel.StockScopeNote(_trendEmpireScope));
            ImGui.PopTextWrapPos();
            PlotLarge("##trend", TrendsModel.ForPlot(stock), stockDomain, PlotBand.Neutral);
            return;
        }
        double[] series = _trendEmpireScope
            ? TrendsModel.Empire(history, metric.Key, metric.ClassId, ownership)
            : TrendsModel.OwnSettlement(history, _selected, metric.Key, metric.ClassId, ownership);
        PlotDomain domain = TrendAxisModel.Domain(metric.Key, series);
        PlotBand band = TrendAxisModel.Band(metric.Key, series.Length == 0 ? double.NaN : series[^1]);
        PushDataFont();
        Text(TrendsModel.LastValueLine(series));
        Text(TrendAxisModel.AxisLabel(domain, series));
        Text(TrendAxisModel.BandNote(band));
        PopDataFont();
        ImGui.PushTextWrapPos(0f);
        Text(TrendsModel.EmpireScopeNote(metric.Key, _trendEmpireScope));
        ImGui.PopTextWrapPos();
        PlotLarge("##trend", TrendsModel.ForPlot(series), domain, band);
    }
}
