using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-044 §6 + ADR-047 (M7 audit, 2026-10-27) — THE STORED KNOWN EXTENT USES THE SAME FORMATION SIGHT AS THE QUERY.
///
/// <see cref="PolityKnowledge.Of"/> answers KNOWN as the stored extent ∪ the seed discs ∪ every owned formation's sight disc,
/// with the sight radius improved by the polity's realized research (fog.formationSightNodes, ADR-047). DiscoverySystem
/// must STORE that same union every step (ADR-044 §6: "DiscoverySystem stores that union every step"), so a node the polity
/// once knew stays known when the formation that revealed it is gone (D-040 B2: the extent is permanent once gained; the
/// KnownExtentRow bits are never cleared). Before the fix the stored union used the CONTENT sight radius (research ignored),
/// so the ring between the content radius and the research radius was known only while the formation stood there.
///
/// Today every formation is stationed at a settlement its owner controlled, whose seed disc (radius 3) covers any sight disc
/// the shipped content can reach (≤ 3), so no shipped world exposed the difference; M7 movement would. The rig places a
/// formation in the field (no station) with Σ = 1.0 of sight research (radius 1 → 2).
/// </summary>
public class DiscoveryExtentResearchSightTests
{
    private static readonly PolityId Player = new(1);

    private static TurnExecutor Exec(SimConfig cfg)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())), new OrderLog());
    }

    /// <summary>The dev world under the probe content, the player holding both fog.formationSightNodes probes (Σ = 1.0 →
    /// sight radius 2), and one of its formations moved off its station into the field, far from every settlement.</summary>
    private static (WorldState World, SimConfig Cfg, int FieldNode) FieldFormationWithSightResearch()
    {
        ResearchContent research = KnowledgeEffectsRigs.ProbeOn;
        SimConfig cfg = KnowledgeEffectsRigs.Cfg(research);
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        KnowledgeEffectsRigs.Grant(w, research, Player, KnowledgeEffectsRigs.ProbeNode(KnowledgeOperand.FogFormationSightNodes));
        KnowledgeEffectsRigs.Grant(w, research, Player, KnowledgeEffectsRigs.ExtraSightProbe);

        (int l, int block, _, bool wrap) = DiscoveryQuery.Lattice(w);
        // The node farthest (in disc metric) from every settlement: the field.
        int fieldNode = -1;
        long best = -1;
        for (int n = 0; n < l * l; n++)
        {
            int y = n / l;
            if (y < 4 || y >= l - 4) continue;   // away from the poles' hard edges, so the whole disc is on the map
            long nearest = long.MaxValue;
            for (int s = 0; s < w.Settlements.Count; s++)
            {
                int sn = DiscoveryQuery.NodeOfSettlement(w, w.Settlements[s].Id);
                long dx = Math.Abs(n % l - sn % l);
                if (wrap) dx = Math.Min(dx, l - dx);
                long dy = y - sn / l;
                nearest = Math.Min(nearest, dx * dx + dy * dy);
            }
            if (nearest > best) { best = nearest; fieldNode = n; }
        }
        Assert.True(best > 10 * 10, "the field node lies outside every seed disc and every sight disc of a settlement");

        int unit = -1;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Owner == Player) { unit = i; break; }
        Assert.True(unit >= 0, "the dev world founds the player's warband");
        w.MilitaryUnits[unit] = w.MilitaryUnits[unit] with
        {
            Location = new SettlementId(-1),
            X = (fieldNode % l) * block + 1,
            Y = (fieldNode / l) * block + 1,
        };
        Assert.Equal(fieldNode, DiscoveryQuery.FormationNode(w, w.MilitaryUnits[unit]));
        return (w, cfg, fieldNode);
    }

    private static ulong[] Disc(IReadOnlyWorldState w, int node, int radius)
    {
        (int l, _, _, bool wrap) = DiscoveryQuery.Lattice(w);
        var disc = new ulong[DiscoveryQuery.WordCount(l)];
        DiscoveryQuery.OrDisc(disc, l, wrap, node, radius);
        return disc;
    }

    [Fact]
    public void TheRig_RaisesThePlayersFormationSight_FromTheContentRadiusToTwo()
    {
        (WorldState w, SimConfig cfg, int field) = FieldFormationWithSightResearch();
        Assert.Equal(1, cfg.Fog!.FormationSightNodes);
        Assert.Equal(2, DiscoveryQuery.FormationSightNodes(w, cfg.Research, cfg.Fog, Player));
        // The query already answers the research radius: the whole radius-2 disc is known and visible now.
        PolityKnowledge k = PolityKnowledge.Of(w, Player, cfg);
        (int l, _, _, _) = DiscoveryQuery.Lattice(w);
        ulong[] disc = Disc(w, field, 2);
        for (int n = 0; n < l * l; n++)
            if (DiscoveryQuery.Has(disc, n)) Assert.True(k.Known(n) && k.Visible(n));
    }

    [Fact]
    public void TheStoredExtent_HoldsTheResearchSightDisc_SoTheLandStaysKnownAfterTheFormationLeaves()
    {
        (WorldState w0, SimConfig cfg, int field) = FieldFormationWithSightResearch();
        TurnExecutor exec = Exec(cfg);
        WorldState w1 = exec.Step(w0);

        // 1. DiscoverySystem stored the SAME union the query answers: the research-radius disc, not only the content one.
        ulong[] disc = Disc(w1, field, 2);
        ulong[] stored = DiscoveryQuery.StoredExtent(w1, Player, disc.Length);
        for (int i = 0; i < disc.Length; i++)
            Assert.True(disc[i] == (disc[i] & stored[i]), "word " + i + ": a node of the radius-2 sight disc is missing from the stored extent");

        // 2. The formation is gone (in M7: it marched on, or was destroyed). Its sight goes; the land it revealed stays KNOWN
        //    (now REMEMBERED: known, not visible) — D-040 B2, ADR-044 §6.
        //    ADR-055 RE-RIG (units track, 2026-10-28): the formation is removed CONSISTENTLY — its MilitaryUnits row AND every
        //    row the v39 invariant ties to it (its FormationPersonnel row; it has no re-equipment or escrow) — the state a
        //    destruction leaves. Removing the unit row alone left an orphaned personnel row, which MilitarySystem's
        //    end-of-step invariant refuses; no production path reaches that state (only MilitarySystem's disband removes a
        //    formation, and it removes the personnel row with it).
        WorldState gone = w1.Clone();
        int goneUnit = -1;
        var keep = new List<MilitaryUnitRow>();
        for (int i = 0; i < gone.MilitaryUnits.Count; i++)
        {
            if (gone.MilitaryUnits[i].Owner == Player && gone.MilitaryUnits[i].Location.Value < 0) goneUnit = gone.MilitaryUnits[i].Id;
            else keep.Add(gone.MilitaryUnits[i]);
        }
        Assert.Equal(gone.MilitaryUnits.Count - 1, keep.Count);
        gone.MilitaryUnits.Clear();
        foreach (MilitaryUnitRow row in keep) gone.MilitaryUnits.Add(row);
        var keepPersonnel = new List<FormationPersonnelRow>();
        for (int i = 0; i < gone.FormationPersonnel.Count; i++)
            if (gone.FormationPersonnel[i].Unit != goneUnit) keepPersonnel.Add(gone.FormationPersonnel[i]);
        Assert.Equal(gone.FormationPersonnel.Count - 1, keepPersonnel.Count);   // the military layer is on: the formation had people
        gone.FormationPersonnel.Clear();
        foreach (FormationPersonnelRow row in keepPersonnel) gone.FormationPersonnel.Add(row);
        for (int i = 0; i < gone.Reequips.Count; i++) Assert.NotEqual(goneUnit, gone.Reequips[i].Unit);
        Assert.Null(RecruitmentQuery.InvariantViolation(gone));
        PolityKnowledge k = PolityKnowledge.Of(gone, Player, cfg);
        (int l, _, _, _) = DiscoveryQuery.Lattice(gone);
        int ringNodes = 0;
        ulong[] inner = Disc(gone, field, 1);
        for (int n = 0; n < l * l; n++)
        {
            if (!DiscoveryQuery.Has(disc, n)) continue;
            Assert.True(k.Known(n), "node " + n + " of the radius-2 disc was forgotten when the formation left");
            Assert.False(k.Visible(n));
            Assert.True(k.Remembered(n));
            if (!DiscoveryQuery.Has(inner, n)) ringNodes++;
        }
        Assert.True(ringNodes > 0, "the rig exercises the ring between the content radius (1) and the research radius (2)");

        // 3. And the next stored step keeps every bit (KnownExtentRow bits are never cleared).
        WorldState w2 = exec.Step(gone);
        ulong[] after = DiscoveryQuery.StoredExtent(w2, Player, disc.Length);
        for (int i = 0; i < stored.Length; i++) Assert.Equal(stored[i], stored[i] & after[i]);
    }
}
