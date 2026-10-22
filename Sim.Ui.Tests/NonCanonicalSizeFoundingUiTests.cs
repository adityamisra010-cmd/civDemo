using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 playtest blocker (D-052): A `--size` SESSION FOUNDS THE PLAYER'S COLONY ON ITS OWN MAP — the product path, end to
/// end. <see cref="UiSession.Start"/> with sizeOverridePx 256 founds a 256-px world, while its production pipeline builds
/// ColonizationSystem with worldgen.json's 1024-px config (UiSession.ProductionPipeline). The hinterland disc was indexed by
/// that config's size, so a colony the player placed in the map's bottom rows (row ≥ 208 of 256) threw
/// IndexOutOfRangeException at End Turn, and one placed elsewhere was written deposits read from other cells. Now the
/// disc is indexed by the terrain's own size: the S1d placement mode (<see cref="PlacementMode"/> → the ONE guarded
/// emitter, <see cref="UiSession.EmitFoundSettlement"/>) queues the order, End Turn founds the colony without a throw, its
/// DepositRows are <see cref="WorldFounding.AddDepositsForSite"/> with the map's own size bit for bit, and the place card
/// the player read before confirming previewed exactly those rolls (its size-mismatch refusal is gone with the defect).
/// Two sessions: seed 42 (the first legal place in a row-major scan — seed 42's 256-px map has NO legal place in the
/// bottom rows on turn 0: they are water and below-floor land), and seed 1, whose first legal bottom-row place (cell
/// 53305, row 208, from settlement 0) is the former IndexOutOfRangeException path, end to end.
/// </summary>
public class NonCanonicalSizeFoundingUiTests
{
    private const int Size = 256;

    /// <summary>The first row whose sites the 1024-px config's disc carried past a 256-px raster (52 × 1024 = 208 × 256).</summary>
    private const int BottomRow = 208;

    private static long Bits(double v) => BitConverter.DoubleToInt64Bits(v);

    [Theory]
    [InlineData(42UL, 0)]
    [InlineData(1UL, BottomRow)]
    public void OnA256PxSession_ThePlayersPlacementFoundsAColony_WithoutAThrow_WritingTheMapsOwnDeposits(ulong seed, int fromRow)
    {
        UiSession s = UiSession.StartWith(TestFog.PreDiscovery, seed, sizeOverridePx: Size);
        TerrainSet t = s.World.Terrain!;
        Assert.Equal(Size, t.Size);
        Assert.Equal(1024, UiSession.ProductionWorldgen().SizePx);   // the pipeline's config describes another map size

        // The first LEGAL place at or below row fromRow for one of the player's settlements, judged by the placement mode
        // itself (THE predicate with this turn's batch): settlements in table order, cells ascending.
        var mode = new PlacementMode();
        int source = -1, site = -1;
        for (int i = 0; i < s.World.Settlements.Count && site < 0; i++)
        {
            SettlementId id = s.World.Settlements[i].Id;
            if (!EmpireQuery.ControlsSettlement(s.World, UiPlayer.Empire, id)) continue;
            mode.Begin(id.Value, s.Names.Name(id.Value), "");
            for (int cell = fromRow * Size; cell < Size * Size; cell++)
                if (mode.Judge(s, cell) == FoundingVerdict.Legal) { source = id.Value; site = cell; break; }
        }
        Assert.True(site >= 0, "no legal place from row " + fromRow + " for any of the player's settlements - the rig is void");
        Assert.Equal(source, mode.Source);

        // The place card the player reads before confirming (the session's own prepared site half).
        SiteAssessment.Report card = SiteAssessment.Assess(s.World, s.Config, s.Sites!, s.Knowledge.Core, UiPlayer.Empire, site, source);
        int colony = card.NextSettlementId;
        Assert.True(colony >= 0);

        Assert.True(mode.Select(s, site));
        Assert.Equal(FoundingVerdict.Legal, mode.SelectedVerdict);
        Assert.True(mode.Confirm(s), mode.Refusal);
        Assert.Equal(FoundingQuery.FoundOrder(s.World, UiPlayer.Empire, new SettlementId(source), site), Assert.Single(s.QueuedOrders()));

        s.EndTurn();   // threw IndexOutOfRangeException in WorldFounding.HinterlandMeans before the fix

        SettlementRow row = default;
        bool founded = false;
        for (int i = 0; i < s.World.Settlements.Count; i++)
            if (s.World.Settlements[i].Id.Value == colony) { row = s.World.Settlements[i]; founded = true; }
        Assert.True(founded, "the player's colony was not founded");
        Assert.Equal(site, row.SiteCell);

        // Its deposits: the founding's own function with THIS map's size, bit for bit.
        var want = new Table<DepositRow>();
        WorldFounding.AddDepositsForSite(want, t, UiSession.ProductionWorldgen() with { SizePx = Size }, s.Config, s.Config.Goods!,
            new SettlementId(colony), site, s.World.Seed, colony);
        var got = new List<DepositRow>();
        for (int i = 0; i < s.World.Deposits.Count; i++) if (s.World.Deposits[i].Settlement.Value == colony) got.Add(s.World.Deposits[i]);
        Assert.True(want.Count > 0);
        Assert.Equal(want.Count, got.Count);
        for (int i = 0; i < want.Count; i++)
        {
            Assert.Equal(want[i].Good, got[i].Good);
            Assert.True(Bits(want[i].Abundance) == Bits(got[i].Abundance),
                $"site {site}, good {want[i].Good.Value}: founded {got[i].Abundance:R}, the map's own disc gives {want[i].Abundance:R}");
        }

        // And the card previewed exactly those rolls (it evaluates the deposits on this map now).
        Assert.Null(card.DepositsUnavailable);
        Assert.Equal(got.Count, card.Deposits.Length);
        for (int i = 0; i < got.Count; i++)
        {
            Assert.Equal(got[i].Good, card.Deposits[i].Good);
            Assert.True(card.Deposits[i].IfFoundedNext is { } exact && Bits(exact) == Bits(got[i].Abundance),
                $"good {got[i].Good.Value}: the card previewed {card.Deposits[i].IfFoundedNext:R}, the colony was written {got[i].Abundance:R}");
        }
    }
}
