using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.State;

/// <summary>
/// M4 (D-042) Empire Control Foundation. These tests pin the STRUCTURAL claims
/// the packet's definition of done makes — that Empire identity is the reused
/// D-037 PolityId, that membership derives from control, that the capital is a
/// designation rather than the identity, that capital loss and extinction are
/// both representable, and that command source is separate from simulation
/// state — plus the constitution's populated-table serialization rule.
/// </summary>
public class EmpireFoundationTests
{
    private static WorldState WorldWithTwoEmpires()
    {
        var world = new WorldState();
        world.Polities.Add(new PolityRow(new PolityId(1), CommandSource.Player));
        world.Polities.Add(new PolityRow(new PolityId(2), CommandSource.Ai));

        // 1 holds three settlements, 2 holds one.
        world.Controls.Add(new ControlRow(new PolityId(1), new SettlementId(10), 1.0));
        world.Controls.Add(new ControlRow(new PolityId(1), new SettlementId(11), 1.0));
        world.Controls.Add(new ControlRow(new PolityId(1), new SettlementId(12), 1.0));
        world.Controls.Add(new ControlRow(new PolityId(2), new SettlementId(20), 1.0));

        world.Capitals.Add(new CapitalRow(new PolityId(1), new SettlementId(10)));
        world.Capitals.Add(new CapitalRow(new PolityId(2), new SettlementId(20)));
        return world;
    }


    /// <summary>
    /// Table has no RemoveAt (Add/Clear only), so losing a row means rebuilding
    /// the table without it — which is exactly what a relation-drop is.
    /// </summary>
    private static void DropRow<T>(Sim.Core.State.Table<T> table, int drop) where T : unmanaged
    {
        var kept = new T[table.Count];
        int n = 0;
        for (int i = 0; i < table.Count; i++)
        {
            if (i != drop) kept[n++] = table[i];
        }

        table.Clear();
        for (int i = 0; i < n; i++) table.Add(kept[i]);
    }

    [Fact]
    public void EmpireIdentityIsThePolityId_NoSecondIdentityExists()
    {
        // The packet forbids a new EmpireId alongside PolityId. The roster row's
        // identity field IS a PolityId, and the same value addresses the polity in
        // the pre-existing D-037 relations — one identity, three tables.
        WorldState world = WorldWithTwoEmpires();
        var one = new PolityId(1);

        Assert.Equal(one, world.Polities[0].Id);
        Assert.Equal(one, world.Controls[0].Polity);
        Assert.Equal(one, world.Capitals[0].Polity);
    }

    [Fact]
    public void Membership_DerivesFromControl_AndFollowsItWhenASettlementIsLost()
    {
        WorldState world = WorldWithTwoEmpires();
        Assert.Equal(3, EmpireQuery.ControlledCount(world, new PolityId(1)));
        Assert.Equal(1, EmpireQuery.ControlledCount(world, new PolityId(2)));

        // Transfer settlement 11 from 1 to 2. Nothing else is edited — if a
        // roster list existed anywhere it would now be stale, and this would fail.
        world.Controls[1] = new ControlRow(new PolityId(2), new SettlementId(11), 1.0);

        Assert.Equal(2, EmpireQuery.ControlledCount(world, new PolityId(1)));
        Assert.Equal(2, EmpireQuery.ControlledCount(world, new PolityId(2)));
    }

    [Fact]
    public void CapitalIsADesignation_NotTheIdentity_AndItsLossLeavesTheEmpireIntact()
    {
        WorldState world = WorldWithTwoEmpires();
        Assert.True(EmpireQuery.TryGetCapital(world, new PolityId(1), out SettlementId seat));
        Assert.Equal(10, seat.Value);

        // Sack the capital: remove the designation AND the control of that place.
        DropRow(world.Capitals, 0);
        DropRow(world.Controls, 0);

        Assert.False(EmpireQuery.TryGetCapital(world, new PolityId(1), out _));
        // The Empire survives — still registered, still commanding two settlements.
        Assert.False(EmpireQuery.IsExtinct(world, new PolityId(1)));
        Assert.Equal(2, EmpireQuery.ControlledCount(world, new PolityId(1)));
        Assert.True(EmpireQuery.TryGetCommandSource(world, new PolityId(1), out _));
    }

    [Fact]
    public void CapitalCanBeMoved_WithoutTouchingIdentityOrHoldings()
    {
        WorldState world = WorldWithTwoEmpires();
        world.Capitals[0] = new CapitalRow(new PolityId(1), new SettlementId(12));

        Assert.True(EmpireQuery.TryGetCapital(world, new PolityId(1), out SettlementId seat));
        Assert.Equal(12, seat.Value);
        Assert.Equal(3, EmpireQuery.ControlledCount(world, new PolityId(1)));
        Assert.Equal(1, world.Polities[0].Id.Value);
    }

    [Fact]
    public void ExtinctionIsRepresentable_WhenNoControlledSettlementRemains()
    {
        WorldState world = WorldWithTwoEmpires();
        Assert.False(EmpireQuery.IsExtinct(world, new PolityId(2)));

        DropRow(world.Controls, 3);   // polity 2's only holding
        Assert.True(EmpireQuery.IsExtinct(world, new PolityId(2)));
        Assert.Equal(0, EmpireQuery.ControlledCount(world, new PolityId(2)));

        // The identity persists so the record of who they were is not erased,
        // and a capital designation for a landless polity is not contradictory.
        Assert.True(EmpireQuery.TryGetCommandSource(world, new PolityId(2), out _));
    }

    [Fact]
    public void AnUnregisteredPolityHasNoCommandSource_RatherThanADefaultedOne()
    {
        WorldState world = WorldWithTwoEmpires();
        Assert.False(EmpireQuery.TryGetCommandSource(world, new PolityId(99), out CommandSource source));
        Assert.Equal(default, source);
        Assert.False(EmpireQuery.IsPlayerCommanded(world, new PolityId(99)));
        // ...and it is extinct by the same derived rule, holding nothing.
        Assert.True(EmpireQuery.IsExtinct(world, new PolityId(99)));
    }

    [Fact]
    public void CommandSourceIsStructurallySeparateFromSimulationState()
    {
        // Player and AI empires are distinguished ONLY by the roster row. Flipping
        // the source changes who issues orders and touches no control, no capital,
        // and no conserved stock.
        WorldState world = WorldWithTwoEmpires();
        Assert.True(EmpireQuery.IsPlayerCommanded(world, new PolityId(1)));
        Assert.False(EmpireQuery.IsPlayerCommanded(world, new PolityId(2)));

        int controlsBefore = world.Controls.Count;
        int capitalsBefore = world.Capitals.Count;
        world.Polities[0] = new PolityRow(new PolityId(1), CommandSource.Ai);

        Assert.False(EmpireQuery.IsPlayerCommanded(world, new PolityId(1)));
        Assert.Equal(controlsBefore, world.Controls.Count);
        Assert.Equal(capitalsBefore, world.Capitals.Count);
        Assert.Equal(3, EmpireQuery.ControlledCount(world, new PolityId(1)));
    }

    [Fact]
    public void Clone_CarriesPopulatedPolityAndCapitalTables_AndTheCopyIsIndependent()
    {
        WorldState world = WorldWithTwoEmpires();
        WorldState copy = world.Clone();

        Assert.Equal(2, copy.Polities.Count);
        Assert.Equal(2, copy.Capitals.Count);
        Assert.Equal(CommandSource.Player, copy.Polities[0].Source);
        Assert.Equal(20, copy.Capitals[1].Place.Value);
        Assert.True(TestUtil.WorldStates.StateEquals(world, copy));

        copy.Polities.Add(new PolityRow(new PolityId(3), CommandSource.Ai));
        copy.Capitals[0] = new CapitalRow(new PolityId(1), new SettlementId(11));
        Assert.Equal(2, world.Polities.Count);
        Assert.Equal(10, world.Capitals[0].Place.Value);
        Assert.False(TestUtil.WorldStates.StateEquals(world, copy));
    }

    [Fact]
    public void SchemaV22_PopulatedPolityAndCapitalTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-table
        // test — exact ExpectedLength, bit-exact round trip, hash equality.
        // The fixture holds both command sources (so a dropped enum write is
        // visible), a capital-LESS polity, and a landless polity with a capital.
        var world = new WorldState();
        world.Polities.Add(new PolityRow(new PolityId(7), CommandSource.Player));
        world.Polities.Add(new PolityRow(new PolityId(3), CommandSource.Ai));
        world.Polities.Add(new PolityRow(new PolityId(0), CommandSource.Ai));
        world.Capitals.Add(new CapitalRow(new PolityId(3), new SettlementId(42)));
        world.Capitals.Add(new CapitalRow(new PolityId(0), new SettlementId(0)));

        Assert.Equal(22, CanonicalSchema.Version);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(TestUtil.WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        Assert.Equal(3, back.Polities.Count);
        Assert.Equal(7, back.Polities[0].Id.Value);
        Assert.Equal(CommandSource.Player, back.Polities[0].Source);
        Assert.Equal(CommandSource.Ai, back.Polities[1].Source);
        Assert.Equal(2, back.Capitals.Count);
        Assert.Equal(3, back.Capitals[0].Polity.Value);
        Assert.Equal(42, back.Capitals[0].Place.Value);
        // polity 7 is registered with no capital row — a capital-less Empire
        Assert.False(EmpireQuery.TryGetCapital(back, new PolityId(7), out _));
    }
}
