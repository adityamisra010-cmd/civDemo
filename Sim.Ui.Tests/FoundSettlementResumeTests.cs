using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 workstream integration (S1b x S0 x the UI session): a UI session in which the PLAYER issues Found Settlement
/// orders (OrderKind 9, ADR-036) is saved the way the game saves it — manifest, trace, order log — and resumed by
/// <see cref="UiSession.Resume"/>, which re-founds the world and replays the log through the SAME executor and
/// per-step observers, checking every replayed turn's hash against the saved trace. The resumed session must be
/// hash-identical to the live one, carry the same log, trace, annals and colony names, and continue identically —
/// including a Found Settlement order that was issued at the save turn but not yet played (re-queued by the resume
/// and delivered by the next End Turn of both sessions).
///
/// The two played orders are the determinism-xproc Found Settlement leg's literals (gen-sample-orders.py --found;
/// re-derived as legal and founding by Sim.Tests FoundSettlementCiLegTests): turn 2 from settlement 0 to cell 330985,
/// turn 5 from settlement 12 — the colony the first order founds, above the turn-0 maximum (the ADR-034 deferral) —
/// to cell 322801. Each is offered only after THE predicate (<see cref="FoundingQuery.Check(IReadOnlyWorldState, Sim.Core.Systems.SimConfig, SettlementSiting.FrontierSiting, PolityId, SettlementId, int)"/>)
/// answers Legal on the world the player sees, which is what the S1d placement mode will ask.
/// </summary>
public class FoundSettlementResumeTests
{
    private const int FirstOrderTurn = 2, FirstSource = 0, FirstSite = 330985;
    private const int SecondOrderTurn = 5, SecondSource = 12, SecondSite = 322801;
    private const int SavedTurns = 8;

    private static WorldgenConfig Worldgen() => UiSession.ProductionWorldgen();

    /// <summary>Appends ONE FoundSettlement order for the player's Empire, stamped with the session's current turn —
    /// only after the predicate answers Legal for it on the world the player sees.</summary>
    private static void PlayerFounds(UiSession s, int source, int site)
    {
        SettlementSiting.FrontierSiting frontier = FoundingQuery.Frontier(s.World, s.Config, Worldgen())
            ?? throw new InvalidOperationException("the canonical world has terrain");
        Assert.Equal(FoundingVerdict.Legal,
            FoundingQuery.Check(s.World, s.Config, frontier, UiPlayer.Empire, new SettlementId(source), site));
        s.Orders.Append(FoundingQuery.FoundOrder(s.World, UiPlayer.Empire, new SettlementId(source), site));
    }

    private static int SettlementAt(IReadOnlyWorldState w, int cell)
    {
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].SiteCell == cell) return w.Settlements[i].Id.Value;
        return -1;
    }

    private static int FoundOrders(OrderLog log)
    {
        int n = 0;
        for (int i = 0; i < log.Count; i++) if (log[i].Kind == OrderKind.FoundSettlement) n++;
        return n;
    }

    [Fact]
    public void PlayerFoundSettlementSession_SavedAndResumed_IsHashIdentical_AndContinuesIdentically()
    {
        string dir = Path.Combine(Path.GetTempPath(), "m5w-found-resume-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        string log = Path.Combine(dir, "orders-20261011-120000.bin");
        try
        {
            UiSession live = UiSession.Start(42);
            live.ExportManifest("2026-10-11 12:00:00", log);
            int turnZeroCount = live.World.Settlements.Count;
            for (int t = 0; t < SavedTurns; t++)
            {
                if (t == FirstOrderTurn) PlayerFounds(live, FirstSource, FirstSite);
                if (t == SecondOrderTurn) PlayerFounds(live, SecondSource, SecondSite);
                int before = live.World.Settlements.Count;
                live.EndTurn();
                // Turn-exact delivery: the order stamped t founds in the state of t + 1, on the site it named, a
                // settlement the player's Empire controls (the source's polity), and not one turn earlier.
                if (t == FirstOrderTurn || t == SecondOrderTurn)
                {
                    int site = t == FirstOrderTurn ? FirstSite : SecondSite;
                    int colony = SettlementAt(live.World, site);
                    Assert.True(colony >= 0, $"the order stamped {t} founded nothing at cell {site}");
                    Assert.Equal(before + 1, live.World.Settlements.Count);
                    Assert.True(EmpireQuery.ControlsSettlement(live.World, UiPlayer.Empire, new SettlementId(colony)));
                    if (t == FirstOrderTurn) Assert.Equal(SecondSource, colony);   // the second order's source
                }
            }
            Assert.True(live.World.Settlements.Count >= turnZeroCount + 2);

            // A third order issued at the save turn and NOT yet played: the resume re-queues it.
            SettlementSiting.FrontierSiting f = FoundingQuery.Frontier(live.World, live.Config, Worldgen())!;
            int queuedSite = AiFoundingPolicy.BestSiteFor(live.World, live.Config, f, live.World.Terrain!,
                new AiFoundingPolicy.Sites(live.World, f), UiPlayer.Empire, new SettlementId(FirstSource));
            Assert.True(queuedSite >= 0, "no legal site left for the queued order");
            PlayerFounds(live, FirstSource, queuedSite);
            Assert.Equal(3, FoundOrders(live.Orders));

            live.Save(log);
            live.ExportTrace(UiSession.TracePath(log));

            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out string resumedLog);
            Assert.Equal(Path.GetFullPath(log), Path.GetFullPath(resumedLog));
            Assert.Empty(resumed.ResumeNotice);   // same schema: the trace was checked turn by turn
            Assert.Equal((long)SavedTurns, resumed.World.Clock.Turn);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(live.Orders.Count, resumed.Orders.Count);
            for (int i = 0; i < live.Orders.Count; i++) Assert.Equal(live.Orders[i], resumed.Orders[i]);
            Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
            Assert.Equal(live.AnnalLines.ToArray(), resumed.AnnalLines.ToArray());
            Assert.Equal(live.World.Settlements.Count, resumed.World.Settlements.Count);
            for (int i = 0; i < live.World.Settlements.Count; i++)
            {
                int id = live.World.Settlements[i].Id.Value;
                Assert.Equal(live.Names.Name(id), resumed.Names.Name(id));
            }

            // Continued play: the re-queued order founds in BOTH sessions on the next End Turn, identically.
            int beforeQueued = live.World.Settlements.Count;
            for (int t = 0; t < 3; t++)
            {
                live.EndTurn();
                resumed.EndTurn();
                Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
                if (t == 0)
                {
                    Assert.Equal(beforeQueued + 1, live.World.Settlements.Count);
                    Assert.True(SettlementAt(resumed.World, queuedSite) >= 0, "the re-queued order founded nothing");
                }
            }
            Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
