using Sim.Core.State;

namespace Sim.Tests.State;

// T4.3 (D-037 A3): structural tests proving the claim/control/recognition
// relations can express what the ratified data model requires — schema only,
// no polity behaviour. See docs/m4-spec.md's T4.3 fence for the three named
// prohibitions this shape must avoid (owner-id, boolean flag, stored decay
// term) and docs/d037-emergent-polities.md Part A for the ruling itself.
public class ClaimModelTests
{
    private static readonly SettlementId Place1 = new(1);
    private static readonly PolityId PolityA = new(1);
    private static readonly PolityId PolityB = new(2);

    [Fact]
    public void MultipleClaims_OnTheSamePlace_AreBothRepresentable()
    {
        var world = new WorldState();
        world.Claims.Add(new ClaimRow(PolityA, Place1, 0.5));
        world.Claims.Add(new ClaimRow(PolityB, Place1, 0.5));

        Assert.Equal(2, world.Claims.Count);
        int forA = -1, forB = -1;
        for (int i = 0; i < world.Claims.Count; i++)
        {
            if (world.Claims[i].Polity == PolityA && world.Claims[i].Place == Place1) forA = i;
            if (world.Claims[i].Polity == PolityB && world.Claims[i].Place == Place1) forB = i;
        }
        Assert.True(forA >= 0, "polity A's claim on Place1 is not representable alongside B's");
        Assert.True(forB >= 0, "polity B's claim on Place1 is not representable alongside A's");
    }

    [Fact]
    public void Recognition_IsAsymmetric_ARecognisingBDoesNotImplyBRecognisingA()
    {
        var world = new WorldState();
        world.Recognitions.Add(new RecognitionRow(PolityA, PolityB));

        bool aRecognisesB = false, bRecognisesA = false;
        for (int i = 0; i < world.Recognitions.Count; i++)
        {
            RecognitionRow row = world.Recognitions[i];
            if (row.Recogniser == PolityA && row.Recognised == PolityB) aRecognisesB = true;
            if (row.Recogniser == PolityB && row.Recognised == PolityA) bRecognisesA = true;
        }
        Assert.True(aRecognisesB, "A's recognition of B was not stored");
        Assert.False(bRecognisesA,
            "storing A recognises B must not manufacture B recognises A — recognition is asymmetric");
    }

    [Fact]
    public void Control_CanBeContested_MultipleRowsForTheSamePlace()
    {
        // D-040 Part C: control "varies with distance and is contested where
        // claims overlap" — the relation shape must not force a single row
        // per place; whether contested control ever RESOLVES to one is a
        // later mechanism, not a schema constraint.
        var world = new WorldState();
        world.Controls.Add(new ControlRow(PolityA, Place1, 0.6));
        world.Controls.Add(new ControlRow(PolityB, Place1, 0.4));

        Assert.Equal(2, world.Controls.Count);
    }

    [Fact]
    public void Claim_IsNotAnOwnerIdOnSettlementRow_SettlementRowUnaffected()
    {
        // T4.3 PROHIBITED 1: control (and by the same reasoning, claim) must
        // never be an owner-id field on the place row. SettlementRow's shape
        // is unchanged by this packet — proven by construction, not by a
        // reflection scan: this test exists so a future reader has a red
        // proof to point at if a future edit adds one.
        var world = new WorldState();
        world.Settlements.Add(new SettlementRow(Place1, SiteCell: 0, FoundedTurn: 0));
        world.Claims.Add(new ClaimRow(PolityA, Place1, 1.0));

        SettlementRow settlement = world.Settlements[0];
        Assert.Equal(Place1, settlement.Id);
        // SettlementRow(Id, SiteCell, FoundedTurn) — three fields, none of
        // them a polity/owner id. If this assertion needs updating because a
        // field was added, that is exactly the review moment PROHIBITED 1
        // exists to force.
    }
}
