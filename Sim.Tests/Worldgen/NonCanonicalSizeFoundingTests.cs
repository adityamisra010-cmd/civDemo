using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Worldgen;

/// <summary>
/// M5 playtest blocker (D-052): A WORLD FOUNDED AT A NON-CANONICAL SIZE FOUNDS COLONIES ON ITS OWN MAP. Every founding
/// pipeline (UiSession.ProductionPipeline, the CLI's founded pipeline, the FoundingRigs default) builds ColonizationSystem
/// with worldgen.json's 1024-px config, also for a world founded at another size (`--size`, the 256-px dev preset).
/// <see cref="WorldFounding.HinterlandMeans"/> indexed the terrain by that CONFIG's sizePx, so a colony on a 256-px map
/// was written deposits read from other cells, and a site in the map's bottom rows (row ≥ 208 of 256: the 1024-px
/// disc's row reaches cell 64 × 1024 = 65 536, past the raster) threw IndexOutOfRangeException during End Turn. The
/// disc is now indexed by the TERRAIN's own size, so the worldgen config's sizePx no longer reaches the founding step:
/// <list type="bullet">
/// <item>the hinterland field on a dev world is bit-identical under the canonical and the dev config, everywhere —
///   corners, the bottom rows, the measured cells — and never throws;</item>
/// <item>METAMORPHIC: an order-driven Found Settlement stepped through the production pipeline built with the canonical
///   config founds a colony whose DepositRows are <see cref="WorldFounding.AddDepositsForSite"/> evaluated with the dev
///   config, bit for bit, and the world after the founding step hashes equal to the same step through a pipeline built
///   with the dev config — for the measured S1b site and for a legal site in the bottom rows;</item>
/// <item>THE PLAYTEST CONFIGURATION: `sim run --founded --seed 1 --size 256 --ai-empires 1`, in process through the CLI's
///   own recipes, runs through the AI Empire's bottom-row founding (it threw on the step to turn 180) and every colony
///   carries the map's own deposits.</item>
/// </list>
/// The canonical 1024-px game (config size = terrain size) is unchanged: the canonical founded golden is the proof.
/// </summary>
public class NonCanonicalSizeFoundingTests
{
    private static readonly PolityId Player = new(1);
    private static SimConfig Cfg => FoundingRigs.Cfg();

    /// <summary>The first row of a 256-px map whose cells the 1024-px config's disc carried past the raster: a site
    /// cell c sits at the 1024-config row c / 1024, and its disc (±12 rows) reaches row 64 = 65 536 / 1024 from row 52,
    /// i.e. from cell 52 × 1024 = 53 248 = row 208 × 256.</summary>
    private const int BottomRow = 208;

    /// <summary>A dev world after one orderless production step (the S1b rig's turn 1; stepped with the dev config, so
    /// the world the order is judged on cannot depend on the defect).</summary>
    private static WorldState W1(ulong seed) =>
        FoundingRigs.Pipeline(Cfg, null, TestConfigs.DevWorldgen()).Step(FoundingRigs.Dev(seed));

    private static OrderLog Log(params OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return log;
    }

    private static long Bits(double v) => BitConverter.DoubleToInt64Bits(v);

    // ================================================================== THE FIELD

    [Fact]
    public void TheHinterlandField_OnADevWorld_IsBitIdenticalUnderTheCanonicalAndTheDevConfig_AndNeverThrows()
    {
        WorldState w = FoundingRigs.Dev(42UL);
        TerrainSet t = w.Terrain!;
        WorldgenConfig canonical = TestConfigs.Worldgen(), dev = TestConfigs.DevWorldgen();
        Assert.Equal(256, t.Size);
        Assert.Equal(1024, canonical.SizePx);   // the mismatch the founding pipelines carry
        double r = WorldFounding.HinterlandRadiusPx(dev, Cfg);
        Assert.Equal(r, WorldFounding.HinterlandRadiusPx(canonical, Cfg));   // --size never overrides kmPerPx

        int last = t.Size - 1;
        var cells = new List<int>
        {
            250 * 256 + 100,                 // measured: threw IndexOutOfRangeException under the 1024 config
            28776,                           // measured: the S1b rig's colony site (wrong disc)
            0, last, last * 256, last * 256 + last,   // the four corners
            BottomRow * 256, BottomRow * 256 + 128, 230 * 256 + 17, 128 * 256 + 128, 64 * 256 + 200, 17 * 256 + 3,
        };
        for (int s = 0; s < w.Settlements.Count; s++) cells.Add(w.Settlements[s].SiteCell);
        for (int cell = 0; cell < t.Size * t.Size; cell += 977) cells.Add(cell);   // a stable stride across every row

        int bottom = 0;
        foreach (int cell in cells)
        {
            (double m, double e) want = WorldFounding.HinterlandMeans(t, dev, cell, r);
            (double m, double e) got = WorldFounding.HinterlandMeans(t, canonical, cell, r);
            Assert.True(Bits(want.m) == Bits(got.m) && Bits(want.e) == Bits(got.e),
                $"cell {cell}: canonical-config field ({got.m:R}, {got.e:R}) != the map's own ({want.m:R}, {want.e:R})");
            if (cell / t.Size >= BottomRow) bottom++;
        }
        Assert.True(bottom >= 8, "the sample covers the bottom rows");
    }

    // ================================================================== THE FOUNDING STEP (metamorphic)

    /// <summary>The first legal site in rows ≥ <see cref="BottomRow"/> for a Found Settlement order from one of the
    /// player's settlements (settlements in table order, cells ascending — a stable integer scan), or (-1, -1).</summary>
    private static (SettlementId Source, int Cell) BottomRowLegalSite(IReadOnlyWorldState w)
    {
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w, Cfg, TestConfigs.DevWorldgen());
        var occupied = new int[w.Settlements.Count];
        for (int i = 0; i < occupied.Length; i++) occupied[i] = w.Settlements[i].SiteCell;
        double[] spacing = SettlementSiting.SeedSpacing(f, occupied);
        int size = f.Size;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId src = w.Settlements[s].Id;
            if (!EmpireQuery.ControlsSettlement(w, Player, src)) continue;
            for (int cell = BottomRow * size; cell < size * size; cell++)
                if (FoundingQuery.Check(w, Cfg, f, spacing, occupied, Player, src, cell) == FoundingVerdict.Legal) return (src, cell);
        }
        return (new SettlementId(-1), -1);
    }

    /// <summary>The two sites: the S1b rig's (dev seed 42, the nearest legal site from settlement 0 — the measured cell
    /// 28776, whose colony was written the 1024-config disc's livestock 0.8743989520947507 instead of the map's
    /// 0.869712442758095); and a legal site in the bottom rows. Dev seed 42 has none there (its bottom rows are water
    /// and below-floor land), so that case runs on dev seed 1, whose first one is cell 53305 (row 208) from settlement 0
    /// — under the 1024 config the step threw IndexOutOfRangeException.</summary>
    public static TheoryData<string, ulong> Sites => new() { { "nearest", 42UL }, { "bottom-row", 1UL } };

    [Theory]
    [MemberData(nameof(Sites))]
    public void AFoundingThroughTheCanonicalConfigPipeline_OnADevWorld_WritesTheMapsOwnDeposits_AndHashesAsTheDevConfigPipeline(string which, ulong seed)
    {
        WorldState w = W1(seed);
        SettlementId src;
        int site;
        if (which == "nearest")
        {
            src = w.Settlements[0].Id;
            site = FoundingRigs.NearestLegalSite(w, Cfg, FoundingRigs.Frontier(w, Cfg, TestConfigs.DevWorldgen()), Player, src);
        }
        else (src, site) = BottomRowLegalSite(w);
        Assert.True(site >= 0, "no legal " + which + " site - the rig is void");
        if (which == "bottom-row") Assert.True(site / w.Terrain!.Size >= BottomRow);
        int colony = FoundingRigs.MaxSettlementId(w) + 1;
        OrderRecord order = FoundingQuery.FoundOrder(w, Player, src, site);

        WorldState viaCanonical = FoundingRigs.Pipeline(Cfg, Log(order), TestConfigs.Worldgen()).Step(w.Clone());
        WorldState viaDev = FoundingRigs.Pipeline(Cfg, Log(order), TestConfigs.DevWorldgen()).Step(w.Clone());
        Assert.True(FoundingRigs.TryGetSettlement(viaCanonical, colony, out SettlementRow row), "the colony was not founded");
        Assert.Equal(site, row.SiteCell);

        // The founding's own call, with the map's own (dev) config, into a scratch table.
        var want = new Table<DepositRow>();
        WorldFounding.AddDepositsForSite(want, w.Terrain!, TestConfigs.DevWorldgen(), Cfg, Cfg.Goods!,
            new SettlementId(colony), site, w.Seed, colony);
        Assert.True(want.Count > 0);
        var got = new List<DepositRow>();
        for (int i = 0; i < viaCanonical.Deposits.Count; i++)
            if (viaCanonical.Deposits[i].Settlement.Value == colony) got.Add(viaCanonical.Deposits[i]);
        Assert.Equal(want.Count, got.Count);
        for (int i = 0; i < want.Count; i++)
        {
            Assert.Equal(want[i].Good, got[i].Good);
            Assert.True(Bits(want[i].Abundance) == Bits(got[i].Abundance),
                $"site {site}, good {want[i].Good.Value}: founded {got[i].Abundance:R}, the map's own disc gives {want[i].Abundance:R}");
        }

        // The worldgen config's size no longer reaches the step at all.
        Assert.Equal(WorldHash.ComputeHex(viaDev), WorldHash.ComputeHex(viaCanonical));
    }

    // ================================================================== THE PLAYTEST CONFIGURATION (the CLI's recipes)

    /// <summary>
    /// `sim run --founded --seed 1 --size 256 --ai-empires 1`, in process, through the CLI's own recipes: the world from
    /// <see cref="Sim.Cli.HeadlessFounding"/>, the founded pipeline from <see cref="Sim.Cli.CliRecipes.ProductionPipeline"/>
    /// (ColonizationSystem built with worldgen.json's 1024-px config), the AI Empire's orders judged with
    /// <see cref="Sim.Cli.CliRecipes.Worldgen"/> — the CLI's run loop. MEASURED on the unfixed tree: that command stopped
    /// with "error: Index was outside the bounds of the array." on the step to turn 180 (`--turns 179` completed), when the
    /// AI Empire's fifth Found Settlement order (stamped 179) named a site in the bottom rows. It now runs through, the AI
    /// founds there, and every settlement founded after turn zero carries the map's own deposits, bit for bit.
    /// </summary>
    [Fact]
    public void ASizedWorldWithAnAiEmpire_ThroughTheCliRecipes_RunsThroughTheAisBottomRowFounding_WithTheMapsOwnDeposits()
    {
        const ulong seed = 1;
        const int turns = 180;
        // Batch 3 (D-054, W1 fix F5): the defect this pins is in ROWS (a sized world's bottom rows indexed through the
        // 1024-px config), so the rig needs an AI that founds there — and on the cylindrical world none does: MEASURED,
        // seeds 1-20, 180 turns at 256 px, 5 colonies each and none in rows >= 208 (the y-only taper and the periodic
        // noise move the land; the AI expands along it). So the world is HeadlessFounding's own founding (worldgen.json
        // at --size 256 with one AI Empire, the production SimConfig) with the cylinder switched off — the flat world the
        // defect was measured on; every recipe after founding is the CLI's, unchanged.
        // Integration R1 (2026-10-14) — RE-MEASURED on the merged tree (seed 1, 180 turns): FLAT — 5 colonies, ONE in the
        // bottom rows (row 208; the others at rows 63, 107, 80, 88); the CYLINDER — 5 colonies, none there (rows 169, 107, 88,
        // 62, 84). The defect is a ROW-index one (the 1024-px config's disc carried past a 256-px raster's bottom rows), so the
        // property is topology-independent; the order-driven bottom-row case above (dev seed 1, its site asserted in rows
        // >= 208) runs on the shipped cylinder.
        WorldState world = WorldFounding.Found(
            TestConfigs.PreD054Worldgen(Sim.Cli.CliRecipes.Worldgen()) with { SizePx = 256, AiEmpires = 1 }, Cfg, seed);
        Assert.False(world.Terrain!.WrapX);
        SimConfig cfg = Cfg;
        var orders = new OrderLog();
        var exec = new TurnExecutor(Sim.Cli.CliRecipes.Era(), Sim.Cli.CliRecipes.ProductionPipeline(), orders);
        WorldgenConfig aiWorldgen = Sim.Cli.CliRecipes.Worldgen();
        Assert.Equal(1024, aiWorldgen.SizePx);
        for (int t = 1; t <= turns; t++)
        {
            AiOrders.Append(orders, world, cfg, aiWorldgen);
            world = exec.Step(world);
        }
        Assert.Equal(turns, world.Clock.Turn);

        TerrainSet terrain = world.Terrain!;
        WorldgenConfig own = Sim.Cli.CliRecipes.Worldgen() with { SizePx = terrain.Size };
        int colonies = 0, bottom = 0;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementRow row = world.Settlements[s];
            if (row.FoundedTurn == 0) continue;
            colonies++;
            if (row.SiteCell / terrain.Size >= BottomRow) bottom++;
            var want = new Table<DepositRow>();
            WorldFounding.AddDepositsForSite(want, terrain, own, cfg, cfg.Goods!, row.Id, row.SiteCell, world.Seed, row.Id.Value);
            int k = 0;
            for (int i = 0; i < world.Deposits.Count; i++)
            {
                DepositRow d = world.Deposits[i];
                if (d.Settlement != row.Id) continue;
                Assert.True(k < want.Count, $"settlement {row.Id.Value} has more deposit rows than the founding writes");
                Assert.Equal(want[k].Good, d.Good);
                Assert.True(Bits(want[k].Abundance) == Bits(d.Abundance),
                    $"settlement {row.Id.Value} at {row.SiteCell}, good {d.Good.Value}: {d.Abundance:R} != the map's own {want[k].Abundance:R}");
                k++;
            }
            Assert.Equal(want.Count, k);
        }
        Assert.True(colonies >= 5, "the AI Empire founded " + colonies + " settlements by turn 180");
        Assert.True(bottom >= 1, "no settlement was founded in the bottom rows - the rig no longer reaches the former throw");
    }
}
