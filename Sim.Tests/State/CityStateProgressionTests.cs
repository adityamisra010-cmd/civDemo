using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// R2a — CITY-STATE PROGRESSION (Director decisions 4, 12, 15 CITY STATES; docs/city-state-progression.md). An
/// uncontrolled settlement researches through the SAME ResearchSystem, content and predicates under its reserved
/// local knowledge-holder key, slower (tuning.cityStatePaceFraction), with no orders.
/// </summary>
public class CityStateProgressionTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));

    private static TurnExecutor Executor(OrderLog? log = null)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), log);
    }

    /// <summary>The dev world with its LAST settlement released from the realm (a revolt's end state).</summary>
    private static (WorldState World, SettlementId City) Released()
    {
        WorldState w = DevSolo.Value.Clone();
        SettlementId city = w.Settlements[w.Settlements.Count - 1].Id;
        Assert.False(EmpireQuery.TryGetCapital(w, Player, out SettlementId cap) && cap == city);
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != city) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow c in kept) w.Controls.Add(c);
        return (w, city);
    }

    private static WorldState Run(WorldState w, int turns)
    {
        TurnExecutor exec = Executor();
        for (int t = 0; t < turns; t++) w = exec.Step(w);
        return w;
    }

    private static int Completed(IReadOnlyWorldState w, PolityId holder)
    {
        int n = 0;
        for (int i = 0; i < w.ResearchCompleted.Count; i++) if (w.ResearchCompleted[i].Polity.Value == holder.Value) n++;
        return n;
    }

    private const int Turns = 320;   // MEASURED: a dev-world city-state of ~380 people makes ~1.3 RP/turn; the cheapest root node costs 260
    private static readonly Lazy<(WorldState World, SettlementId City)> Developed = new(() =>
    {
        (WorldState w, SettlementId city) = Released();
        return (Run(w, Turns), city);
    });

    [Fact]
    public void C1_UncontrolledSettlement_DevelopsWithoutPlayerOrders()
    {
        (WorldState w, SettlementId city) = Developed.Value;
        PolityId local = SettlementKnowledge.LocalHolder(city);
        Assert.True(Completed(w, local) > 0, "the city-state completed nothing in " + Turns + " turns");
        Assert.Equal(0, Completed(w, Player));            // the player issued no research order
        Assert.True(ResearchQuery.Population(w, local) > 0);
        // Its own knowledge is what its settlement-scoped predicates read.
        bool[] mask = SettlementKnowledge.MaskOf(w, Research, city)!;
        Assert.Equal(ResearchQuery.CompletedMask(w, Research, local), mask);
    }

    [Fact]
    public void C2_Progression_IsDeterministic()
    {
        (WorldState a, _) = Released();
        (WorldState b, _) = Released();
        a = Run(a, 60);
        b = Run(b, 60);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.True(WorldStates.StateEquals(a, b));
    }

    [Fact]
    public void C3_Progression_IsSlowerThanAnEquivalentOrganizedCivilization()
    {
        (WorldState w, SettlementId city) = Released();
        PolityId local = SettlementKnowledge.LocalHolder(city);
        double cityRp = ResearchQuery.CityStateResearchPoints(w, Research, city);
        double organizedRp = ResearchQuery.ResearchPoints(Research.Tuning, ResearchQuery.Population(w, local));
        Assert.True(cityRp > 0.0);
        Assert.Equal(Research.Tuning.CityStatePaceFraction * organizedRp, cityRp);
        Assert.True(Research.Tuning.CityStatePaceFraction is > 0.0 and < 1.0);

        // Measured: the same settlement under the realm (the player researching the cheapest node whenever idle,
        // the city-state's own rule) completes more knowledge over the same turns than it does on its own.
        (WorldState organized, _) = (DevSolo.Value.Clone(), 0);
        TurnExecutor exec = Executor();
        for (int t = 0; t < 60; t++)
        {
            var log = new OrderLog();
            if (!ResearchQuery.TryGetTarget(organized, Player, out _))
            {
                bool[] done = ResearchQuery.CompletedMask(organized, Research, Player);
                int pick = AiResearchPolicy.Cheapest(organized, Research, Player, ResearchQuery.AvailableMask(Research, done), null);
                if (pick >= 0) log.Append(ResearchQuery.TargetOrder(organized, Research, Player, Research.Nodes[pick].Key)!.Value);
            }
            organized = Executor(log).Step(organized);
        }
        WorldState alone = Run(w, 60);
        Assert.True(Completed(alone, local) < Completed(organized, Player),
            $"city-state {Completed(alone, local)} vs organized {Completed(organized, Player)}");
    }

    [Fact]
    public void C4_AccumulatedDevelopment_PersistsThroughSaveLoad()
    {
        (WorldState w, SettlementId city) = Developed.Value;
        using var buffer = new MemoryStream();
        Snapshot.Save(w, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer, w.Terrain);
        Assert.Equal(SettlementKnowledge.MaskOf(w, Research, city), SettlementKnowledge.MaskOf(loaded, Research, city));
        Assert.Equal(WorldHash.ComputeHex(Executor().Step(w.Clone())), WorldHash.ComputeHex(Executor().Step(loaded)));
    }

    [Fact]
    public void C5_LaterControl_DoesNotResetAccumulatedState_NorGrantItToTheController()
    {
        (WorldState developed, SettlementId city) = Developed.Value;
        WorldState w = developed.Clone();
        PolityId local = SettlementKnowledge.LocalHolder(city);
        bool[] own = ResearchQuery.CompletedMask(w, Research, local);
        int before = Completed(w, local);
        w.Controls.Add(new ControlRow(Player, city, 1.0));   // the realm takes the city back
        w = Run(w, 5);
        Assert.Equal(before, Completed(w, local));           // dormant, never deleted
        bool[] mask = SettlementKnowledge.MaskOf(w, Research, city)!;
        bool[] realm = ResearchQuery.CompletedMask(w, Research, Player);
        for (int i = 0; i < own.Length; i++)
        {
            if (own[i]) Assert.True(mask[i], Research.Nodes[i].Id);   // the city keeps what it learned
        }
        int ownOnly = 0;
        for (int i = 0; i < own.Length; i++) if (own[i] && !realm[i]) ownOnly++;
        Assert.True(ownOnly > 0, "control vacuous: the realm already knew everything the city did");
        Assert.Equal(0, Completed(w, Player));               // the controller is NOT granted the city's knowledge
    }

    [Fact]
    public void C6_UncontrolledSettlement_DoesNotBypassResearchPrerequisites()
    {
        (WorldState w, SettlementId city) = Developed.Value;
        PolityId local = SettlementKnowledge.LocalHolder(city);
        // Every node it completed had its prerequisites completed by itself (completion order = row order).
        var known = new bool[Research.Nodes.Count];
        for (int r = 0; r < w.ResearchCompleted.Count; r++)
        {
            ResearchCompletedRow row = w.ResearchCompleted[r];
            if (row.Polity.Value != local.Value) continue;
            int i = Research.IndexOf(row.Node);
            Assert.True(ResearchQuery.IsAvailable(Research, i, known, ResearchQuery.StageReached(Research, known)), Research.Nodes[i].Id);
            known[i] = true;
        }
        // And a directive aimed at it is ignored: nobody can order a city-state (an injected order for its key).
        WorldState start = w.Clone();
        int locked = -1;
        bool[] avail = ResearchQuery.AvailableMask(Research, known);
        for (int i = 0; i < avail.Length && locked < 0; i++) if (!avail[i] && !known[i]) locked = i;
        var log = new OrderLog();
        log.Append(OrderRecord.From(start.Clock.Turn, local, OrderKind.SetResearchTarget, Research.Nodes[locked].Key.Value, 0.0));
        WorldState next = Executor(log).Step(start);
        Assert.False(ResearchQuery.TryGetTarget(next, local, out ResearchNodeId t) && t.Value == Research.Nodes[locked].Key.Value);
    }

    [Fact]
    public void LocalHolderRows_PopulatedTables_LengthRoundTripAndHashExact()
    {
        var world = new WorldState(7);
        PolityId local = SettlementKnowledge.LocalHolder(new SettlementId(3));
        Assert.Equal(-4, local.Value);
        Assert.Equal(3, SettlementKnowledge.SettlementOf(local).Value);
        world.ResearchCompleted.Add(new ResearchCompletedRow(local, new ResearchNodeId(426)));
        world.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(1), new ResearchNodeId(1)));
        world.ResearchProgress.Add(new ResearchProgressRow(local, new ResearchNodeId(76), 12.345678901234567));
        world.ResearchTargets.Add(new ResearchTargetRow(local, new ResearchNodeId(76)));
        world.ResearchEurekas.Add(new ResearchEurekaRow(local, new ResearchNodeId(81), 0));
        world.ResearchCredits.Add(new ResearchCreditRow(local, new ResearchNodeId(81), 1, 0.30000000000000004));

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        Assert.Equal(2 * 8 + 1 * 16 + 1 * 8 + 1 * 12 + 1 * 20, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(7)));
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
        Assert.Equal(-4, back.ResearchCompleted[0].Polity.Value);
        Assert.Equal(BitConverter.DoubleToInt64Bits(12.345678901234567), BitConverter.DoubleToInt64Bits(back.ResearchProgress[0].Progress));
    }

    [Fact]
    public void CityStateResearch_IsInert_InAWorldWithNoControlRelation_OrWithThePaceAtZero()
    {
        (WorldState w, SettlementId city) = Released();
        Assert.True(SettlementKnowledge.ResearchesLocally(w, Research, city));
        WorldState toy = w.Clone();
        toy.Controls.Clear();
        Assert.False(SettlementKnowledge.ResearchesLocally(toy, Research, city));
        Assert.Null(SettlementKnowledge.MaskOf(toy, Research, city));
        ResearchContent off = TestConfigs.PreTradeKnowledge(Cfg).Research!;
        Assert.Equal(0.0, off.Tuning.CityStatePaceFraction);
        Assert.False(SettlementKnowledge.ResearchesLocally(w, off, city));
    }
}
