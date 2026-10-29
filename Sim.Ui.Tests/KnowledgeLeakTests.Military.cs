using System.Collections.Immutable;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Headless;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-055 × P7 — THE MILITARY ORDER SURFACE TELLS NOTHING OF A HIDDEN POLITY. The player's military orders are built by
/// MilitaryOrderFactory as the player of a PlayerKnowledge, and listed by AvailableActionsQuery's military contributor:
/// <list type="bullet">
/// <item>a formation of the hidden polity is refused in the SAME words as a formation id that does not exist (re-equip,
///   cancel, disband), and a hidden settlement in the same words as a settlement that does not exist (recruit) — the
///   answer cannot tell the player that it exists;</item>
/// <item>METAMORPHIC: removing every formation of the hidden polity, or adding one, leaves the player's military
///   descriptors and every factory answer identical.</item>
/// </list>
/// The rig is the shipped config (the military layer ON, the fog ON), 256 px, one AI Empire, three turns played. M6 closure
/// (track FOG; D-066 §3): every test runs on two lenses — <see cref="Lens.Fog"/>, THE GATE: the player's production view
/// (<c>PlayerKnowledge.Of(world, player, cfg)</c>), the AI hidden by the REAL fog (MEASURED 2026-10-28: on this rig the
/// player knows none of the AI's 2 settlements, its formation or the polity through turn 6); and <see cref="Lens.Stub"/>,
/// the control leg: the knowledge stub (all known but the AI polity).
/// </summary>
public partial class KnowledgeLeakTests
{
    private static readonly Lazy<(WorldState World, SimConfig Config, bool[] Mask)> MilitaryRig = new(() =>
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        for (int t = 0; t < 3; t++) s.EndTurn();
        WorldState w = s.World.Clone();
        return (w, s.Config, KnowledgeStubs.HiddenMask(w, Player, Hidden));
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private static MilitaryUnitRow Unit(IReadOnlyWorldState w, int id)
    {
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Id == id) return w.MilitaryUnits[i];
        throw new InvalidOperationException("no formation " + id);
    }

    private static int[] FormationsOf(IReadOnlyWorldState w, PolityId p)
    {
        var ids = new List<int>();
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Owner == p) ids.Add(w.MilitaryUnits[i].Id);
        return [.. ids];
    }

    /// <summary>Every answer the military order surface gives the player on <paramref name="w"/>, as text: the military
    /// descriptors the query lists, and the factory's refusal of each formation id and settlement id in <paramref name="ids"/>.</summary>
    /// <summary>What the player knows of <paramref name="w"/> on the military rig in a leg.</summary>
    private static PlayerKnowledge MilitaryView(Lens lens, IReadOnlyWorldState w, SimConfig cfg, bool[] mask) =>
        lens == Lens.Fog ? PlayerKnowledge.Of(w, Player, cfg) : KnowledgeStubs.Over(w, Player, mask);

    private static string MilitarySurface(Lens lens, IReadOnlyWorldState w, SimConfig cfg, bool[] mask, int[] formationIds, int[] settlementIds)
    {
        PlayerKnowledge k = MilitaryView(lens, w, cfg, mask);
        var lines = new List<string>();
        ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, cfg, Player);
        foreach (ActionDescriptor a in actions)
        {
            if (a.Domain != ActionDomain.Military) continue;
            lines.Add(a.Key + " | " + a.Label + " | " + a.Blocker + " | " + a.Detail + " | "
                + string.Join(";", a.Targets.Select(t => t.Id + ":" + t.Label + ":" + t.Blocker)));
        }
        int warbandKey = cfg.UnitFamilies!.FoundingIdentity.Key;
        foreach (int id in formationIds)
        {
            MilitaryOrderFactory.Reequip(w, cfg, k, id, warbandKey, out string? r1);
            MilitaryOrderFactory.CancelReequip(w, cfg, k, id, out string? r2);
            MilitaryOrderFactory.Disband(w, cfg, k, id, out string? r3);
            lines.Add("formation " + id + ": " + r1 + " | " + r2 + " | " + r3);
        }
        foreach (int id in settlementIds)
        {
            MilitaryOrderFactory.Recruit(w, cfg, k, new SettlementId(id), warbandKey, out string? r);
            lines.Add("settlement " + id + ": " + r);
        }
        return string.Join("\n", lines);
    }

    [Theory]
    [InlineData(Lens.Fog)]
    [InlineData(Lens.Stub)]
    public void TheMilitaryOrderSurface_RefusesAHiddenFormationOrSettlement_ExactlyAsANonexistentOne(Lens lens)
    {
        (WorldState w, SimConfig cfg, bool[] mask) = MilitaryRig.Value;
        Assert.True(RecruitmentQuery.Enabled(cfg), "the rig must run the military layer");
        Assert.True(DiscoveryQuery.FogOn(cfg), "the rig must run the shipped fog");
        PlayerKnowledge k = MilitaryView(lens, w, cfg, mask);
        Assert.False(k.AllKnown);
        int[] hiddenFormations = FormationsOf(w, Hidden);
        IReadOnlyList<int> hiddenSettlements = KnowledgeStubs.SettlementsOf(w, Hidden);
        Assert.NotEmpty(hiddenFormations);                                        // anti-vacuity: the AI fields a warband
        Assert.NotEmpty(hiddenSettlements);
        foreach (int s in hiddenSettlements) Assert.False(k.KnowsSettlement(s));
        foreach (int h in hiddenFormations) Assert.False(k.Core.KnowsUnit(Unit(w, h)));
        int ghostFormation = 1_000_000, ghostSettlement = 1_000_000;
        int warbandKey = cfg.UnitFamilies!.FoundingIdentity.Key;

        MilitaryOrderFactory.Reequip(w, cfg, k, ghostFormation, warbandKey, out string? gr);
        MilitaryOrderFactory.CancelReequip(w, cfg, k, ghostFormation, out string? gc);
        MilitaryOrderFactory.Disband(w, cfg, k, ghostFormation, out string? gd);
        foreach (int h in hiddenFormations)
        {
            Assert.Null(MilitaryOrderFactory.Reequip(w, cfg, k, h, warbandKey, out string? r));
            Assert.Null(MilitaryOrderFactory.CancelReequip(w, cfg, k, h, out string? c));
            Assert.Null(MilitaryOrderFactory.Disband(w, cfg, k, h, out string? d));
            Assert.Equal((gr, gc, gd), (r, c, d));
        }
        MilitaryOrderFactory.Recruit(w, cfg, k, new SettlementId(ghostSettlement), warbandKey, out string? gs);
        foreach (int s in hiddenSettlements)
        {
            Assert.Null(MilitaryOrderFactory.Recruit(w, cfg, k, new SettlementId(s), warbandKey, out string? r));
            Assert.Equal(gs, r);
        }
        Assert.Equal(MilitaryOrderFactory.NotYourSettlement, gs);

        // The player's own warband is still orderable (the surface is not simply empty).
        int own = FormationsOf(w, Player)[0];
        Assert.NotNull(MilitaryOrderFactory.Disband(w, cfg, k, own, out _));
    }

    [Theory]
    [InlineData(Lens.Fog)]
    [InlineData(Lens.Stub)]
    public void PerturbingTheHiddenPolitysFormations_LeavesTheMilitaryOrderSurfaceIdentical(Lens lens)
    {
        (WorldState w, SimConfig cfg, bool[] mask) = MilitaryRig.Value;
        int[] probeFormations = [.. FormationsOf(w, Player), .. FormationsOf(w, Hidden), 999, 1_000_000];
        int[] probeSettlements = [.. Enumerable.Range(0, w.Settlements.Count).Select(i => w.Settlements[i].Id.Value), 1_000_000];
        string baseline = MilitarySurface(lens, w, cfg, mask, probeFormations, probeSettlements);
        Assert.Contains("military.disband.", baseline);                         // anti-vacuity: the player's warband is listed

        // (a) every hidden formation removed; (b) the hidden formations re-identified and one more added at a hidden
        // settlement. A test clone that is never stepped: MilitaryUnits holds no conserved stock (personnel are
        // FormationPersonnel's), and only the read surface is asked.
        int[] hidden = FormationsOf(w, Hidden);
        int hiddenSettlement = KnowledgeStubs.SettlementsOf(w, Hidden)[0];
        WorldState gone = w.Clone();
        MilitaryUnitRow[] rows = new MilitaryUnitRow[gone.MilitaryUnits.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = gone.MilitaryUnits[i];
        gone.MilitaryUnits.Clear();
        foreach (MilitaryUnitRow r in rows) if (r.Owner != Hidden) gone.MilitaryUnits.Add(r);
        Assert.Empty(FormationsOf(gone, Hidden));

        WorldState more = w.Clone();
        int otherIdentity = cfg.UnitFamilies!.FoundingIdentity.Key + 1;
        MilitaryUnitRow template = default;
        for (int i = 0; i < more.MilitaryUnits.Count; i++)
        {
            ref MilitaryUnitRow u = ref more.MilitaryUnits.Ref(i);
            if (u.Owner != Hidden) continue;
            u = u with { Identity = otherIdentity, Location = new SettlementId(hiddenSettlement), Experience = u.Experience + 3.0 };
            template = u;
        }
        more.MilitaryUnits.Add(template with { Id = 999 });
        Assert.Equal(hidden.Length + 1, FormationsOf(more, Hidden).Length);

        Assert.Equal(baseline, MilitarySurface(lens, gone, cfg, mask, probeFormations, probeSettlements));
        Assert.Equal(baseline, MilitarySurface(lens, more, cfg, mask, probeFormations, probeSettlements));
    }
}
