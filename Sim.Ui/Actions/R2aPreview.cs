using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Actions;

/// <summary>
/// R2a — HEADLESS PREVIEWS of the Trade unlock and city-state progression (sim-ui --r2a-preview [dir];
/// docs/architecture/r2-previews/). Every state is a real session on the canonical founded world (seed 42); the
/// action surface is the real ActionSurfaceScreen over ActionSurface.Build (<see cref="ActionSurfacePreview"/>).
/// <list type="number">
/// <item>turn 1 (Age I): no Trade capability listed;</item>
/// <item>researched, pre-Trade (Age III): crafts, a crop, the Taxation civic, a road class and Trade's PREREQUISITES
///   (token_counting, donkey) known — Trade not yet listed, no goods move;</item>
/// <item>post-Trade: the same plus `trade` completed through the order pathway — Trade listed, goods move;</item>
/// <item>a city-state: the last settlement released from the realm and left to develop on its own for many
///   turns; a card shows its own accumulated knowledge, the crafts and capabilities that knowledge makes legal
///   there, and the world screen with it selected.</item>
/// </list>
/// Deterministic: the same build writes byte-identical SVGs (their SHA-256 are logged).
/// </summary>
public static class R2aPreview
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static string[] Closure(ResearchContent r, params string[] ids) => ActionSurfacePreview.WithAncestors(r, ids);

    private static WorldState Know(WorldState w, ResearchContent r, params string[] ids)
    {
        foreach (string id in Closure(r, ids))
        {
            ResearchNodeId key = r.Nodes[r.IndexOfId(id)].Key;
            if (!ResearchQuery.IsCompleted(w, Me, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, key));
        }
        return w;
    }

    private static int Capital(IReadOnlyWorldState w) => EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c.Value : -1;

    /// <summary>The researched pre-Trade knowledge: crafts, a crop, the tax edict's research gate (read from sim.json
    /// <c>governance.taxationRequires</c> — the Taxation civic since R5; <see cref="ActionSurfacePreview.TaxationGate"/>),
    /// a road class and Trade's prerequisites. Until 2026-10-05 this granted <c>arithmetic_babylonian</c> as "taxation",
    /// which stopped opening the gate at R5, so the description's "taxation known" was false.</summary>
    private static string[] Researched(SimConfig cfg) =>
        [.. ActionSurfacePreview.TaxationGate(cfg), "cereal_cultivation", "pottery_open_fired", "tin_bronze", "track_road", "token_counting", "donkey"];

    public static ActionSurfacePreview.State PreTrade()
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        WorldState w = Know(UiFounding.Found(42), cfg.Research!, Researched(cfg));
        w = EraPreview.WorldAt(w, cfg.Ages!, Me, ActionSurfacePreview.RigAge);
        UiSession s = UiSession.StartFrom(w, 42);
        for (int t = 0; t < 3; t++) s.EndTurn();
        return new("pre-trade-a3", "Researched, pre-Trade: crafts, a crop, the Taxation civic, a road class and Trade's prerequisites known; Age III",
            s, s.World, Capital(s.World));
    }

    public static ActionSurfacePreview.State PostTrade()
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        WorldState w = Know(UiFounding.Found(42), cfg.Research!, Researched(cfg));
        w = EraPreview.WorldAt(w, cfg.Ages!, Me, ActionSurfacePreview.RigAge);
        UiSession s = UiSession.StartFrom(w, 42);
        ResearchNodeId trade = cfg.Research!.Nodes[cfg.Research.IndexOfId("trade")].Key;
        if (!s.EmitResearchOrder(trade)) throw new InvalidOperationException("r2a preview: Trade not orderable");
        for (int t = 0; t < 120 && !ResearchQuery.IsCompleted(s.World, Me, trade); t++) s.EndTurn();
        if (!ResearchQuery.IsCompleted(s.World, Me, trade)) throw new InvalidOperationException("r2a preview: Trade never completed");
        for (int t = 0; t < 3; t++) s.EndTurn();
        return new("post-trade-a3", "Post-Trade: the same world with Trade researched through the order pathway; Age III",
            s, s.World, Capital(s.World));
    }

    public static (ActionSurfacePreview.State State, SettlementId City) CityState(int turns)
    {
        WorldState w = UiFounding.Found(42);
        SettlementId city = w.Settlements[w.Settlements.Count - 1].Id;
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != city) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow c in kept) w.Controls.Add(c);
        UiSession s = UiSession.StartFrom(w, 42);
        for (int t = 0; t < turns; t++) s.EndTurn();
        return (new("city-state", $"A city-state: settlement {city.Value} released from the realm at founding and left to develop for {turns} turns",
            s, s.World, city.Value), city);
    }

    /// <summary>The city-state card: its own knowledge and what it makes legal there (all from State readers).</summary>
    public static (string Svg, List<string> Lines) CityCard(ActionSurfacePreview.State st, SettlementId city, string? fontDir)
    {
        SimConfig cfg = st.Session.Config;
        ResearchContent r = cfg.Research!;
        IReadOnlyWorldState w = st.World;
        PolityId local = SettlementKnowledge.LocalHolder(city);
        var lines = new List<string>
        {
            $"City-state: {st.Session.Names.Name(city.Value)} (settlement {city.Value.ToString(CultureInfo.InvariantCulture)}) — no Empire controls it",
            $"Turn {Sim.Ui.ViewModel.PlayerTurn.Current(w.Clock.Turn)} · population {ResearchQuery.Population(w, local)}",
            $"Research: {ResearchQuery.CityStateResearchPoints(w, r, city).ToString("0.00", CultureInfo.InvariantCulture)} RP/turn (city-state pace {r.Tuning.CityStatePaceFraction.ToString("0.00", CultureInfo.InvariantCulture)} × its own population's curve)",
            "Own accumulated knowledge:",
        };
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (w.ResearchCompleted[i].Polity.Value == local.Value && r.IndexOf(w.ResearchCompleted[i].Node) is int n and >= 0)
                lines.Add("  • " + r.Nodes[n].Name + " (" + r.Nodes[n].Age + ")");
        if (ResearchQuery.TryGetTarget(w, local, out ResearchNodeId target) && r.IndexOf(target) is int ti and >= 0)
            lines.Add($"  researching: {r.Nodes[ti].Name} ({ResearchQuery.Progress(w, local, target).ToString("0", CultureInfo.InvariantCulture)} / {r.Nodes[ti].BaseCost.ToString("0", CultureInfo.InvariantCulture)} RP)");
        lines.Add("Crafts legal here (CraftingQuery):");
        foreach (RecipeEntry recipe in cfg.Goods!.Recipes)
            if (CraftingQuery.IsKnown(r, recipe, SettlementKnowledge.MaskOf(w, r, city))) lines.Add("  • " + recipe.Name);
        lines.Add("Trade (TradeQuery): " + (TradeQuery.SettlementKnowsTrade(w, cfg, city) ? "known" : "not yet known"));
        bool[] mask = SettlementKnowledge.MaskOf(w, r, city)!;
        lines.Add("Food sector: " + LabourActivities.CapabilityLabel(r, mask, Sectors.Farming));

        EraTheme t = EraThemes.For(UiEra.Prehistoric);
        double h = 60 + 22 * lines.Count;
        var d = new DrawList();
        PanelFrame.Field(d, new RectD(0, 0, 720, h + 40), t, 2);
        PanelFrame.Paint(d, new RectD(20, 20, 680, h), t, 3, FrameKind.Panel);
        double y = 40;
        for (int i = 0; i < lines.Count; i++, y += 22)
            d.Write(t, 40, y, lines[i], i == 0 ? 17 : 14, t.Ink.Text, TextAlign.Left, i == 0 ? FontRole.Heading : FontRole.Body);
        return (SvgWriter.Write(d, 720, h + 40, fontDir), lines);
    }

    public static IReadOnlyList<string> Run(string outDir, string? fontDir, int cityTurns = 600)
    {
        fontDir = Sim.Ui.Render.SvgWriter.FontDirectoryFor(fontDir, outDir); // F3: portable @font-face URLs
        Directory.CreateDirectory(outDir);
        var written = new List<string>();
        var log = new List<string> { "R2a preview: the real ActionSurfaceScreen over ActionSurface.Build, seed 42, canonical 1024 px world" };
        void Emit(string name, string svg)
        {
            string path = Path.Combine(outDir, name);
            File.WriteAllText(path, svg);
            written.Add(path);
            log.Add("  " + name + "  sha256 " + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(svg))));
        }
        (ActionSurfacePreview.State city, SettlementId cityId) = CityState(cityTurns);
        foreach (ActionSurfacePreview.State s in new[] { ActionSurfacePreview.TurnOne(), PreTrade(), PostTrade(), city })
        {
            ActionSurfaceModel model = ActionSurfacePreview.ModelOf(s);
            long flows = 0;
            for (int i = 0; i < s.World.TradeFlows.Count; i++) flows += s.World.TradeFlows[i].Quantity;
            log.Add(s.Stem + ": " + s.Description + "; turn " + s.World.Clock.Turn.ToString(CultureInfo.InvariantCulture)
                + ", world hash " + WorldHash.ComputeHex(s.World) + ", domains " + string.Join("/", model.Domains)
                + ", standing [" + string.Join(", ", model.Standing?.Items ?? []) + "], trade units moved this turn "
                + flows.ToString(CultureInfo.InvariantCulture));
            string terrain = AgePreview.TerrainDataUri(s.World);
            Emit(s.Stem + "-screen.svg", ActionSurfacePreview.ScreenSvg(s, fontDir, terrain));
            if (s.Stem != "city-state") Emit(s.Stem + "-panel.svg", ActionSurfacePreview.PanelSvg(s, fontDir));
        }
        (string card, List<string> lines) = CityCard(city, cityId, fontDir);
        Emit("city-state-card.svg", card);
        foreach (string l in lines) log.Add("    " + l);
        File.WriteAllLines(Path.Combine(outDir, "preview-log.txt"), log);
        return written;
    }
}
