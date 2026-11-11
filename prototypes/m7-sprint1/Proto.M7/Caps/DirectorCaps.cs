using Proto.M7.Tactical;

namespace Proto.M7.Caps;

/// <summary>
/// [D] What one unit of a cap counts (lane D proposal, docs/m7-plan/caps-and-counted-objects-proposal.md §2). Stable codes.
/// People are always the conserved stock (Law 1); a machine (vehicle, hull, airframe, crewed tube) is a battle-transient
/// entity derived from the people who crew it (D-079 §1.5 R7, V1), so a machine-counted cap is checked on the number of
/// machines the formation's people crew: ceil(persons / persons per machine).
/// [CB, D-082 DA5] Every cap counts BATTLE ENTITIES ("Battlefield units"; READING DA5-R (a), applied): the counted object of a
/// class is the battle entity the formation fights as (<see cref="DirectorCaps.EntitiesOf"/>). For a machine class one entity is
/// one machine with its crew (unchanged); for a soldier class one entity is one soldier while the formation's persons are within
/// the cap, and above it each entity stands for persons ÷ entities persons (docs/m7-plan/battle-entity-proposal.md §2).
/// </summary>
public enum CountedObject : byte
{
    /// <summary>One battle entity of soldiers (heavy/aggregated infantry, archers and lighter infantry, snipers): one soldier while
    /// the formation's persons are within the cap; [CB, D-082 DA5] above it, an equal share of the formation's persons.</summary>
    Soldier = 0,
    /// <summary>One armoured vehicle with its crew (tanks).</summary>
    Vehicle = 1,
    /// <summary>One helicopter with its crew.</summary>
    Helicopter = 2,
    /// <summary>One vessel with its company (every vessel family: D-079 §1.5 R6).</summary>
    Hull = 3,
    /// <summary>One fixed-wing aircraft with its crew.</summary>
    FixedWingAirframe = 4,
    /// <summary>One mortar tube with the crew that serves it (the RECOMMENDED counted object for the mortar cap, PROPOSAL).</summary>
    CrewedTube = 5,
    /// <summary>No counted object is capped (D-076 §5 CAP-5; D-079 §1.5 R6: cavalry, siege, air defence, missiles, reconnaissance).</summary>
    Uncounted = 6,
}

/// <summary>[D] Why a formation creation or edit is refused. Stable codes; None = accepted.</summary>
public enum CapRefusal : byte
{
    None = 0,
    /// <summary>The request is not positive, or the formation would be left with no one in it (an empty formation is
    /// disbanded explicitly, never by an edit).</summary>
    NonPositive = 1,
    /// <summary>The formation would hold more counted objects than its class's Director cap. [CB, D-082 DA5] Only a machine class
    /// can: its people crew one machine each, so persons above cap × persons per machine need a machine past the cap; a soldier
    /// class fights as at most cap entities whatever its persons, so it is never refused for size.</summary>
    ExceedsCap = 2,
    /// <summary>The two formations are of different cap classes or identities (one formation is one identity).</summary>
    ClassMismatch = 3,
    UnknownFormation = 4,
    DuplicateId = 5,
    /// <summary>The source formation holds fewer people than the edit moves out of it.</summary>
    InsufficientPersons = 6,
    /// <summary>The owner's depot holds fewer people than the edit draws.</summary>
    DepotShort = 7,
    /// <summary>A merge or transfer names the same formation twice.</summary>
    SameFormation = 8,
    /// <summary>The formation belongs to another owner.</summary>
    NotOwner = 9,
    /// <summary>A machine-counted class without a persons-per-machine input (CAP-3: a TUNE rule of M7.6; no value is shipped).</summary>
    NoCrewRule = 10,
}

/// <summary>[D] Outcome of one cap check or roster edit: the refusal, the formation it concerns (-1 none), the counted objects
/// the formation would hold and the class cap (long.MaxValue for an uncapped class).</summary>
public readonly record struct CapVerdict(CapRefusal Refusal, long FormationId, long Counted, long Cap)
{
    public bool Accepted => Refusal == CapRefusal.None;
    public static CapVerdict Refused(CapRefusal r, long formationId) => new(r, formationId, 0, 0);
}

/// <summary>
/// [D] The Director's per-formation entity caps (D-079 DR24, DIR5 §2.10: "Recover and preserve the existing numerical caps":
/// heavy/aggregated infantry 50; archers/light infantry 60; tanks 5; helicopters 3; ships 5; fixed-wing aircraft 1; snipers 2;
/// mortars 5). Values are Director decisions (DR24 closes O-11 as to values); the counted object of each class is lane D's
/// proposal; the persons-per-machine rule of the machine classes is a TUNE input (D-079 §4 D10: "a crew-per-machine TUNE rule in
/// M7.6") with NO shipped value here — a machine-counted check without it is refused (NoCrewRule), never guessed.
/// PROTOTYPE CONSTANTS: nothing is read from or written to Sim.Data (D10: "Nothing changes in data now"). The values equal
/// lane N's <see cref="ProposedCaps.Proposed"/> table (the battle-entry check), pinned equal by a test so the two cannot drift.
/// Uncapped classes reuse lane N's CAP-5 treatment (<see cref="CapFamily.Uncapped"/>: no cap value, never refused for size).
/// [CB, D-082 DA5: "Battlefield units"; DA5-R (a), applied] THE ONE RULE OF LANES W AND D: a cap bounds the battle entities a
/// formation fights as (<see cref="EntitiesOf"/>, used by the roster, the War Sandbox's Quick Battle and its Campaign Encounter
/// alike); a formation's size in persons is limited by the economy, not by the cap. Before D-082 this class counted the persons
/// of a soldier class (READING C1 (a), lane D), so a heavy-infantry formation held at most 50 people; that is superseded
/// (D-082 §3). Machine classes keep one entity per machine (lane D's crewed tube, lane N's hulls and airframes), so their persons
/// stay bounded by cap × persons per machine; uncapped classes keep one entity per person. What one entity standing for many
/// persons is (hit points, output, morale weight) is a design proposal (docs/m7-plan/battle-entity-proposal.md), not built here.
/// </summary>
public sealed class DirectorCaps
{
    public const string Citation = "D-079 DR24 (DIR5 §2.10): heavy/aggregated infantry 50; archers/light infantry 60; tanks 5; helicopters 3; ships 5; fixed-wing aircraft 1; snipers 2; mortars 5";
    private static readonly long[] Dr24 = { 50, 60, 5, 3, 5, 1, 2, 5 };   // indexed by CapFamily 0..7
    private readonly long[] _crew;                                        // persons per counted machine; 0 = no rule supplied

    private DirectorCaps(long[] crew) { _crew = crew; }

    /// <summary>The DR24 caps with no persons-per-machine rule (machine classes are refused with NoCrewRule).</summary>
    public static DirectorCaps Values { get; } = new(new long[8]);

    /// <summary>A copy with the persons-per-machine rule of one machine class set (a TUNE input; tests label their values).</summary>
    public DirectorCaps WithCrew(CapFamily f, long personsPerMachine)
    {
        if (!IsMachineCounted(f)) throw new ArgumentException("only a machine-counted class has a persons-per-machine rule", nameof(f));
        if (personsPerMachine <= 0) throw new ArgumentOutOfRangeException(nameof(personsPerMachine));
        var c = (long[])_crew.Clone();
        c[(int)f] = personsPerMachine;
        return new DirectorCaps(c);
    }

    public static CountedObject CountedObjectOf(CapFamily f) => f switch
    {
        CapFamily.HeavyInfantry or CapFamily.ArchersLightInfantry or CapFamily.Snipers => CountedObject.Soldier,
        CapFamily.Tanks => CountedObject.Vehicle,
        CapFamily.Helicopters => CountedObject.Helicopter,
        CapFamily.Ships => CountedObject.Hull,
        CapFamily.FixedWing => CountedObject.FixedWingAirframe,
        CapFamily.Mortars => CountedObject.CrewedTube,
        CapFamily.Uncapped => CountedObject.Uncounted,
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    public static bool IsMachineCounted(CapFamily f) => CountedObjectOf(f) is CountedObject.Vehicle or CountedObject.Helicopter
        or CountedObject.Hull or CountedObject.FixedWingAirframe or CountedObject.CrewedTube;

    /// <summary>The DR24 cap in counted objects; long.MaxValue for an uncapped class (no value is invented).</summary>
    public static long CapOf(CapFamily f) => f == CapFamily.Uncapped ? long.MaxValue : Dr24[(int)f];

    /// <summary>Persons per counted machine of a machine class (0 when no rule was supplied); 1 for a soldier-counted class (one
    /// soldier per entity while the persons are within the cap) and for an uncapped class (one entity per person).</summary>
    public long PersonsPerObject(CapFamily f) => IsMachineCounted(f) ? _crew[(int)f] : 1;

    /// <summary>The most people a formation of the class may hold under the cap: cap × persons per machine for a machine class
    /// (-1 without a rule). [CB, D-082 DA5] long.MaxValue for a soldier class (the cap bounds its entities, not its persons; its
    /// size is limited by the economy) and, as before, for an uncapped class. Before D-082 a soldier class's maximum was its cap.</summary>
    public long MaxPersons(CapFamily f)
    {
        if (f == CapFamily.Uncapped || !IsMachineCounted(f)) return long.MaxValue;
        long per = PersonsPerObject(f);
        return per <= 0 ? -1 : checked(CapOf(f) * per);
    }

    /// <summary>[CB] The persons at which a formation of the class fields its cap's full complement of entities with one person
    /// (soldier class) or one crew (machine class) per entity: cap × persons per object. -1 for a machine class without a rule;
    /// long.MaxValue for an uncapped class. Equal to <see cref="MaxPersons"/> for a machine class; for a soldier class it is the
    /// cap, which since D-082 DA5 is no longer a maximum (persons above it share the cap's entities).</summary>
    public long FullStrengthPersons(CapFamily f)
    {
        if (f == CapFamily.Uncapped) return long.MaxValue;
        long per = PersonsPerObject(f);
        return per <= 0 ? -1 : checked(CapOf(f) * per);
    }

    /// <summary>
    /// [CB, D-082 DA5] THE ONE RULE: the battle entities a formation of <paramref name="persons"/> people of class
    /// <paramref name="f"/> fights as.
    /// <list type="bullet">
    /// <item>Soldier-counted class (heavy/aggregated infantry 50, archers and lighter infantry 60, snipers 2): min(persons, cap), the
    /// most entities the cap allows. While persons ≤ cap each entity is one person (lane W's P = n case); above it each entity stands
    /// for an equal share persons ÷ entities (a rational share: integers appear once, at the collapse, by the largest-remainder
    /// method of <see cref="Proto.M7.War.Outcomes.BattleCollapse.Apportion"/>, so persons are conserved exactly, Law 1).</item>
    /// <item>Machine-counted class (tanks, helicopters, ships, fixed-wing aircraft, mortars as crewed tubes): one entity per machine,
    /// ceil(persons ÷ persons per machine) (lane D §2.1: one entity per tube; lane N: hulls and airframes are the entities). This
    /// can exceed the cap, which <see cref="Check"/> refuses. No persons-per-machine value is shipped (CAP-3, M7.6): without one,
    /// this throws, and the check refuses (NoCrewRule).</item>
    /// <item>Uncapped class (CAP-5, lane N's treatment): one entity per person, never refused for size.</item>
    /// </list>
    /// </summary>
    public long EntitiesOf(CapFamily f, long persons)
    {
        if (persons < 0) throw new ArgumentOutOfRangeException(nameof(persons));
        if (f == CapFamily.Uncapped) return persons;
        if (!IsMachineCounted(f)) return Math.Min(persons, CapOf(f));
        long per = PersonsPerObject(f);
        if (per <= 0) throw new InvalidOperationException("no persons-per-machine rule for " + f);
        return persons / per + (persons % per == 0 ? 0 : 1);
    }

    /// <summary>Counted objects of a formation holding <paramref name="persons"/> people. [CB, D-082 DA5] The counted objects ARE
    /// the battle entities: <see cref="EntitiesOf"/>. Before D-082 a soldier class counted its persons (READING C1 (a), superseded):
    /// a partly crewed machine is still a machine, as before; a soldier class now counts min(persons, cap).</summary>
    public long CountOf(CapFamily f, long persons) => EntitiesOf(f, persons);

    /// <summary>The cap check of one formation of <paramref name="persons"/> people (deterministic, pure).</summary>
    public CapVerdict Check(long formationId, CapFamily f, long persons)
    {
        if (persons <= 0) return CapVerdict.Refused(CapRefusal.NonPositive, formationId);
        if (IsMachineCounted(f) && PersonsPerObject(f) <= 0) return CapVerdict.Refused(CapRefusal.NoCrewRule, formationId);
        long counted = CountOf(f, persons), cap = CapOf(f);
        return new CapVerdict(counted > cap ? CapRefusal.ExceedsCap : CapRefusal.None, formationId, counted, cap);
    }
}
