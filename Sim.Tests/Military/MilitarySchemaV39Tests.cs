using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 — SCHEMA v39: sections 70–73 (FormationPersonnel 28 bytes, Reequips 56, ReequipEscrow 16, ReequipLog 60) appended
/// after ResearchReserves; the readable SET {32, …, 37, 39}; the v37 → v39 migration ("empty tables"); the Snapshot.Load
/// refusals of tables no rule writes. Constitution rule: every new serialized row type ships a POPULATED-table test (exact
/// ExpectedLength, a bit-exact round trip, hash equality, per-field hash sensitivity).
/// </summary>
public class MilitarySchemaV39Tests
{
    /// <summary>Two formations, two personnel rows (the conserved counts entered through a Ledger source), one pending
    /// re-equipment with two escrow rows and a two-row log — extreme values included.</summary>
    internal static WorldState Populated()
    {
        var w = new WorldState(39);
        var ledger = new Ledger(w.LedgerFlows);
        w.MilitaryUnits.Add(new MilitaryUnitRow(1, new PolityId(1), 3, 301, new SettlementId(0), 4.0, 5.0, 0.5, 0));
        w.MilitaryUnits.Add(new MilitaryUnitRow(7, new PolityId(2), 4, 404, new SettlementId(2), 9.0, 1.0, 0.0, 3));
        w.FormationPersonnel.Add(new FormationPersonnelRow(1, default, 0.25, 0));
        w.FormationPersonnel.Add(new FormationPersonnelRow(7, default, 0.0, long.MaxValue));
        ledger.Flow(ref w.FormationPersonnel.Ref(0).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, 20, FlowDirection.Source, OverdrawPolicy.Throw);
        ledger.Flow(ref w.FormationPersonnel.Ref(1).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, 40, FlowDirection.Source, OverdrawPolicy.Throw);
        w.Reequips.Add(new ReequipRow(7, new PolityId(2), 4, 405, 11, 12, 360, 90, -3));
        w.ReequipEscrow.Add(new ReequipEscrowRow(7, 4, default));
        w.ReequipEscrow.Add(new ReequipEscrowRow(7, 11, default));
        ledger.Flow(ref w.ReequipEscrow.Ref(0).Units, ConservedQuantityIds.OfGood(new GoodId(4)), ReasonIds.InitialEndowment, 15, FlowDirection.Source, OverdrawPolicy.Throw);
        ledger.Flow(ref w.ReequipEscrow.Ref(1).Units, ConservedQuantityIds.OfGood(new GoodId(11)), ReasonIds.InitialEndowment, 5, FlowDirection.Source, OverdrawPolicy.Throw);
        w.ReequipLog.Add(new ReequipLogRow(9, 1, new PolityId(1), 3, 301, 2, 201, 1, 0, 2.0, 0.7));
        w.ReequipLog.Add(new ReequipLogRow(long.MaxValue, int.MaxValue, new PolityId(2), 4, 404, 4, 405, 3, long.MinValue, double.MaxValue, -0.0));
        return w;
    }

    [Fact]
    public void SchemaV39_PopulatedTables_ExpectedLengthBitExactRoundTripHashEquality()
    {
        Assert.Equal(39, CanonicalSchema.Version);
        WorldState world = Populated();
        Assert.Null(RecruitmentQuery.InvariantViolation(world));
        byte[] bytes = SchemaProjection.Serialize(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        var empty = new WorldState(39);
        empty.MilitaryUnits.Add(world.MilitaryUnits[0]);
        empty.MilitaryUnits.Add(world.MilitaryUnits[1]);
        Assert.Equal(2 * 28 + 56 + 2 * 16 + 2 * 60,
            CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(empty) - (world.LedgerFlows.Count - empty.LedgerFlows.Count) * 24L);

        // The last block is section 72 (the log), and its last field the second row's ExperienceAfter (-0.0: its sign bit).
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.ToInt64(bytes, bytes.Length - 8));

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            WorldState back = CanonicalSchema.Read(reader);
            Assert.True(WorldStates.StateEquals(world, back), "round trip drifted");
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
            Assert.Equal(bytes, SchemaProjection.Serialize(back));
            Assert.Equal(20L, back.FormationPersonnel[0].Count.Value);
            Assert.Equal(15L, back.ReequipEscrow[0].Units.Value);
        }

        // Every field is hashed.
        string h = WorldHash.ComputeHex(world);
        var variants = new List<WorldState>();
        WorldState V(Action<WorldState> change) { WorldState c = world.Clone(); change(c); variants.Add(c); return c; }
        V(c => c.FormationPersonnel[0] = c.FormationPersonnel[0] with { Unit = 2 });
        V(c => c.FormationPersonnel.Ref(0).UpkeepRemainder = 0.5);
        V(c => c.FormationPersonnel.Ref(1).UnfedUnits = 1);
        V(c => new Ledger(c.LedgerFlows).Flow(ref c.FormationPersonnel.Ref(0).Count, ConservedQuantityIds.Population, ReasonIds.Deaths, 1, FlowDirection.Sink, OverdrawPolicy.Throw));
        V(c => c.Reequips[0] = c.Reequips[0] with { Unit = 1 });
        V(c => c.Reequips[0] = c.Reequips[0] with { Owner = new PolityId(3) });
        V(c => c.Reequips[0] = c.Reequips[0] with { TargetFamily = 5 });
        V(c => c.Reequips[0] = c.Reequips[0] with { TargetIdentity = 406 });
        V(c => c.Reequips[0] = c.Reequips[0] with { IssuedTurn = 10 });
        V(c => c.Reequips[0] = c.Reequips[0] with { AcceptedTurn = 13 });
        V(c => c.Reequips[0] = c.Reequips[0] with { RequiredDays = 361 });
        V(c => c.Reequips[0] = c.Reequips[0] with { ElapsedDays = 91 });
        V(c => c.Reequips[0] = c.Reequips[0] with { PersonnelDelta = 3 });
        V(c => c.ReequipEscrow.Ref(0).Good = 5);
        V(c => c.ReequipEscrow.Ref(0).Unit = 8);
        V(c => new Ledger(c.LedgerFlows).Flow(ref c.ReequipEscrow.Ref(1).Units, ConservedQuantityIds.OfGood(new GoodId(11)), ReasonIds.Eaten, 1, FlowDirection.Sink, OverdrawPolicy.Throw));
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { Turn = 8 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { Unit = 2 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { Owner = new PolityId(2) });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { FromFamily = 2 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { FromIdentity = 309 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { ToFamily = 4 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { ToIdentity = 202 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { Cause = 2 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { PersonnelDelta = 1 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { ExperienceBefore = 2.5 });
        V(c => c.ReequipLog[0] = c.ReequipLog[0] with { ExperienceAfter = 0.0 });
        V(c => (c.ReequipLog[0], c.ReequipLog[1]) = (c.ReequipLog[1], c.ReequipLog[0]));
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));
        Assert.Equal(20L, world.FormationPersonnel[0].Count.Value);   // the clones were independent

        // Through a snapshot: version 39 in the header, bit-exact load and re-save.
        using var file = new MemoryStream();
        Snapshot.Save(world, file);
        byte[] saved = file.ToArray();
        Assert.Equal(39, BitConverter.ToInt32(saved, 8));
        WorldState loaded = Snapshot.Load(new MemoryStream(saved));
        using var again = new MemoryStream();
        Snapshot.Save(loaded, again);
        Assert.Equal(saved, again.ToArray());
    }

    [Fact]
    public void AV37Stream_LoadsWithTheFourTablesEmpty_AndItsHashIsTheV37Hash()
    {
        Assert.True(CanonicalSchema.IsReadable(37));
        Assert.True(CanonicalSchema.IsReadable(38));   // ADR-054 (v38, the research track's) — the stream just before v39
        Assert.False(CanonicalSchema.IsReadable(40));
        var w = new WorldState(5);
        w.MilitaryUnits.Add(new MilitaryUnitRow(1, new PolityId(1), 3, 301, new SettlementId(0), 0, 0, 0, 0));
        byte[] v39 = SchemaProjection.Serialize(w);
        Assert.Equal(16, CanonicalSchema.BytesAppendedSince(w, 38));
        Assert.Equal(20, CanonicalSchema.BytesAppendedSince(w, 37));   // + the empty v38 ResearchPopulations prefix
        byte[] v37 = v39[..^20];
        using var reader = new BinaryReader(new MemoryStream(v37));
        WorldState migrated = CanonicalSchema.Read(reader, 37, out _);
        Assert.Equal((0, 0, 0, 0), (migrated.FormationPersonnel.Count, migrated.Reequips.Count, migrated.ReequipEscrow.Count, migrated.ReequipLog.Count));
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(migrated));
        Assert.Equal(SchemaProjection.V37(migrated), WorldHash.ComputeHexAsSchema(migrated, 37));
    }

    [Theory]
    [InlineData("unsorted personnel")]
    [InlineData("personnel of no formation")]
    [InlineData("re-equipment complete")]
    [InlineData("escrow without a re-equipment")]
    [InlineData("empty escrow row")]
    [InlineData("bad log cause")]
    public void SnapshotLoad_RefusesTablesNoRuleWrites(string defect)
    {
        WorldState w = Populated();
        switch (defect)
        {
            case "unsorted personnel": (w.FormationPersonnel[0], w.FormationPersonnel[1]) = (w.FormationPersonnel[1], w.FormationPersonnel[0]); break;
            case "personnel of no formation": w.FormationPersonnel.Ref(1).Unit = 8; break;
            case "re-equipment complete": w.Reequips[0] = w.Reequips[0] with { ElapsedDays = 360 }; break;
            case "escrow without a re-equipment": w.ReequipEscrow.Ref(0).Unit = 1; w.ReequipEscrow.Ref(1).Unit = 1; break;
            case "empty escrow row":
                new Ledger(w.LedgerFlows).Flow(ref w.ReequipEscrow.Ref(1).Units, ConservedQuantityIds.OfGood(new GoodId(11)), ReasonIds.Eaten, 5, FlowDirection.Sink, OverdrawPolicy.Throw);
                break;
            case "bad log cause": w.ReequipLog[0] = w.ReequipLog[0] with { Cause = 4 }; break;
        }
        Assert.NotNull(RecruitmentQuery.InvariantViolation(w));
        using var file = new MemoryStream();
        Snapshot.Save(w, file);
        file.Position = 0;
        Assert.Contains("schema v39 invariant", Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(file)).Message);
    }
}
