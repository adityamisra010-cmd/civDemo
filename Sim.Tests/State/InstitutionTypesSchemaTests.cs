using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-057 (M6 CIV, P3; D-066 A1: institution types are content-driven) — NO SCHEMA CHANGE. The second institution type
/// rides the existing <c>InstitutionRow.Type</c> (an int key): the layout is v39's, unchanged — the same 32 bytes per row,
/// the same version, the same section — and a table MIXING university rows and law-court rows round-trips bit for bit with
/// an exact ExpectedLength and an equal hash (the populated-table rule: an empty table proves nothing). The generalization
/// itself is pinned to change nothing for a university: its profile is institutions.universities exactly, every university
/// shares one market, and the per-type staffing reads the old arithmetic.
/// </summary>
public sealed class InstitutionTypesSchemaTests
{
    private static WorldState Mixed()
    {
        var w = new WorldState(57);
        w.Institutions.Add(new InstitutionRow(4, new PolityId(2), new SettlementId(3), 6, 120, 1.0 - Math.Exp(-10.0 / 16.0)));   // a court
        w.Institutions.Add(new InstitutionRow(1, new PolityId(1), new SettlementId(0), 2, 7, 0.95));                            // a university
        w.Institutions.Add(new InstitutionRow(9, new PolityId(1), new SettlementId(0), 6, 8, 0.0));                             // a court, just founded
        w.Institutions.Add(new InstitutionRow(int.MaxValue, new PolityId(0), new SettlementId(int.MaxValue), 6, long.MaxValue, 1.0));
        w.Institutions.Add(new InstitutionRow(2, new PolityId(3), new SettlementId(1), 5, 0, -0.0));                            // a university
        w.Institutions.Add(new InstitutionRow(5, new PolityId(1), new SettlementId(2), 6, 9, BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0057)));
        return w;
    }

    [Fact]
    public void MixedTypes_PopulatedTable_SameLayout_LengthRoundTripAndHashExact()
    {
        Assert.Equal(39, CanonicalSchema.Version);   // ADR-057: NO schema bump — the type is the existing int column
        WorldState world = Mixed();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);
        // the layout is unchanged: 32 bytes per institution row whatever its type (InstitutionSchemaTests' figure)
        Assert.Equal(6 * 32, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(57)));
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow a = world.Institutions[i], b = back.Institutions[i];
            Assert.Equal((a.Id, a.Polity, a.Settlement, a.Type, a.FoundedTurn), (b.Id, b.Polity, b.Settlement, b.Type, b.FoundedTurn));
            Assert.Equal(BitConverter.DoubleToInt64Bits(a.Maturity), BitConverter.DoubleToInt64Bits(b.Maturity));
        }
        // the type is state: changing a court into a university changes the hash
        WorldState c = world.Clone();
        c.Institutions[0] = c.Institutions[0] with { Type = 2 };
        Assert.NotEqual(WorldHash.ComputeHex(world), WorldHash.ComputeHex(c));
    }

    /// <summary>The generalization changes nothing for a university: its profile IS institutions.universities, every
    /// university type shares market 0 (an unknown key too — the pre-ADR-057 reading), each sim.json type is its own market,
    /// and a university's per-type staff is the old σ × A_u × M bit for bit.</summary>
    [Fact]
    public void Universities_ReadExactlyTheirOldNumbers_EveryOtherTypeItsOwn()
    {
        SimConfig cfg = TestConfigs.Sim();
        UniversitiesConfig u = cfg.Institutions!.Universities;
        foreach (InstitutionTypeInfo t in InstitutionContent.Types(cfg))
        {
            InstitutionProfile p = InstitutionContent.Profile(cfg.Institutions, t.Key);
            Assert.Equal(t.University, p.University);
            if (t.University)
            {
                Assert.Equal(InstitutionContent.UniversityProfile(u), p);
                Assert.Equal(0, InstitutionContent.MarketOf(cfg.Institutions, t.Key));
                foreach (double m in new[] { 0.0, 0.3, 1.0 - Math.Exp(-10.0 / 16.0), 1.0 })
                    Assert.Equal(u.StaffShareAtMaturity * u.AdultsPerUniversity * m, InstitutionStaffing.StaffOfInstitution(cfg, t.Key, m));
            }
            else Assert.Equal(t.Key, InstitutionContent.MarketOf(cfg.Institutions, t.Key));
        }
        Assert.Equal(InstitutionContent.UniversityProfile(u), InstitutionContent.Profile(cfg.Institutions, 999));   // unknown key
        Assert.Equal(0, InstitutionContent.MarketOf(cfg.Institutions, 999));
        Assert.Equal([1, 2, 3, 4, 5, 6], InstitutionContent.Types(cfg).Select(t => t.Key).ToArray());
        // the switch-off twin has the universities alone
        Assert.Equal([1, 2, 3, 4, 5], InstitutionContent.Types(TestConfigs.PreLawCourt(cfg)).Select(t => t.Key).ToArray());
    }
}
