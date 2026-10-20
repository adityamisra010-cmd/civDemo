using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Headless;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

public partial class KnowledgeLeakTests
{
    /// <summary>
    /// D-055 (2026-10-16, track T1b; batch-4 design §1.6 "Visibility (P7)", review L4-18): A FOREIGN HELD HEAD YIELDS NO HOLD
    /// TEXT. In the one-AI rig the hidden polity's first settlement A holds a part-built granary (ordered at turn 0, p 437,500,
    /// no builders) that sets aside its Empire's 22 timber and 11 stone, and its second settlement B a younger granary
    /// (ordered at turn 1, all builders) that waits on it — the hold state construction publishes for that purse. Seen by the
    /// PLAYER through the ALL-KNOWN view (the strongest case: both places known), nothing names it: B's settlement view is closed books (D-060; it said
    /// what is being built before D-060), the granary card at B (requirements, status) and the EMPIRE view say nothing of
    /// a set-aside. CONTROL: the same rig through the hidden polity's OWN books names it — its card's note "… set aside for
    /// A's Granary …" and its EMPIRE view's "Set aside for projects: 22 timber, 11 stone" — so the absence is the filter, not
    /// an empty rig. Mutants: the card reads the set-asides whatever the viewer rules; the EMPIRE summary sums every purse.
    /// </summary>
    [Fact]
    public void AForeignHeldHead_YieldsNoHoldText_WhileTheHoldersOwnBooksNameIt()
    {
        Rig r = Played.Value;
        WorldState w = r.Next.Clone();
        IReadOnlyList<int> foreign = KnowledgeStubs.SettlementsOf(w, Hidden);
        Assert.True(foreign.Count >= 2, "the rig needs two settlements of one foreign Empire");
        var a = new SettlementId(foreign[0]);
        var b = new SettlementId(foreign[1]);
        int timber = r.Config.Goods!.IdOf("timber"), stone = r.Config.Goods!.IdOf("stone");
        const int granary = 1;

        // Only the two heads of this rig in the foreign queues (every other foreign queue emptied); the foreign purse's timber
        // and stone at A alone.
        foreach (int s in foreign) ClearQueue(w, new SettlementId(s));
        foreach (int s in foreign) { SetStock(w, new SettlementId(s), timber, 0); SetStock(w, new SettlementId(s), stone, 0); }
        SetStock(w, a, timber, 22);
        SetStock(w, a, stone, 11);
        AddHead(w, a, 0, granary, 437_500);
        ShareOf(w, a, 0.0);
        AddHead(w, b, 1, granary, 0);
        ShareOf(w, b, 1.0);
        double dt = ActionQueryContext.ForNextStep(w, UiSession.ProductionEra()).NextDtYears!.Value;

        // What construction publishes for this world (THE walk's rows), so the table holds the foreign purse's set-aside.
        SetAsideReading reading = SetAsideQuery.Read(w, r.Config, null, dt);
        w.ConstructionHolds.Clear();
        foreach (ConstructionHoldRow row in reading.Walk.Rows()) w.ConstructionHolds.Add(row);
        Assert.Equal([new ConstructionHoldRow(Hidden.Value, timber, 22), new ConstructionHoldRow(Hidden.Value, stone, 11)],
            Enumerable.Range(0, w.ConstructionHolds.Count).Select(i => w.ConstructionHolds[i]));
        HeadStatusReport waiting = ConstructionQuery.HeadStatus(w, r.Config, b, null, dt)!;
        Assert.Equal((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(a, 0, granary)), (waiting.Cause, waiting.Holder));   // PREMISE

        string Seen(PolityId viewer)
        {
            PlayerKnowledge k = PlayerKnowledge.Of(w, viewer);
            InfoCard card = InfoQuery.Card(w, r.Config, viewer, InfoSubject.OfProject(granary, b.Value), new ActionQueryContext(null, dt), r.Name);
            var parts = new List<string> { card.StatusLine ?? "", card.Summary ?? "" };
            foreach (InfoLink l in card.Requirements) parts.Add(l.Label + "|" + l.Note);
            foreach (ViewBlock blk in PlayerViews.Settlement(w, r.Config, viewer, null, b.Value, r.Name, 3, k).Blocks) parts.AddRange(blk.Lines);
            foreach (ViewBlock blk in PlayerViews.Empire(w, r.Config, viewer, r.Name, 3, k).Blocks) parts.AddRange(blk.Lines);
            return string.Join("\n", parts);
        }

        string player = Seen(Player);
        Assert.DoesNotContain("set aside", player, StringComparison.OrdinalIgnoreCase);
        // D-060: B is a known rival — its view is closed books (before D-060 it said "Being built: granary").
        Assert.Contains(PlayerKnowledge.NotYoursLine, player, StringComparison.Ordinal);
        Assert.DoesNotContain("Being built", player, StringComparison.Ordinal);

        string own = Seen(Hidden);
        Assert.Contains("22 set aside for " + r.Name(a.Value) + "'s Granary, ahead in the building order", own, StringComparison.Ordinal);
        Assert.Contains("Set aside for projects: 22 timber, 11 stone", own, StringComparison.Ordinal);
    }

    private static void ClearQueue(WorldState w, SettlementId s)
    {
        var queue = new List<ConstructionQueueRow>();
        for (int i = 0; i < w.ConstructionQueue.Count; i++) if (w.ConstructionQueue[i].Settlement != s) queue.Add(w.ConstructionQueue[i]);
        var progress = new List<ConstructionProgressRow>();
        for (int i = 0; i < w.ConstructionProgress.Count; i++) if (w.ConstructionProgress[i].Settlement != s) progress.Add(w.ConstructionProgress[i]);
        w.ConstructionQueue.Clear();
        foreach (ConstructionQueueRow q in queue) w.ConstructionQueue.Add(q);
        w.ConstructionProgress.Clear();
        foreach (ConstructionProgressRow p in progress) w.ConstructionProgress.Add(p);
    }

    private static void AddHead(WorldState w, SettlementId s, long turn, int project, long progress)
    {
        int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, project));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, project, turn, progress));
    }

    private static void ShareOf(WorldState w, SettlementId s, double construction)
    {
        var row = new SectorAllocationRow(s, Farming: 1.0 - construction, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: construction);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) { w.SectorAllocations[i] = row; return; }
        w.SectorAllocations.Add(row);
    }
}
