using Proto.M7.Spatial;
using Proto.M7.Tactical;

namespace Proto.M7.Sandbox;

/// <summary>
/// [D] War Sandbox modes (DIR5 §2.13, D-079 DR30: "Quick Battle, Scenario Editor, Battle Test, Replay and Inspection, later
/// Campaign Encounter"). Replay and Inspection is not a mode with its own rules: it re-runs any finished session from its logs
/// (<see cref="SandboxSession.Replay"/>). Stable codes.
/// </summary>
public enum SandboxMode : byte
{
    QuickBattle = 1,
    ScenarioEditor = 2,
    BattleTest = 3,
    /// <summary>A battle loaded from the campaign (later): it inherits the campaign's terrain, which may not be edited.</summary>
    CampaignEncounter = 4,
}

/// <summary>[D] Why a sandbox edit is refused (recorded; it changes nothing). Stable codes; None = accepted.</summary>
public enum SandboxRefusal : byte
{
    None = 0,
    /// <summary>DIR5 §2.13: "Loaded campaign encounters inherit campaign terrain. Do not allow campaign encounter terrain editing."</summary>
    TerrainLocked = 1,
    /// <summary>Not a designed unit family the prototype can field (no battle profile).</summary>
    UnknownFamily = 2,
    BadSide = 3,
    OutOfGrid = 4,
    /// <summary>The battle has started: the scenario is closed (deployment and battle rules apply from here).</summary>
    BattleStarted = 5,
    /// <summary>A deployment edit lane TR's DeploymentPhase refused (its own code is in the row's C field).</summary>
    DeploymentRefused = 6,
    /// <summary>The battle cannot start before the deployment phase closed on a valid deployment (DIR5 §2.14).</summary>
    NotDeployed = 7,
    NoForces = 8,
    NonPositive = 9,
    /// <summary>The deployment phase is already open (forces can no longer be added: the thirds are being edited).</summary>
    DeploymentOpen = 10,
    /// <summary>[CB, verification round 1, CB-R1-02] The sandbox mint holds fewer people than the force asks for (its opening
    /// <see cref="SandboxSession.MintOpening"/> less every force minted so far). Refused before anything is minted: under D-082 DA5
    /// a force of any size is one formation, so a force above the mint reached the mint transfer, which threw after the formation's
    /// id and ledger account were taken and before the edit was logged.</summary>
    MintShort = 11,
}

/// <summary>[D] Kinds of sandbox session edits (the session log; stable codes).</summary>
public enum SandboxEditKind : byte
{
    TerrainEdit = 1,
    AddForce = 2,
    OpenDeployment = 3,
    Deploy = 4,
    CloseDeployment = 5,
    StartBattle = 6,
}

/// <summary>[D] Who commands a side in a sandbox battle. Both AI = an AI-vs-AI battle (DIR5 §2.13: AutoResolver, no rendering).</summary>
public enum SandboxCommand : byte { Ai = 0, Player = 1 }

/// <summary>
/// [D] A designed unit family the sandbox can instantiate (DIR5 §2.13 "Any designed unit family can be instantiated regardless of
/// campaign era"; §2.15). IdentityAge is the identity's Age in unit-families.json (cited line) or, for a prototype identity with no
/// data row, a labelled TEST INPUT. The prototype can field only the families that have a battle profile.
/// </summary>
public sealed record DesignedFamily(int FamilyKey, int IdentityKey, string Name, int IdentityAge, CapFamily CapClass, UnitProfile Profile, string AgeSource)
{
    /// <summary>[D, verification round 1, D-R1-09] The domain weapons every entity of the family carries (lane N's
    /// <see cref="FormationSpec.Weapons"/>; null = none). A crew-served gun's profile has Ranged 0 because its gun is given here:
    /// without it the formation cannot fire. Read-only (the sandbox copies it into each formation it creates).</summary>
    public IReadOnlyList<WeaponSpec>? Weapons { get; init; }
}

/// <summary>[D] The designed families with prototype battle profiles (prototype constants; Ages cited, never read from Sim.Data).</summary>
public static class SandboxCatalogue
{
    private const string Uf = "Sim.Data/content/unit-families.json";

    public static IReadOnlyList<DesignedFamily> All { get; } = new DesignedFamily[]
    {
        new(3, 301, "warband", 1, CapFamily.HeavyInfantry, UnitProfiles.HeavyInfantry, Uf + ":75 (age 1)"),
        new(4, 402, "archers", 2, CapFamily.ArchersLightInfantry, UnitProfiles.Archers, Uf + ":97 (age 2)"),
        new(4, 4991, "riflemen(prototype)", 8, CapFamily.ArchersLightInfantry, DomainProfiles.Riflemen,
            "TEST INPUT: prototype identity 4991 has no data row; the rifle era of 407 sharpshooters, " + Uf + ":102 (age 8)"),
        new(4, 4990, "sniper_team(prototype)", 8, CapFamily.Snipers, DomainProfiles.Sniper,
            "TEST INPUT: no sniper identity exists (CAP-4); the rifle era of " + Uf + ":102 (age 8)"),
        // [D, verification round 1, D-R1-09] fielded WITH lane N's field gun (the weapon of the field artillery it cites); before
        // round 1 the gun crew had no gun and could not fire (verifier probe P2).
        new(8, 4992, "gun_crew(prototype)", 8, CapFamily.Uncapped, DomainProfiles.GunCrew,
            "TEST INPUT: prototype identity 4992; field artillery 805, " + Uf + ":163 (age 8)")
        { Weapons = Array.AsReadOnly(new[] { DomainProfiles.FieldGun }) },
    };

    public static DesignedFamily? Find(int familyKey, int identityKey)
    {
        foreach (var f in All) if (f.FamilyKey == familyKey && f.IdentityKey == identityKey) return f;
        return null;
    }
}

/// <summary>
/// [D] Campaign availability by COMPUTED Age (DIR5 §2.15 "Campaign availability remains gated by era and technology"; D-079 §1.5
/// READING R2: "era" is the computed Age state of ADR-031, so Law 4 holds). An identity is available to a campaign polity only at
/// or after its Age (the knowledge gate of ADR-055 applies on top and is not modelled here). The sandbox does NOT call this
/// (DR33: "Sandbox supports all designed unit families regardless of campaign-era availability").
/// </summary>
public static class CampaignAvailability
{
    public static bool Available(int computedAge, DesignedFamily f) => computedAge >= 1 && f.IdentityAge <= computedAge;
}

/// <summary>[D] One sandbox session edit as executed (Refusal 0 = accepted). Field use by kind: TerrainEdit (A = cell x, B = cell y,
/// C = class × 2^40 + height mm); AddForce (A = persons, B = anchor x, C = anchor y; FormationId = the first formation created);
/// Deploy (FormationId; B, C = anchor); OpenDeployment / CloseDeployment / StartBattle (C = lane TR's DeploymentRefusal code or
/// the ResolverKind used).</summary>
public readonly record struct SandboxEditRow(int Index, byte Kind, int Side, int FamilyKey, int IdentityKey, long FormationId, long A, long B, long C, byte Refusal);

/// <summary>[D] The sandbox casualty account of one formation after its battle: persons, and lane W's collapse split.</summary>
public readonly record struct SandboxCasualtyRow(long FormationId, int Side, long Persons, long Fit, long Wounded, long RoutedReturned, long Deserted,
                                                 long Dead, long Captured);

/// <summary>[D] What a Campaign Encounter copies from the campaign at load: the campaign's battle terrain (a copy) and, per campaign
/// formation, its side, designed family and persons. A VALUE: the sandbox holds no reference to the campaign world.</summary>
public sealed record CampaignEncounterSnapshot(TerrainGrid Terrain, (int CampaignFormation, int Side, int FamilyKey, int IdentityKey, long Persons, Vec2L Anchor)[] Forces);
