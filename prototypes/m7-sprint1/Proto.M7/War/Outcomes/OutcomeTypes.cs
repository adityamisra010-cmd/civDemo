namespace Proto.M7.War.Outcomes;

/// <summary>
/// Event and order kinds of the lane W outcome world. Disjoint from T2 (40-43, 140-141), T3 (150-153), lane S (60-70,
/// 180-187), the tactical world (101-108) and the demo kinds.
/// </summary>
public static class OutcomeKinds
{
    /// <summary>Campaign-turn start: prisoner rations settle up to it (sub-stepping) and the ledger is snapshotted.</summary>
    public const int TurnStart = 80;
    /// <summary>TEST DEVICE and stand-in for the M7.13 capture paths other than a lane S siege (a field army taking an
    /// unwalled town, a coup de main): control of settlement A passes to polity B at the event instant.
    /// C = Pack(war id, sackers): the war it is fought in and the victor's men present.</summary>
    public const int Capture = 81;
    /// <summary>Class Resolve at the instant of a control change: the leader rule over the settled control of that instant.</summary>
    public const int ControlSettle = 82;
    /// <summary>Significant: a settlement was taken (opens the post-battle window in which the victor disposes of it).</summary>
    public const int SettlementTaken = 83;
    /// <summary>Significant: a leader's fate changed (executed or taken captive). SourceId = the leader's polity.</summary>
    public const int LeaderFateChanged = 84;
    /// <summary>A field battle's result arrives at its end instant: its entities collapse into formation counts.</summary>
    public const int BattleEnded = 85;
    /// <summary>Significant: post-battle window of a field battle (prisoner decisions).</summary>
    public const int BattleResolved = 86;
    /// <summary>TEST DEVICE: opens a decision window at an instant of the test's choosing.</summary>
    public const int Probe = 87;

    /// <summary>Settlement disposition: A = Pack(issuer, settlement), B = (long)<see cref="Disposition"/>.</summary>
    public const int OrderDispose = 200;
    /// <summary>One-party prisoner action: A = Pack(captor, origin), B = action * ValueStride + count.</summary>
    public const int OrderPrisoners = 201;
    /// <summary>Standing ransom terms of the issuer: A = Pack(issuer, subject) with subject 0 = its soldiers (B = highest price per
    /// head it pays) or 1 = its leader (B = highest lump sum it pays). 0 withdraws the terms.</summary>
    public const int OrderRansomTerms = 202;
    /// <summary>Ransom of the issuer's prisoners of one origin: A = Pack(captor, origin), B = price per head.</summary>
    public const int OrderRansom = 203;
    /// <summary>Standing exchange offer: A = Pack(issuer, other polity), B = how many of the other's men it offers to give back.</summary>
    public const int OrderExchange = 204;
    /// <summary>Decision on a captive leader: A = Pack(captor, the leader's polity), B = decision * ValueStride + price.</summary>
    public const int OrderLeader = 205;

    public const long ValueStride = 1L << 32;

    public static bool IsOwn(int kind) => (kind >= TurnStart && kind <= Probe) || (kind >= OrderDispose && kind <= OrderLeader);

    /// <summary>War code of a capture outside any war.</summary>
    public const int NoWar = 65_535;

    /// <summary>The Capture test device's C operand: war id (-1: none) and the victor's men present.</summary>
    public static long PackCapture(int warId, long sackers)
    {
        if (sackers < 0 || warId < -1 || warId >= NoWar) throw new ArgumentOutOfRangeException(nameof(warId));
        return checked(sackers * 65_536 + (warId < 0 ? NoWar : warId));
    }
}

/// <summary>Reasons an outcome order is executed as a recorded refusal (it changes nothing).</summary>
public static class OutcomeRefusal
{
    public const int BadTarget = 1;
    /// <summary>The settlement was not taken by the issuer at this instant (no post-battle choice exists for it here).</summary>
    public const int NotEligible = 2;
    /// <summary>The post-battle window of that capture has passed (the choice is offered only at the capture instant).</summary>
    public const int WindowClosed = 3;
    public const int AlreadyDisposed = 4;
    /// <summary>Razing the issuer's only city would make it lose all its cities by its own choice.</summary>
    public const int LastCity = 5;
    public const int BadValue = 6;
    /// <summary>The issuer holds fewer prisoners of that origin than the order needs.</summary>
    public const int NotHeld = 7;
    /// <summary>The issuer's institutions do not allow the action (state-derived).</summary>
    public const int Institution = 8;
    /// <summary>The other party has no standing terms or offer that the order can meet.</summary>
    public const int NoTerms = 9;
    public const int CannotPay = 10;
    public const int NotCaptor = 11;
    /// <summary>A Director-level rule forbids the action (captive-leader execution where the leader rule does not apply).</summary>
    public const int NotPermitted = 12;
    public const int UnknownKind = 13;
    /// <summary>Fix r1 (W-R1-6, PROPOSAL rule L13): a polity may not sack or raze a settlement whose population is its own (a re-capture
    /// is a liberation: Occupy is its only choice).</summary>
    public const int OwnPopulation = 14;
}

/// <summary>Fate of a polity's leader. Stable codes (hashed and serialized).</summary>
public enum LeaderFate : byte
{
    Reigning = 0,
    /// <summary>Held by another polity (its seat was taken while the polity still held another city).</summary>
    Captive = 1,
    /// <summary>Dead by execution. Final: no transition leaves it.</summary>
    Executed = 2,
}

/// <summary>Why a leader's fate changed. Stable codes.</summary>
public enum FateCause : byte
{
    None = 0,
    /// <summary>DIR5 §4.1: the empire lost all its cities.</summary>
    LastCityLost = 1,
    /// <summary>The leader's seat was taken while the polity still held another city.</summary>
    SeatTaken = 2,
    Released = 3,
    Ransomed = 4,
    /// <summary>A captor's decision (only under <see cref="CaptiveExecutionRule.Permitted"/>, a Director-level choice).</summary>
    ExecutedAsCaptive = 5,
}

/// <summary>What the victor does with a settlement it has taken (the post-battle choice). Stable codes.</summary>
public enum Disposition : byte
{
    /// <summary>Keep and hold it (the default when the victor makes no choice at the window).</summary>
    Occupy = 0,
    /// <summary>Plunder it for the customary days and keep it.</summary>
    Sack = 1,
    /// <summary>Plunder it, expel the survivors and destroy it; it ceases to be a city.</summary>
    Raze = 2,
}

/// <summary>Custody movements of prisoners (the prisoner record). Stable codes.</summary>
public enum PrisonerAction : byte
{
    /// <summary>Prisoners came into custody (a garrison taken in a siege, men who surrendered in a battle).</summary>
    Intake = 0,
    /// <summary>Freed on parole: they go home but not back into the ranks.</summary>
    Release = 1,
    /// <summary>Pressed into the captor's own forces (not under a prisoner convention).</summary>
    Conscript = 2,
    /// <summary>Sold or kept as slaves (only where the captor's institutions include slavery and no prisoner convention binds it).</summary>
    Enslave = 3,
    /// <summary>Returned against a price paid by their own polity under its standing terms (back into its ranks).</summary>
    Ransom = 4,
    /// <summary>Returned one for one against the same number of the captor's own men.</summary>
    Exchange = 5,
}

/// <summary>Decision of a captor about a captive leader. Stable codes.</summary>
public enum LeaderDecision : byte
{
    Release = 1,
    Ransom = 2,
    Execute = 3,
}

/// <summary>DIRECTOR-LEVEL choice (C32 candidate flag, not decided here): may a captor execute a captive leader where the
/// leader rule of DIR5 §4.1 does not apply? Every caller passes one explicitly; there is no default.</summary>
public enum CaptiveExecutionRule : byte
{
    NotPermitted = 0,
    Permitted = 1,
}

/// <summary>DIRECTOR-LEVEL choice (C32 candidate flag "whether razing kills or displaces population", not decided here; fix r1,
/// W-R1-8). Every caller passes one explicitly; there is no default. Both options take the same loot and burn the same; they differ
/// only in the civilians.</summary>
public enum RazePopulationRule : byte
{
    /// <summary>The raze kills no one: every inhabitant is expelled to the refugees of the population's polity.</summary>
    Displace = 0,
    /// <summary>The sack's killing and flight over the customary days, then every survivor is expelled (the lane's first proposal).</summary>
    KillThenDisplace = 1,
}

/// <summary>
/// Institutions of a polity that the outcome rules read. A polity HOLDS a flag because its computed state says so (a law
/// adopted, a convention it is bound by, a coinage): Law 4, no date or era gate. The caller passes the projection (in the
/// campaign: the institution and civics tables), exactly as lane E's <c>PolityCapability</c> stands in for the research table.
/// </summary>
[Flags]
public enum OutcomeInstitution : uint
{
    None = 0,
    /// <summary>Minted coin: ransom is paid and received in money.</summary>
    Coinage = 1u << 0,
    /// <summary>Its law recognises slavery: captives may be enslaved.</summary>
    Slavery = 1u << 1,
    /// <summary>It is bound by a prisoner-of-war convention: no enslavement, no conscription of prisoners.</summary>
    PrisonerConvention = 1u << 2,
}

/// <summary>How a war ended (the M7 record; M8 owns treaties and their content). Stable codes.</summary>
public enum WarOutcome : byte
{
    Ongoing = 0,
    /// <summary>Every participant of one side was eliminated (its leader executed on the loss of its last city).</summary>
    Conquest = 1,
    /// <summary>Both sides' last participants were eliminated at the same instant.</summary>
    MutualCollapse = 2,
    /// <summary>Ended by a peace: written by M8, never by M7 (reserved code, no treaty fields here).</summary>
    NegotiatedPeace = 3,
}

/// <summary>Why a participant left a war. Stable codes.</summary>
public enum ParticipantExit : byte
{
    None = 0,
    /// <summary>It lost all its cities (its leader executed at that instant, or already dead: fix r1, W-R1-1).</summary>
    Eliminated = 1,
}

/// <summary>Kind of a recorded battle. Stable codes.</summary>
public enum BattleKind : byte
{
    Field = 0,
    Siege = 1,
    /// <summary>A settlement taken outside a siege (the Capture stand-in).</summary>
    Storming = 2,
}

/// <summary>Which mechanism settled a battle. Stable codes.</summary>
public enum BattleMechanism : byte
{
    Tactical = 0,
    Auto = 1,
    /// <summary>Lane S's siege-health calculation (end code = lane S <c>SiegeOutcome</c>).</summary>
    SiegeHealth = 2,
    /// <summary>The Capture stand-in (no battle simulated).</summary>
    StandIn = 3,
}

/// <summary>Casualties by cause (the record). Stable codes. Soldiers' causes 1-6, civilians' 10-12.</summary>
public enum CasualtyCause : byte
{
    Killed = 1,
    Wounded = 2,
    Captured = 3,
    Deserted = 4,
    /// <summary>A garrison that marched out on terms (not a loss of people; recorded so the side's numbers close).</summary>
    MarchedOut = 6,
    CiviliansKilled = 10,
    CiviliansFled = 11,
}

/// <summary>
/// Coefficients of the post-battle mechanisms (Law 2: coefficients inside resolution equations, no free-floating buff; Law 3:
/// per-sim-year rates integrated over the time they act). Provenance: DERIVED (from a cited custom or figure), PLACEHOLDER (no
/// source; a prototype value for Director review). The loot rate is NOT here: a sack takes movable wealth at lane E's forced
/// plunder rate (<c>LogisticsRates.ForageForcedPermilleYear</c>, reused through <c>StockDraw</c>).
/// </summary>
public sealed record OutcomeCoefficients
{
    /// <summary>Days of plunder in a sack. DERIVED: the customary three days (Rome 410, Constantinople 1453).</summary>
    public long SackDays { get; init; } = 3;
    /// <summary>Civilian deaths in a sack, ‰ per sim-year of those still there (360-day year, applied daily), at one sacker per
    /// inhabitant or more (scaled down in proportion below that). PLACEHOLDER: with <see cref="SackFleePermilleYear"/>, three days
    /// remove about 30 %, a third of them dead.</summary>
    public long SackKillPermilleYear { get; init; } = 13_452;
    /// <summary>Civilian flight in a sack (same scaling). PLACEHOLDER.</summary>
    public long SackFleePermilleYear { get; init; } = 26_904;
    /// <summary>Buildings burnt in a sack, ‰ per sim-year of those standing (scaled like the killing). PLACEHOLDER: about 10 % in three days.</summary>
    public long SackBurnPermilleYear { get; init; } = 12_420;
    /// <summary>Occupation grievance (‰ per sim-year, the input of lane S's occupation unrest) added per whole population harmed:
    /// grievance = the settlement's base grievance + this × (killed + fled) / population before. PLACEHOLDER.</summary>
    public long HarmGrievancePermilleYear { get; init; } = 4_000;

    public void Validate()
    {
        if (SackDays < 0 || SackKillPermilleYear < 0 || SackFleePermilleYear < 0 || SackBurnPermilleYear < 0 || HarmGrievancePermilleYear < 0)
            throw new ArgumentOutOfRangeException(nameof(SackDays), "outcome coefficients must be non-negative");
    }

    public static readonly OutcomeCoefficients Proposal = new();
}

/// <summary>A polity of the outcome world. Seat = the settlement where its leader resides (-1: none). In a world bound to a lane S
/// siege world the money, grain, troops and refugees live in lane S's accounts and the opening amounts here must be 0.</summary>
public sealed record OutcomePolity(int Id, int LeaderId)
{
    public int Seat { get; init; } = -1;
    public long Treasury { get; init; }
    public long Granary { get; init; }
    public long HomeTroops { get; init; }
    public OutcomeInstitution Institutions { get; init; }
}

/// <summary>A settlement. IsCity: it counts for the leader rule. C31's READING of "cities" is "the settlements the polity controls";
/// that forts, harbours, airports and depots (IsCity = false) do NOT count is THIS LANE's narrower reading, an open question (DQ7),
/// not C31's or Sim.Core's. Installation ≥ 0: bound to that lane S installation (control, civilians and grain are lane S's;
/// the amounts here must then be 0).</summary>
public sealed record OutcomeSettlement(int Id, bool IsCity, int Controller)
{
    /// <summary>The polity its population belongs to; -1 = the initial controller (lane S's rule).</summary>
    public int PopulationPolity { get; init; } = -1;
    public long Civilians { get; init; }
    /// <summary>Grain, gm (D-015: 1,000 gm = one adult's year of food).</summary>
    public long Grain { get; init; }
    /// <summary>Movable wealth in money units.</summary>
    public long Money { get; init; }
    public long Buildings { get; init; }
    /// <summary>The population's base disposition toward a foreign ruler (‰ per sim-year; lane S's grievance input).</summary>
    public long BaseGrievancePermilleYear { get; init; }
    public int Installation { get; init; } = -1;
}

/// <summary>A war (an INPUT: declared outside M7). Its War Pulses run from StartMs (its own phase).</summary>
public sealed record OutcomeWar(int Id, long StartMs, int[] Side0, int[] Side1);

/// <summary>A campaign formation (people in one ledger account) that can fight a field battle.</summary>
public sealed record OutcomeFormation(int Id, int Owner, long Personnel);
