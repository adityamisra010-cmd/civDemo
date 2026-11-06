using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Time;
using Proto.M7.WarEcon;

namespace Proto.M7.War.Siege;

public readonly record struct SiegePulseRecord(int SiegeId, long Index, long StartMs, long EndMs, SiegeRegime Regime,
    long HealthRaw, long AttackerHealthRaw, long WallRaw, long WorksRaw, long MoraleRaw, long AttackerMoraleRaw,
    long DiseaseRaw, long NutritionRaw, long Garrison, long Civilians, long Attackers, long Food, long Water, long Munitions, long Missiles,
    long CapabilityMilli, long AssaultPowerMilli, long RationRaw);

public readonly record struct SiegeEndRecord(int SiegeId, SiegeOutcome Outcome, long AtMs, long Turn, long DurationMs, long Pulses,
    int Assaults, int FailedAssaults, int Mines, long HealthRaw, long AttackerHealthRaw, long Garrison, long Civilians, long Attackers);

/// <summary>Purpose 1 assault, 2 relief battle, 3 sally, 4 raid.</summary>
public readonly record struct BattleRecord(int SiegeOrRaidId, int Purpose, long AtMs, long AttackMilli, long DefenceMilli, long OddsRaw,
    long FortuneRaw, bool Success, long AttackerLosses, long DefenderLosses);

public readonly record struct SiegeLatch(int LogIndex, int Issuer, int Kind, int TargetId, long IssuedMs, long EffectiveMs, long EffectiveIndex, long Value);

public readonly record struct SiegeOrderRefusal(int LogIndex, int Issuer, int Kind, long AtMs, int Reason);

public readonly record struct CaptureRecord(int InstallationId, int From, int To, long AtMs, SiegeOutcome Cause, long Prisoners, long MarchedOut);

public readonly record struct RaidRecord(int RaidId, int InstallationId, long AtMs, bool Success, long Pillaged, long RaiderLosses, long GarrisonLosses);

public readonly record struct RevoltRecord(int InstallationId, long AtMs);

public readonly record struct SiegeTurnSnapshot(long Turn, long AtMs, long[] Balances);

/// <summary>
/// Phase C lane S world: sieges evaluated pulse by pulse through a siege-health calculation, and garrisons (DIR5 §2.2, §2.3;
/// C15, C30). All people, food, water, ammunition, ordnance and money move only through <see cref="WarLedger"/> transfers,
/// so every commodity total is constant (Law 1).
///
/// LANE E MECHANISMS REUSED (Proto.M7.WarEcon): food is Grain (gm) at lane E's rations; garrison pay is lane E's pay table by
/// arm, in coin with Coinage and in kind (grain) without it, with arrears whose age in months drives desertion; desertion and
/// disease mortality are <see cref="TroopLossRates"/> (Sanitation capability); every per-head loss rate is integrated by
/// <see cref="HeadcountLoss"/>; gunpowder fire draws lane E Munitions (kg per combat day) and needs the Gunpowder capability;
/// the besiegers' line of communication is <see cref="ArmyLogistics.Evaluate"/> (carriage loss, throughput).
///
/// CLOCKS. (1) The THEATRE clock: the War Pulses of the war (WarPulseSchedule from WarStartMs) — garrison orders, garrison
/// upkeep, unrest and raids settle on it. (2) Each siege's EVALUATION clock, a parameter (D-079 Q14 OPEN): the War Pulses
/// (<see cref="SiegeClockKind.WarPulse"/>, the first pulse truncated at the siege start) or fixed tactical battle pulses
/// anchored at the siege start (<see cref="SiegeClockKind.BattlePulse"/>). (3) Campaign-turn starts split every in-progress
/// pulse (DIR5 §2.4 "Use sub-stepping"): linear economic flows settle up to the boundary and the ledger is snapshotted.
///
/// EVENTS at one instant X (class System; ties by priority, then SourceId):
///   0 TheatrePulseEnd   settle upkeep; theatre desertion and occupation unrest of the rest of the pulse (fix r2, S2-V2).
///   1 SiegePulseEnd(s)  settle linear flows; the siege-health calculation over the pulse; termination; maximum duration.
///   2 TurnStart         the straddle split for the theatre and every siege; ledger snapshot.  (RaidArrival also at 2, SourceId ≥ 1,000,000.)
///   3 TheatrePulseStart latch garrison orders; resolve raids that have arrived.
///   4 ReliefArrival(s)  marks the relief present (it fights at the siege pulse start at or after its arrival).
///   5 SiegePulseStart(s) begin the siege (first pulse); latch siege orders; AI doctrine; relief battle, lift, sally, assault.
/// Significant (class 70): SiegeEnded, AssaultResolved, Revolt, Probe.
///
/// ORDER DELIVERY (WA1: an order issued exactly at a boundary applies to the pulse that starts at that instant):
///   siege orders (ration, assault, sally, lift) take effect at the first SIEGE pulse start B ≥ T (if T is the current pulse's
///   start they act at once); garrison orders (assign, withdraw, replace, reinforce) at the first THEATRE pulse start B ≥ T.
///   Every check is made when the order takes effect; a failed check is a recorded refusal that changes nothing.
///
/// LINEAR vs NONLINEAR. Within a pulse the linear flows (rations at the current ration level, water inflow, corridor, the
/// besiegers' supply line, ammunition, and the garrison's pay on the theatre clock) are integrated exactly with carries
/// (RateIntegrator.IntegrateExact), split at turn starts and before any change of the people they depend on (siege pulse ends,
/// assaults and sallies settle the theatre's pay first: fix r1, S-V2). Wherever both are settled, garrison pay is settled BEFORE the
/// sieges' flows, so on a short home granary pay in kind is fed before a corridor or the besiegers' supply at every settlement, on
/// every clock (fix r2, S2-V6: the theatre pulse end paid first; the siege pulse end, turn start and raid fed the sieges first). The nonlinear siege-health calculation runs once per pulse at
/// its end over the whole pulse length (SiegeEquations; stable implicit steps), and only there do people die, desert or
/// leave. Discrete actions (battles) happen at pulse starts.
///
/// MAXIMUM DURATION (DIR5 §2.2 "three campaign-turn equivalents"; READING RDG-2): Cap = start + 3 × L, L = the length of the
/// campaign turn containing the start instant, fixed at the start. The pulse in progress is truncated at Cap; at Cap, after
/// that pulse's siege-health calculation, the siege is adjudicated: the side with the LOWER health yields — defender
/// lower → CapCapitulation (surrender on terms: the garrison marches out, control passes); otherwise (including equal health)
/// → CapLifted (the besiegers withdraw). The siege is active at Cap − 1 ms and over at Cap.
/// </summary>
public sealed class SiegeWorld : IEventWorld
{
    /// <summary>Carry slots per people account. 0-4: the rate flows (carries in units of 1/<see cref="HeadcountLoss.One"/> of a head,
    /// i.e. 10⁻¹⁸ head: below a whole head nothing is dropped, so the loss does not depend on the pulse length; fix r1, S-V1); 5: battle
    /// losses (carry in Q32 raw); 6: unused (evacuation has its own exact carry); 7: desertion of a garrison outside a siege
    /// (theatre clock, 10⁻¹⁸ head).</summary>
    private const int FCombat = 0, FThirst = 1, FStarve = 2, FDisease = 3, FDesert = 4, FAssault = 5, FEvac = 6, FTheatreDesert = 7;
    public const int FlowCount = 8;
    private const int RateFlows = 5;
    private const long RaidSourceBase = 1_000_000;
    /// <summary>Pending-order sentinels: no order, or an AI doctrine decision (not an order; never logged as a latch).</summary>
    private const int NoOrder = -1, DoctrineOrder = -2;

    internal sealed class Inst
    {
        public InstallationSpec Spec = null!;
        public int Controller;
        public int Civ, Granary, Cistern, Magazine, MissileStore, Guns;
        public Q32 Wall, Works, Unrest, Loyalty;
        public int ActiveSiege = -1;
        public bool Revolted;
        public readonly long[] CivCarry = new long[FlowCount];
        public ExactFraction UpkeepCarry;
        /// <summary>Pay due and paid in the current theatre pulse; arrears outstanding (money, or gm of grain without Coinage) and the
        /// instant they began (-1: none), lane E's arrears clock.</summary>
        public long UpkeepDue, UpkeepPaid, UpkeepArrears, ArrearsSinceMs = -1;
        public Q32 UnpaidShare;
        /// <summary>The instant up to which this installation's theatre-clock desertion and unrest are integrated (fix r2, S2-V2): split
        /// at every siege start and end on it, so each part of a theatre pulse is charged at the state that held over it.</summary>
        public long TheatreLossSettledMs;
    }

    internal sealed class Form
    {
        public FormationSpec Spec = null!;
        public int Account;
        public FormationState State;
        public int At = -1;
        public long X, Y;
        public readonly long[] Carry = new long[FlowCount];
    }

    internal sealed class SiegeS
    {
        public SiegeSpec Spec = null!;
        public Inst I = null!;
        public int Defender = -1;
        public readonly int[] AttPeople = new int[4];
        public readonly int[] AttEngines = new int[4];
        public int AttGranary, AttMunitions, AttMissiles;
        /// <summary>Lane E supply line of the besiegers (fixed at construction: route, train and capability do not change).</summary>
        public bool HasLine;
        public ExactFraction LineLoss;
        public long LineCapacityPerYear, Shipped, Delivered;
        public readonly int[] Relief = new int[4];
        public bool Begun, Active;
        public SiegeOutcome Outcome;
        public long EndMs = -1, CapMs, StartTurnLengthMs;
        public long PulseIndex = -1, PulseStart, PulseEnd, LastSettled;
        public WarPulse Cursor;
        public Q32 NutG = Q32.One, NutC = Q32.One, NutA = Q32.One, Dis, DisA, Morale, MoraleA, Mining, Approach, Ration = Q32.One, AttWorks;
        public Q32 HealthD = Q32.One, HealthA = Q32.One, MisShare = Q32.One, MunShare = Q32.One, MisShareA = Q32.One, MunShareA = Q32.One, SupplyA = Q32.One;
        public long DRef, ARef, G0, A0, C0, LabourRef;
        public long LastAssaultMs = long.MinValue / 4;
        public bool ReliefArrived, ReliefResolved, ReliefFailed;
        public int Assaults, FailedAssaults, Sallies, Mines;
        public long DueG, DrawG, DueC, DrawC, DueWG, DrawWG, DueWC, DrawWC, DueMis, DrawMis, DueMun, DrawMun, DueA, DrawA, DueMisA, DrawMisA, DueMunA, DrawMunA;
        public ExactFraction CFoodG, CFoodC, CWaterIn, CWaterG, CWaterC, CMis, CMun, CCorrFood, CCorrMun, CCorrMis, CSupplyA, CLineCap, CEatA, CMisA, CMunA,
                             CEvac, CReinf, CReinfA;
        public readonly long[] AttCarry = new long[4 * FlowCount];
        public readonly long[] EngCarry = new long[4];
        public readonly long[] ReliefCarry = new long[4];
        public long PendRation = -1, PendAssault = -1, PendSally = -1;
        /// <summary>A human ration order has taken effect: the ration is a standing order and the AI doctrine no longer sets it (fix r1, S-V6).</summary>
        public bool RationHeld;
        /// <summary>Per-arm carry of the besiegers' reinforcement split, in units of 1/(opening total) of a person (fix r1).</summary>
        public readonly long[] ReinfArmCarry = new long[4];
                public int PendRationLog = NoOrder, PendAssaultLog = NoOrder, PendSallyLog = NoOrder, PendLiftLog = NoOrder;
        public long PendRationIssued, PendAssaultIssued, PendSallyIssued, PendLiftIssued;
        public int PendRationIssuer, PendAssaultIssuer, PendSallyIssuer, PendLiftIssuer;
    }

    private sealed class RaidS
    {
        public RaidSpec Spec = null!;
        public int PeopleAcct, LootAcct;
        public bool Arrived, Resolved;
    }

    private readonly record struct PendingGarrison(int LogIndex, int Issuer, int Kind, int FormationId, long Value, long IssuedMs);

    private readonly CampaignCalendar _cal;
    private readonly WarPulseSchedule _schedule;
    private readonly long _warStartMs, _warEndMs;
    private readonly SiegeClock _clock;
    private readonly SiegeCoefficients _k;
    private readonly LogisticsRates _r;
    private readonly PolityCapability[] _cap;
    private readonly TerrainLogistics _terrain;
    private readonly ShortfallPolicy _policy;
    private readonly ulong _seed;
    private readonly IAssaultResolver _resolver;
    private readonly int _polityCount;
    private readonly WarLedger _ledger = new();
    private readonly int[] _treasury, _homeFood, _homeMunitions, _homeMissiles, _homeTroops, _prisoners, _elsewhere;
    private readonly int _foodEaten, _wagesInKind, _carriageLoss, _waterDrunk, _munitionsSpent, _missilesSpent, _upkeepPaid, _kia, _starved, _thirst,
                         _diseased, _deserted, _equipLost, _nature;
    private readonly Inst[] _inst;
    private readonly Form[] _form;
    private readonly SiegeS[] _sieges;
    private readonly RaidS[] _raids;
    private readonly List<PendingGarrison> _pendingGarrison = new();
    private WarPulse _tp;
    private bool _tpActive;
    private long _tpLastSettled;
    private readonly List<SiegePulseRecord> _pulseRecords = new();
    private readonly List<SiegeEndRecord> _ends = new();
    private readonly List<BattleRecord> _battles = new();
    private readonly List<SiegeLatch> _latches = new();
    private readonly List<SiegeOrderRefusal> _refusals = new();
    private readonly List<CaptureRecord> _captures = new();
    private readonly List<RaidRecord> _raidRecords = new();
    private readonly List<RevoltRecord> _revolts = new();
    private readonly List<SiegeTurnSnapshot> _snapshots = new();
    private long _evaluations;
    /// <summary>Running digest of every siege pulse record, kept whether or not <see cref="RecordPulses"/> keeps the records (fix r2,
    /// S2-V5: the state hash hashed the kept list, so two identical simulations with different recording settings hashed differently).</summary>
    private ulong _pulseDigest;

    /// <param name="rates">Lane E's coefficient table (rations, pay, munitions, desertion, disease); null = its labelled PROPOSAL.</param>
    /// <param name="capabilities">Each polity's war capability, DERIVED from its computed state (lane E <see cref="PolityCapability"/>; Law 4):
    /// Coinage (pay in coin or in kind), Gunpowder (firearms, guns and gunpowder engines may exist), Sanitation (disease mortality),
    /// Magazines and the carriage capabilities (supply line). One entry per polity.</param>
    public SiegeWorld(CampaignCalendar calendar, WarPulseSchedule schedule, long warStartMs, long warEndMs, SiegeClock clock,
                      SiegeCoefficients coefficients, LogisticsRates? rates, PolityCapability[] capabilities, ShortfallPolicy policy, ulong seed,
                      InstallationSpec[] installations, FormationSpec[] formations, SiegeSpec[] sieges, RaidSpec[]? raids = null,
                      long openingMoney = 0, long homeFood = 0, long homeMunitions = 0, long homeMissiles = 0, long homeTroops = 0,
                      IAssaultResolver? resolver = null, TerrainLogistics? terrain = null)
    {
        coefficients.Validate();
        int polityCount = capabilities.Length;
        if (polityCount <= 0 || polityCount > 64) throw new ArgumentOutOfRangeException(nameof(capabilities));
        if (warEndMs <= warStartMs) throw new ArgumentOutOfRangeException(nameof(warEndMs));
        if (clock.Kind == SiegeClockKind.BattlePulse && clock.BattlePulseMs <= 0) throw new ArgumentOutOfRangeException(nameof(clock));
        _cal = calendar; _schedule = schedule; _warStartMs = warStartMs; _warEndMs = warEndMs; _clock = clock; _k = coefficients;
        _r = rates ?? LogisticsRates.Proposal(); _cap = (PolityCapability[])capabilities.Clone(); _terrain = terrain ?? TerrainLogistics.Proposal();
        _policy = policy; _seed = seed; _resolver = resolver ?? FormationAssaultStandIn.Instance; _polityCount = polityCount;
        _treasury = new int[polityCount]; _homeFood = new int[polityCount]; _homeMunitions = new int[polityCount]; _homeMissiles = new int[polityCount];
        _homeTroops = new int[polityCount]; _prisoners = new int[polityCount]; _elsewhere = new int[polityCount];
        for (int p = 0; p < polityCount; p++)
        {
            _treasury[p] = _ledger.Open(Commodity.Money, openingMoney);
            _homeFood[p] = _ledger.Open(Commodity.Grain, homeFood);
            _homeMunitions[p] = _ledger.Open(Commodity.Munitions, homeMunitions);
            _homeMissiles[p] = _ledger.Open(Commodity.Missiles, homeMissiles);
            _homeTroops[p] = _ledger.Open(Commodity.People, homeTroops);
            _prisoners[p] = _ledger.Open(Commodity.People, 0);
            _elsewhere[p] = _ledger.Open(Commodity.People, 0);
        }
        _foodEaten = _ledger.Open(Commodity.Grain, 0);
        _wagesInKind = _ledger.Open(Commodity.Grain, 0);
        _carriageLoss = _ledger.Open(Commodity.Grain, 0);
        _waterDrunk = _ledger.Open(Commodity.Water, 0);
        _munitionsSpent = _ledger.Open(Commodity.Munitions, 0);
        _missilesSpent = _ledger.Open(Commodity.Missiles, 0);
        _upkeepPaid = _ledger.Open(Commodity.Money, 0);
        _kia = _ledger.Open(Commodity.People, 0);
        _starved = _ledger.Open(Commodity.People, 0);
        _thirst = _ledger.Open(Commodity.People, 0);
        _diseased = _ledger.Open(Commodity.People, 0);
        _deserted = _ledger.Open(Commodity.People, 0);
        _equipLost = _ledger.Open(Commodity.Ordnance, 0);
        _nature = _ledger.Open(Commodity.Water, 1L << 60);

        // Accounts are opened in ID order, never in declaration order: the ledger's account numbering (and so every hash and row)
        // must not depend on how the caller listed the specs (tie-dense test).
        installations = SortById(installations, x => x.Id);
        formations = SortById(formations, x => x.Id);
        sieges = SortById(sieges, x => x.Id);
        if (raids is not null) raids = SortById(raids, x => x.Id);
        _inst = new Inst[installations.Length];
        foreach (var s in installations)
        {
            if (s.Id < 0 || s.Id >= installations.Length || _inst[s.Id] is not null) throw new ArgumentException("installation ids must be 0..n-1, each once");
            if (s.Controller < 0 || s.Controller >= polityCount || s.Capacity < 0 || s.Housing <= 0 || s.PopulationPolity < -1 || s.PopulationPolity >= polityCount)
                throw new ArgumentOutOfRangeException(nameof(installations));
            if (s.Guns > 0 && !_cap[s.Controller].Has(WarTech.Gunpowder)) throw new ArgumentException($"installation {s.Id}: fortress guns need the Gunpowder capability");
            var i = new Inst
            {
                Spec = s, Controller = s.Controller,
                Civ = _ledger.Open(Commodity.People, s.Civilians), Granary = _ledger.Open(Commodity.Grain, s.Food),
                Cistern = _ledger.Open(Commodity.Water, s.Water), Magazine = _ledger.Open(Commodity.Munitions, s.Munitions),
                MissileStore = _ledger.Open(Commodity.Missiles, s.Missiles), Guns = _ledger.Open(Commodity.Ordnance, s.Guns),
                Wall = s.Fort == FortKind.None ? Q32.Zero : Q.Permille(s.WallPermille), Works = Q.Permille(s.FieldWorksPermille),
                Loyalty = Q.Permille(s.LoyaltyPermille),
            };
            _inst[s.Id] = i;
        }
        _form = new Form[formations.Length];
        foreach (var f in formations)
        {
            if (f.Id < 0 || f.Id >= formations.Length || _form[f.Id] is not null) throw new ArgumentException("formation ids must be 0..n-1, each once");
            if (f.Owner < 0 || f.Owner >= polityCount || f.Personnel < 0) throw new ArgumentOutOfRangeException(nameof(formations));
            if ((SiegeEquations.IsGunpowder(f.Ranged) || f.Family == FormationFamily.Artillery) && !_cap[f.Owner].Has(WarTech.Gunpowder))
                throw new ArgumentException($"formation {f.Id}: firearms and artillery need the Gunpowder capability");
            var x = new Form { Spec = f, Account = _ledger.Open(Commodity.People, f.Personnel), X = f.X, Y = f.Y };
            if (f.GarrisonedAt >= 0)
            {
                var i = _inst[f.GarrisonedAt];
                if (i.Controller != f.Owner || !Eligible(i.Spec.Kind, f.Family)) throw new ArgumentException("initial garrison is not legal");
                x.State = FormationState.Garrisoned; x.At = f.GarrisonedAt; x.X = i.Spec.X; x.Y = i.Spec.Y;
            }
            _form[f.Id] = x;
        }
        foreach (var i in _inst)
            if (GarrisonPersonnel(i) > i.Spec.Capacity) throw new ArgumentException($"installation {i.Spec.Id}: initial garrison exceeds capacity");
        _sieges = new SiegeS[sieges.Length];
        foreach (var sp in sieges)
        {
            if (sp.Id < 0 || sp.Id >= sieges.Length || _sieges[sp.Id] is not null) throw new ArgumentException("siege ids must be 0..n-1, each once");
            if (sp.Attacker < 0 || sp.Attacker >= polityCount || sp.InstallationId < 0 || sp.InstallationId >= _inst.Length) throw new ArgumentOutOfRangeException(nameof(sieges));
            sp.Force.Validate();
            if (sp.CampCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(sieges), "camp capacity");
            var capA = _cap[sp.Attacker];
            bool powderA = SiegeEquations.IsGunpowder(sp.Force.Ranged) || sp.Force.Engines[(int)EngineClass.Bombard] > 0 || sp.Force.Engines[(int)EngineClass.ModernGun] > 0
                           || (sp.Relief is not null && SiegeEquations.IsGunpowder(sp.Relief.Ranged));
            if (powderA && !capA.Has(WarTech.Gunpowder))
                throw new ArgumentException($"siege {sp.Id}: firearms and gunpowder engines need the besieger's Gunpowder capability");
            var s = new SiegeS { Spec = sp, I = _inst[sp.InstallationId], AttWorks = Q.Permille(sp.AttackerWorksPermille) };
            if (sp.SupplyLine is not null)
            {
                // Lane E's supply line over the whole route (depot to the siege): the besiegers' train carries it.
                var f = sp.Force;
                var train = new ArmyComposition(f.People[0] + f.People[1] + f.People[3], f.People[2], 0, 0, sp.SupplyLine.DraftAnimals,
                                                sp.SupplyLine.Wagons, sp.SupplyLine.Trucks, f.Engines[2] + f.Engines[3], SiegeEquations.IsGunpowder(f.Ranged));
                string? why = train.Validate(capA);
                if (why is not null) throw new ArgumentException($"siege {sp.Id}: supply train: {why}");
                var line = ArmyLogistics.Evaluate(sp.SupplyLine.Route, sp.SupplyLine.Route.Length, SimInstant.MsPerYear, train, capA, _r, _terrain);
                s.HasLine = true; s.LineLoss = line.Loss; s.LineCapacityPerYear = line.CapacityPerPulse;
            }
            for (int a = 0; a < 4; a++)
            {
                s.AttPeople[a] = _ledger.Open(Commodity.People, sp.Force.People[a]);
                s.AttEngines[a] = _ledger.Open(Commodity.Ordnance, sp.Force.Engines[a]);
                s.Relief[a] = _ledger.Open(Commodity.People, sp.Relief is null ? 0 : sp.Relief.People[a]);
            }
            s.AttGranary = _ledger.Open(Commodity.Grain, sp.AttackerFood);
            s.AttMunitions = _ledger.Open(Commodity.Munitions, sp.AttackerMunitions);
            s.AttMissiles = _ledger.Open(Commodity.Missiles, sp.AttackerMissiles);
            _sieges[sp.Id] = s;
        }
        raids ??= Array.Empty<RaidSpec>();
        _raids = new RaidS[raids.Length];
        foreach (var r in raids)
        {
            if (r.Id < 0 || r.Id >= raids.Length || _raids[r.Id] is not null) throw new ArgumentException("raid ids must be 0..n-1, each once");
            if (r.Raider < 0 || r.Raider >= polityCount || r.InstallationId < 0 || r.InstallationId >= _inst.Length || r.People < 0)
                throw new ArgumentOutOfRangeException(nameof(raids));
            if (SiegeEquations.IsGunpowder(r.Ranged) && !_cap[r.Raider].Has(WarTech.Gunpowder)) throw new ArgumentException($"raid {r.Id}: firearms need Gunpowder");
            var x = new RaidS { Spec = r, PeopleAcct = _ledger.Open(Commodity.People, r.People), LootAcct = _ledger.Open(Commodity.Grain, 0) };
            _raids[r.Id] = x;
        }
    }

    /// <summary>A copy ordered by an integer id (insertion sort on the id: stable, no comparer delegate over scores).</summary>
    private static T[] SortById<T>(T[] items, Func<T, int> id)
    {
        var a = (T[])items.Clone();
        for (int i = 1; i < a.Length; i++)
        {
            var x = a[i];
            int j = i - 1;
            while (j >= 0 && id(a[j]) > id(x)) { a[j + 1] = a[j]; j--; }
            a[j + 1] = x;
        }
        return a;
    }

    // ================================================================== public read surface

    public WarLedger Ledger => _ledger;
    public SiegeCoefficients Coefficients => _k;
    public bool RecordPulses { get; set; } = true;
    public long Evaluations => _evaluations;
    public IReadOnlyList<SiegePulseRecord> PulseRecords => _pulseRecords;
    public IReadOnlyList<SiegeEndRecord> Ends => _ends;
    public IReadOnlyList<BattleRecord> Battles => _battles;
    public IReadOnlyList<SiegeLatch> Latches => _latches;
    public IReadOnlyList<SiegeOrderRefusal> Refusals => _refusals;
    public IReadOnlyList<CaptureRecord> Captures => _captures;
    public IReadOnlyList<RaidRecord> RaidRecords => _raidRecords;
    public IReadOnlyList<RevoltRecord> Revolts => _revolts;
    public IReadOnlyList<SiegeTurnSnapshot> TurnSnapshots => _snapshots;
    public int TreasuryOf(int p) => _treasury[p];
    public int HomeFoodOf(int p) => _homeFood[p];
    public int PrisonersOf(int p) => _prisoners[p];
    public int ElsewhereOf(int p) => _elsewhere[p];
    public int HomeTroopsOf(int p) => _homeTroops[p];
    public int UpkeepSink => _upkeepPaid;
    public int WagesInKindSink => _wagesInKind;
    public int CarriageLossSink => _carriageLoss;
    public int MunitionsSpentSink => _munitionsSpent;
    public int MissilesSpentSink => _missilesSpent;
    public int HomeMunitionsOf(int p) => _homeMunitions[p];
    public LogisticsRates Rates => _r;
    public long ArrearsSinceOf(int inst) => _inst[inst].ArrearsSinceMs;
    public long MunitionsOf(int inst) => _ledger.Balance(_inst[inst].Magazine);
    public long MissilesOf(int inst) => _ledger.Balance(_inst[inst].MissileStore);
    public long AttackerFoodOf(int siege) => _ledger.Balance(_sieges[siege].AttGranary);
    public long AttackerMunitionsOf(int siege) => _ledger.Balance(_sieges[siege].AttMunitions);
    public (bool HasLine, ExactFraction Loss, long CapacityPerYear, long Shipped, long Delivered) SupplyLineOf(int siege)
    { var x = _sieges[siege]; return (x.HasLine, x.LineLoss, x.LineCapacityPerYear, x.Shipped, x.Delivered); }
    /// <summary>Full ration of a soldier (garrison, besieger) per sim-year, gm: lane E's base ration plus the field extra (guard,
    /// repair, entrenching). A civilian eats lane E's base ration.</summary>
    public long SoldierRation => checked(_r.RationBase + _r.RationFieldExtra);
    public int KilledSink => _kia;
    public int StarvedSink => _starved;
    public int DiseaseSink => _diseased;
    public int DesertedSink => _deserted;
    public int FoodEatenSink => _foodEaten;
    public int FormationAccount(int f) => _form[f].Account;
    public long Personnel(int f) => _ledger.Balance(_form[f].Account);
    public FormationState StateOf(int f) => _form[f].State;
    public int LocationOf(int f) => _form[f].At;
    public (long X, long Y) PositionOf(int f) => (_form[f].X, _form[f].Y);
    public int ControllerOf(int inst) => _inst[inst].Controller;
    public long Civilians(int inst) => _ledger.Balance(_inst[inst].Civ);
    public long FoodOf(int inst) => _ledger.Balance(_inst[inst].Granary);
    public int GranaryAccount(int inst) => _inst[inst].Granary;
    public int CivilianAccount(int inst) => _inst[inst].Civ;
    public Q32 WallOf(int inst) => _inst[inst].Wall;
    public Q32 UnrestOf(int inst) => _inst[inst].Unrest;
    public bool IsOccupied(int inst) => _inst[inst].Controller != PopulationOf(_inst[inst]);
    public long UpkeepArrearsOf(int inst) => _inst[inst].UpkeepArrears;
    public long GarrisonOf(int inst) => GarrisonPersonnel(_inst[inst]);
    public bool IsActive(int siege) => _sieges[siege].Active;
    public SiegeOutcome OutcomeOf(int siege) => _sieges[siege].Outcome;
    public long EndOf(int siege) => _sieges[siege].EndMs;
    public long CapOf(int siege) => _sieges[siege].CapMs;
    public Q32 HealthOf(int siege) => _sieges[siege].HealthD;
    public Q32 AttackerHealthOf(int siege) => _sieges[siege].HealthA;
    public Q32 RationOf(int siege) => _sieges[siege].Ration;
    public Q32 MoraleOf(int siege) => _sieges[siege].Morale;
    public long PulseIndexOf(int siege) => _sieges[siege].PulseIndex;
    public Q32 MiningOf(int siege) => _sieges[siege].Mining;
    public int MinesOf(int siege) => _sieges[siege].Mines;
    public Q32 ApproachOf(int siege) => _sieges[siege].Approach;
    public Q32 AttackerMoraleOf(int siege) => _sieges[siege].MoraleA;
    public Q32 DiseaseOf(int siege) => _sieges[siege].Dis;
    public long WaterOf(int inst) => _ledger.Balance(_inst[inst].Cistern);
    public int ThirstSink => _thirst;
    public int EquipmentLostSink => _equipLost;
    public long CurrentPulseStart(int siege) => _sieges[siege].PulseStart;
    public long AttackerPeople(int siege) { long t = 0; foreach (var a in _sieges[siege].AttPeople) t += _ledger.Balance(a); return t; }
    public long AttackerEngines(int siege, EngineClass e) => _ledger.Balance(_sieges[siege].AttEngines[(int)e]);
    public SiegeRegime RegimeOf(int siege) => SiegeEquations.RegimeOf(EngineCounts(_sieges[siege]));
    public long TheatrePulseStart => _tp.StartMs;
    public long TheatrePulseIndex => _tp.Index;
    public int PendingGarrisonOrders => _pendingGarrison.Count;
    public bool TheatreActive => _tpActive;

    /// <summary>The garrison aggregate as the siege equations see it (only Garrisoned formations count: one capability, one state).</summary>
    public DefenderForce DefenderOf(int inst) => Defender(_inst[inst]);

    /// <summary>Capacity and eligibility (DIR5 §2.3; C30). PLACEHOLDER table: which families each installation kind may host.</summary>
    public static bool Eligible(InstallationKind kind, FormationFamily f) => kind switch
    {
        InstallationKind.City => f is FormationFamily.Infantry or FormationFamily.Ranged or FormationFamily.Cavalry or FormationFamily.Engineers
                                    or FormationFamily.Artillery or FormationFamily.AirDefence,
        InstallationKind.Fort => f is FormationFamily.Infantry or FormationFamily.Ranged or FormationFamily.Engineers or FormationFamily.Artillery
                                    or FormationFamily.AirDefence,
        InstallationKind.Harbour => f is FormationFamily.Infantry or FormationFamily.Ranged or FormationFamily.Naval or FormationFamily.AirDefence
                                    or FormationFamily.Artillery,
        InstallationKind.Airport => f is FormationFamily.Air or FormationFamily.AirDefence or FormationFamily.Infantry,
        InstallationKind.Depot => f is FormationFamily.Infantry or FormationFamily.Ranged,
        _ => false,
    };

    // ================================================================== setup

    /// <summary>Schedule the theatre's first pulse, the first turn start at or after now, every siege start, relief and raid.</summary>
    public void Begin(EventScheduler s)
    {
        if (s.NowMs > _warStartMs) throw new InvalidOperationException("the scheduler must start at or before the war start");
        var p = _schedule.First(_warStartMs);
        s.Schedule(p.StartMs, EventClass.System, 3, 0, SiegeKinds.TheatrePulseStart, p.Index, p.StartMs, p.EndMs);
        long now = s.NowMs;
        if (now < _cal.CampaignEnd.Ms)
        {
            long turn = _cal.TurnOf(new SimInstant(now));
            if (_cal.TurnStart(turn).Ms < now) turn++;
            if (turn < _cal.TotalTurns) s.Schedule(_cal.TurnStart(turn).Ms, EventClass.System, 2, 0, SiegeKinds.TurnStart, turn);
        }
        foreach (var x in _sieges)
        {
            if (x.Spec.StartMs < _warStartMs || x.Spec.StartMs >= _warEndMs) throw new ArgumentException("a siege must start inside the war");
            s.Schedule(x.Spec.StartMs, EventClass.System, 5, x.Spec.Id, SiegeKinds.SiegePulseStart);
            if (x.Spec.Relief is not null)
                s.Schedule(Math.Max(x.Spec.Relief.ArrivalMs, x.Spec.StartMs), EventClass.System, 4, x.Spec.Id, SiegeKinds.ReliefArrival);
        }
        foreach (var r in _raids)
            s.Schedule(r.Spec.ArrivalMs, EventClass.System, 2, RaidSourceBase + r.Spec.Id, SiegeKinds.RaidArrival);
    }

    /// <summary>Test device: open a decision window at atMs.</summary>
    public static void ScheduleProbe(EventScheduler s, long atMs, long tag = 0)
        => s.Schedule(atMs, EventClass.Significant, 0, tag, SiegeKinds.Probe);

    public bool IsLive(EntityRef entity) => true;

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        long now = ev.Key.TimeMs;
        if (ev.Key.Class == (byte)EventClass.PlayerOrder) { ExecuteOrder(in ev, now); return; }
        switch (ev.Kind)
        {
            case SiegeKinds.TheatrePulseEnd: OnTheatrePulseEnd(s, now); break;
            case SiegeKinds.SiegePulseEnd: OnSiegePulseEnd(_sieges[(int)ev.Key.SourceId], s, now); break;
            case SiegeKinds.TurnStart: OnTurnStart(in ev, s, now); break;
            case SiegeKinds.TheatrePulseStart: OnTheatrePulseStart(in ev, s, now); break;
            case SiegeKinds.ReliefArrival: _sieges[(int)ev.Key.SourceId].ReliefArrived = true; break;
            case SiegeKinds.SiegePulseStart: OnSiegePulseStart(_sieges[(int)ev.Key.SourceId], s, now); break;
            case SiegeKinds.RaidArrival: OnRaidArrival(_raids[(int)(ev.Key.SourceId - RaidSourceBase)], now); break;
            case SiegeKinds.SiegeEnded:
            case SiegeKinds.AssaultResolved:
            case SiegeKinds.Revolt:
            case SiegeKinds.Probe:
                break;
            default: throw new InvalidOperationException($"unknown kind {ev.Kind}");
        }
    }

    // ================================================================== campaign turns (sub-stepping)

    private void OnTurnStart(in ScheduledEvent ev, EventScheduler s, long now)
    {
        long turn = ev.A;
        SettleTheatre(now);   // pay before the sieges' flows (S2-V6)
        foreach (var x in _sieges) if (x.Active) Settle(x, now);
        _snapshots.Add(new SiegeTurnSnapshot(turn, now, _ledger.Snapshot()));
        if (turn + 1 < _cal.TotalTurns) s.Schedule(_cal.TurnStart(turn + 1).Ms, EventClass.System, 2, 0, SiegeKinds.TurnStart, turn + 1);
    }

    // ================================================================== theatre clock: garrisons

    private void OnTheatrePulseStart(in ScheduledEvent ev, EventScheduler s, long now)
    {
        _tp = new WarPulse(ev.A, ev.B, ev.C);
        if (_tp.StartMs != now) throw new InvalidOperationException("theatre pulse out of order");
        _tpActive = true;
        _tpLastSettled = now;
        foreach (var i in _inst) { i.UpkeepDue = 0; i.UpkeepPaid = 0; i.TheatreLossSettledMs = now; }
        // Garrison orders issued at or before now take effect now, in log order.
        var keep = new List<PendingGarrison>();
        foreach (var o in _pendingGarrison)
        {
            if (o.IssuedMs <= now) ApplyGarrison(o, now, _tp.Index);
            else keep.Add(o);
        }
        _pendingGarrison.Clear();
        _pendingGarrison.AddRange(keep);
        foreach (var r in _raids) if (r.Arrived && !r.Resolved) ResolveRaid(r, now);
        s.Schedule(_tp.EndMs, EventClass.System, 0, 0, SiegeKinds.TheatrePulseEnd);
    }

    private void OnTheatrePulseEnd(EventScheduler s, long now)
    {
        if (!_tpActive || _tp.EndMs != now) throw new InvalidOperationException("theatre pulse end out of order");
        SettleTheatre(now);
        foreach (var x in _sieges) if (x.Active) Settle(x, now); // people change below: settle linear flows first (pay first: S2-V6)
        foreach (var i in _inst)
        {
            i.UnpaidShare = i.UpkeepDue <= 0 ? Q32.Zero : Q32.FromRatio(i.UpkeepDue - i.UpkeepPaid, i.UpkeepDue);
            SettleTheatreLosses(i, now, s);
        }
        _tpActive = false;
        if (_tp.EndMs < _warEndMs && _schedule.HasNext(_tp))
        {
            var n = _schedule.Next(_tp);
            s.Schedule(n.StartMs, EventClass.System, 3, 0, SiegeKinds.TheatrePulseStart, n.Index, n.StartMs, n.EndMs);
        }
    }

    /// <summary>
    /// The theatre clock's per-installation losses over [TheatreLossSettledMs, upTo), at the state that held over that span (fix r2,
    /// S2-V2): (1) desertion of a garrison that is NOT under siege (a besieged garrison deserts in the siege evaluation): lane E's rate
    /// with full morale (a garrison's morale is modelled only under siege) and no hunger: base + the age of its pay arrears; (2) the
    /// occupation unrest step. Called at every theatre pulse end and at every change of that state inside a pulse: a siege's start
    /// (before the installation becomes besieged) and its end (before it stops being besieged and before control passes). Before fix
    /// r2 both were applied once, at the theatre pulse end, over the whole pulse at the state of that instant: a siege starting
    /// mid-pulse skipped the desertion of the part before it, a siege ending mid-pulse with the garrison staying charged its besieged
    /// part a second time, and a capture counted the whole pulse as occupied.
    /// </summary>
    private void SettleTheatreLosses(Inst i, long upTo, EventScheduler? s)
    {
        if (!_tpActive) return;
        long dt = upTo - i.TheatreLossSettledMs;
        if (dt <= 0) return;
        i.TheatreLossSettledMs = upTo;
        if (i.ActiveSiege < 0)
        {
            long des = TroopLossRates.DesertionPermilleYear(_r, 1_000_000, ArrearsMonths(i, upTo), 0, false);
            foreach (var f in _form)
                if (f.State == FormationState.Garrisoned && f.At == i.Spec.Id) LoseAtRate(f.Account, des, dt, f.Carry, FTheatreDesert, _deserted);
        }
        if (i.Spec.GrievancePermillePerYear > 0 && !i.Revolted)
        {
            // Occupation (fix r1, S-V7): the grievance (an input: the population's disposition toward a foreign ruler) accrues only
            // while the installation is held by a polity other than its population's own; under its own polity the unrest decays.
            bool occupied = i.Controller != PopulationOf(i);
            long civ = _ledger.Balance(i.Civ);
            Q32 sup = civ <= 0 ? Q32.One : Q.Min(Q32.One, Q.Ratio(checked(GarrisonPersonnel(i) * 1000), checked(civ * _k.PolicePer1000)));
            Q32 g = occupied ? Q.OverDt(i.Spec.GrievancePermillePerYear, dt).Mul(Q.Sub(Q32.One, sup)) : Q32.Zero;
            Q32 d = Q.OverDt(_k.UnrestDecayPermillePerYear, dt).Mul(occupied ? sup : Q32.One);
            i.Unrest = Q.Clamp01(new Q32(FixedMath.MulDivFloor(checked(i.Unrest.Raw + g.Raw), Q32.OneRaw, checked(Q32.OneRaw + d.Raw))));
            if (i.Unrest.Raw >= Q32.OneRaw)
            {
                i.Revolted = true;
                _revolts.Add(new RevoltRecord(i.Spec.Id, upTo));
                if (s is not null && s.CurrentKey.Class < (byte)EventClass.Significant)
                    s.Schedule(upTo, EventClass.Significant, 0, i.Spec.Id, SiegeKinds.Revolt);
            }
        }
    }

    /// <summary>The polity the installation's population belongs to (its spec's population polity, by default its first controller).</summary>
    private static int PopulationOf(Inst i) => i.Spec.PopulationPolity >= 0 ? i.Spec.PopulationPolity : i.Spec.Controller;

    /// <summary>
    /// Garrison pay, linear and exact (lane E's pay table, LogisticsRates): Σ garrisoned personnel × pay of its arm per sim-year,
    /// paid by the controller in coin from its treasury when it holds Coinage, otherwise in kind (grain, gm) from its home granary
    /// (lane E: soldiers paid in kind without coinage). What cannot be paid runs up arrears, whose clock (lane E's) starts at the
    /// interpolated instant the source ran dry. NOT BUILT: back pay — this world has no revenue, so a source that ran dry never
    /// refills; lane E's back pay applies once the campaign treasury has income (integration).
    /// </summary>
    private void SettleTheatre(long upTo)
    {
        if (!_tpActive) return;
        long dt = upTo - _tpLastSettled;
        if (dt <= 0) return;
        foreach (var i in _inst)
        {
            bool coin = _cap[i.Controller].Has(WarTech.Coinage);
            long rate = 0;
            foreach (var f in _form)
                if (f.State == FormationState.Garrisoned && f.At == i.Spec.Id) rate = checked(rate + _ledger.Balance(f.Account) * PayPerYear(f.Spec.Family, coin));
            long due = RateIntegrator.IntegrateExact(rate, dt, SimInstant.MsPerYear, ref i.UpkeepCarry);
            int src = coin ? _treasury[i.Controller] : _homeFood[i.Controller];
            int sink = coin ? _upkeepPaid : _wagesInKind;
            long paid = Math.Min(due, _ledger.Balance(src));
            if (paid > 0) _ledger.Transfer(src, sink, paid);
            i.UpkeepDue += due; i.UpkeepPaid += paid;
            if (due > paid)
            {
                if (i.UpkeepArrears == 0) i.ArrearsSinceMs = checked(_tpLastSettled + (long)((Int128)dt * paid / due));
                i.UpkeepArrears = checked(i.UpkeepArrears + due - paid);
            }
        }
        _tpLastSettled = upTo;
    }

    /// <summary>Lane E's pay per man-year by arm: coin in soldier-months (Coinage), else wages in kind in gm of grain.</summary>
    private long PayPerYear(FormationFamily f, bool coin) => f switch
    {
        FormationFamily.Cavalry => coin ? _r.PayMounted : _r.WageInKindMounted,
        FormationFamily.Artillery or FormationFamily.Air => coin ? _r.PayGunCrew : _r.WageInKindGunCrew,
        FormationFamily.Naval => coin ? _r.PaySeaman : _r.WageInKindSeaman,
        _ => coin ? _r.PayFoot : _r.WageInKindFoot,
    };

    /// <summary>Age of the oldest unpaid pay in whole 30-day months (lane E's arrears clock).</summary>
    private static long ArrearsMonths(Inst i, long now)
        => i.UpkeepArrears > 0 && i.ArrearsSinceMs >= 0 ? Math.Max(0, now - i.ArrearsSinceMs) / (30 * SimInstant.MsPerDay) : 0;

    private void OnRaidArrival(RaidS r, long now)
    {
        r.Arrived = true;
        // WA1: a raid arriving exactly at a theatre pulse start acts in that pulse (the start already executed at this instant).
        if (_tpActive && _tp.StartMs == now) ResolveRaid(r, now);
    }

    private void ResolveRaid(RaidS r, long now)
    {
        r.Resolved = true;
        var i = _inst[r.Spec.InstallationId];
        long raiders = _ledger.Balance(r.PeopleAcct);
        if (i.ActiveSiege >= 0 || i.Controller == r.Spec.Raider || raiders <= 0)
        {
            _raidRecords.Add(new RaidRecord(r.Spec.Id, i.Spec.Id, now, false, 0, 0, 0));
            return;
        }
        SettleTheatre(now);   // pay before the sieges' flows (S2-V6)
        foreach (var x in _sieges) if (x.Active) Settle(x, now);
        var people = new long[4];
        people[0] = raiders;
        long attack = SiegeEquations.FieldAttackPower(_k, people, r.Spec.Ranged);
        var d = Defender(i);
        Q32 fort = SiegeEquations.FortFactor(_k, i.Spec.Fort, i.Wall, i.Works);
        // An ungarrisoned settlement defends with its civilians as militia at 1/20 of a soldier (PLACEHOLDER).
        long defence = d.Fighting + d.GunsManned > 0
            ? SiegeEquations.Capability(_k, d, Q32.One, Q32.One, Q32.Zero, Q32.One, fort)
            : fort.Scale(checked(_ledger.Balance(i.Civ) * 50));
        var res = _resolver.Resolve(new AssaultInputs(attack, defence, _seed, RaidSourceBase + r.Spec.Id, now, 4), _k);
        long lossR = r.Spec.People <= 0 ? 0 : res.AttackerLossShare.Scale(raiders);
        _ledger.Transfer(r.PeopleAcct, _kia, lossR);
        long lossG = 0;
        foreach (var f in _form)
            if (f.State == FormationState.Garrisoned && f.At == i.Spec.Id) lossG += Lose(f.Account, res.DefenderLossShare, ref f.Carry[FAssault], _kia);
        long pillaged = 0;
        if (res.Success)
        {
            pillaged = FixedMath.MulDivFloor(_ledger.Balance(i.Granary), r.Spec.PillagePermille, 1000);
            _ledger.Transfer(i.Granary, r.LootAcct, pillaged);
        }
        _battles.Add(new BattleRecord((int)(RaidSourceBase + r.Spec.Id), 4, now, attack, defence, res.Odds.Raw, res.Fortune.Raw, res.Success, lossR, lossG));
        _raidRecords.Add(new RaidRecord(r.Spec.Id, i.Spec.Id, now, res.Success, pillaged, lossR, lossG));
    }

    public long RaiderFood(int raid) => _ledger.Balance(_raids[raid].LootAcct);
    public long Raiders(int raid) => _ledger.Balance(_raids[raid].PeopleAcct);

    // ================================================================== garrison orders

    private void ApplyGarrison(in PendingGarrison o, long now, long pulseIndex)
    {
        if (o.FormationId < 0 || o.FormationId >= _form.Length) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.BadTarget); return; }
        var f = _form[o.FormationId];
        if (f.Spec.Owner != o.Issuer) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.NotOwner); return; }
        switch (o.Kind)
        {
            case SiegeKinds.OrderGarrisonAssign:
            {
                if (o.Value < 0 || o.Value >= _inst.Length) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.BadTarget); return; }
                var i = _inst[(int)o.Value];
                int r = CheckEntry(f, i, GarrisonPersonnel(i) + _ledger.Balance(f.Account));
                if (r != 0) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, r); return; }
                SettleTheatre(now);
                f.State = FormationState.Garrisoned; f.At = i.Spec.Id;
                _latches.Add(new SiegeLatch(o.LogIndex, o.Issuer, o.Kind, f.Spec.Id, o.IssuedMs, now, pulseIndex, o.Value));
                return;
            }
            case SiegeKinds.OrderGarrisonWithdraw:
            {
                if (f.State != FormationState.Garrisoned) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.NotGarrisoned); return; }
                var i = _inst[f.At];
                if (i.ActiveSiege >= 0) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.Invested); return; }
                SettleTheatre(now);
                f.State = FormationState.Mobile; f.At = -1; f.X = i.Spec.X; f.Y = i.Spec.Y;
                _latches.Add(new SiegeLatch(o.LogIndex, o.Issuer, o.Kind, f.Spec.Id, o.IssuedMs, now, pulseIndex, i.Spec.Id));
                return;
            }
            case SiegeKinds.OrderGarrisonReplace:
            {
                if (f.State != FormationState.Garrisoned) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.NotGarrisoned); return; }
                if (o.Value < 0 || o.Value >= _form.Length || o.Value == f.Spec.Id) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.BadTarget); return; }
                var g = _form[(int)o.Value];
                if (g.Spec.Owner != o.Issuer) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.NotOwner); return; }
                var i = _inst[f.At];
                int r = CheckEntry(g, i, GarrisonPersonnel(i) - _ledger.Balance(f.Account) + _ledger.Balance(g.Account));
                if (r != 0) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, r); return; }
                SettleTheatre(now);
                f.State = FormationState.Mobile; f.At = -1; f.X = i.Spec.X; f.Y = i.Spec.Y;
                g.State = FormationState.Garrisoned; g.At = i.Spec.Id;
                _latches.Add(new SiegeLatch(o.LogIndex, o.Issuer, o.Kind, f.Spec.Id, o.IssuedMs, now, pulseIndex, o.Value));
                return;
            }
            case SiegeKinds.OrderGarrisonReinforce:
            {
                if (f.State != FormationState.Garrisoned) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.NotGarrisoned); return; }
                long fromId = o.Value / SiegeKinds.ReinforceStride, n = o.Value % SiegeKinds.ReinforceStride;
                if (fromId < 0 || fromId >= _form.Length || fromId == f.Spec.Id || n <= 0) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.BadValue); return; }
                var g = _form[(int)fromId];
                var i = _inst[f.At];
                if (g.Spec.Owner != o.Issuer) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.NotOwner); return; }
                if (g.State != FormationState.Mobile) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.AlreadyGarrisoned); return; }
                if (g.Spec.Family != f.Spec.Family) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.Eligibility); return; }
                if (i.ActiveSiege >= 0) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.Invested); return; }
                if (g.X != i.Spec.X || g.Y != i.Spec.Y) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.NotPresent); return; }
                if (n > _ledger.Balance(g.Account)) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.BadValue); return; }
                if (GarrisonPersonnel(i) + n > i.Spec.Capacity) { Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.Capacity); return; }
                SettleTheatre(now);
                _ledger.Transfer(g.Account, f.Account, n);
                _latches.Add(new SiegeLatch(o.LogIndex, o.Issuer, o.Kind, f.Spec.Id, o.IssuedMs, now, pulseIndex, o.Value));
                return;
            }
            default: Refuse(o.LogIndex, o.Issuer, o.Kind, now, SiegeRefusal.UnknownKind); return;
        }
    }

    /// <summary>Entry checks for formation f into installation i with the resulting garrison size: 0 = legal, else a refusal code.</summary>
    private int CheckEntry(Form f, Inst i, long resultingPersonnel)
    {
        if (f.State == FormationState.Garrisoned) return SiegeRefusal.AlreadyGarrisoned;
        if (f.State != FormationState.Mobile) return SiegeRefusal.BadTarget;
        if (i.Controller != f.Spec.Owner) return SiegeRefusal.NotController;
        if (i.ActiveSiege >= 0) return SiegeRefusal.Invested;
        if (!Eligible(i.Spec.Kind, f.Spec.Family)) return SiegeRefusal.Eligibility;
        if (f.X != i.Spec.X || f.Y != i.Spec.Y) return SiegeRefusal.NotPresent;
        if (resultingPersonnel > i.Spec.Capacity) return SiegeRefusal.Capacity;
        return 0;
    }

    private long GarrisonPersonnel(Inst i)
    {
        long t = 0;
        foreach (var f in _form) if (f.State == FormationState.Garrisoned && f.At == i.Spec.Id) t = checked(t + _ledger.Balance(f.Account));
        return t;
    }

    private DefenderForce Defender(Inst i)
    {
        long melee = 0, cav = 0, eng = 0, bow = 0, bv = 0, bl = 0, gun = 0, gv = 0, gl = 0, crews = 0;
        foreach (var f in _form)
        {
            if (f.State != FormationState.Garrisoned || f.At != i.Spec.Id) continue;
            long n = _ledger.Balance(f.Account);
            switch (f.Spec.Family)
            {
                case FormationFamily.Infantry: melee += n; break;
                case FormationFamily.Cavalry: cav += n; break;
                case FormationFamily.Engineers: eng += n; break;
                case FormationFamily.Artillery: crews += n; break;
                case FormationFamily.Ranged:
                case FormationFamily.AirDefence:
                    long v = checked(n * _k.RangedKindFactorPermille[(int)f.Spec.Ranged]), l = checked(n * _k.FireLethalityPermillePerYear[(int)f.Spec.Ranged]);
                    if (SiegeEquations.IsGunpowder(f.Spec.Ranged)) { gun += n; gv = checked(gv + v); gl = checked(gl + l); }
                    else { bow += n; bv = checked(bv + v); bl = checked(bl + l); }
                    break;
                default: break; // naval and air crews eat but do not fight in a siege
            }
        }
        long guns = Math.Min(_ledger.Balance(i.Guns), i.Spec.GunCrew <= 0 ? 0 : crews / i.Spec.GunCrew);
        return new DefenderForce(melee, cav, eng, bow, bv, bl, gun, gv, gl, guns);
    }

    /// <summary>Munitions (kg per sim-year) the defenders fire: lane E's kg per firearm and per gun per combat day × the siege's
    /// daily share of a combat day.</summary>
    private long DefenderMunitionsPerYear(in DefenderForce d)
        => FixedMath.MulDivFloor(checked(d.Gunmen * _r.MunitionsPerFirearmCombatDay + d.GunsManned * _r.MunitionsPerGunCombatDay),
                                 checked(SimInstant.YearDays * _k.SiegeFireCombatDayPermille), 1000);

    /// <summary>Munitions (kg per sim-year) the besiegers fire: firearms at the siege share, gunpowder engines at the bombardment share.</summary>
    private long AttackerMunitionsPerYear(SiegeS x)
    {
        long shooters = SiegeEquations.IsGunpowder(x.Spec.Force.Ranged) ? _ledger.Balance(x.AttPeople[(int)Arm.Ranged]) : 0;
        long engines = checked(_ledger.Balance(x.AttEngines[(int)EngineClass.Bombard]) + _ledger.Balance(x.AttEngines[(int)EngineClass.ModernGun]));
        return checked(FixedMath.MulDivFloor(checked(shooters * _r.MunitionsPerFirearmCombatDay), checked(SimInstant.YearDays * _k.SiegeFireCombatDayPermille), 1000)
                       + FixedMath.MulDivFloor(checked(engines * _r.MunitionsPerGunCombatDay), checked(SimInstant.YearDays * _k.BombardmentCombatDayPermille), 1000));
    }

    private long AttackerMissilesPerYear(SiegeS x)
        => SiegeEquations.IsGunpowder(x.Spec.Force.Ranged) || x.Spec.Force.Ranged == RangedKind.None ? 0
           : checked(_ledger.Balance(x.AttPeople[(int)Arm.Ranged]) * _k.MissilesPerShooterYear);

    // ================================================================== sieges: pulse lifecycle

    private void OnSiegePulseStart(SiegeS x, EventScheduler s, long now)
    {
        if (!x.Begun) BeginSiege(x, now, s);
        if (!x.Active) return;
        x.PulseIndex++;
        x.PulseStart = now;
        x.DueG = x.DrawG = x.DueC = x.DrawC = x.DueWG = x.DrawWG = x.DueWC = x.DrawWC = x.DueMis = x.DrawMis = x.DueMun = x.DrawMun = 0;
        x.DueA = x.DrawA = x.DueMisA = x.DrawMisA = x.DueMunA = x.DrawMunA = 0;
        LatchSiegeOrders(x, now);
        RunDoctrine(x, now);
        ResolveActions(x, now, s);
        if (!x.Active) return;
        long nominal;
        if (_clock.Kind == SiegeClockKind.BattlePulse) nominal = checked(now + _clock.BattlePulseMs);
        else
        {
            while (x.Cursor.EndMs <= now) x.Cursor = _schedule.Next(x.Cursor);
            nominal = x.Cursor.EndMs;
        }
        x.PulseEnd = Math.Min(nominal, x.CapMs);
        if (x.Spec.CorridorFromMs > now && x.Spec.CorridorFromMs < x.PulseEnd) x.PulseEnd = x.Spec.CorridorFromMs;   // corridor opens: a pulse boundary
        s.Schedule(x.PulseEnd, EventClass.System, 1, x.Spec.Id, SiegeKinds.SiegePulseEnd, x.PulseIndex);
    }

    private void BeginSiege(SiegeS x, long now, EventScheduler s)
    {
        x.Begun = true;
        var i = x.I;
        if (i.ActiveSiege >= 0 || i.Controller == x.Spec.Attacker) throw new InvalidOperationException($"siege {x.Spec.Id}: installation already besieged or own");
        // The part of the theatre pulse before the siege: pay up to now at the head count that held (S-V2), then that part's theatre
        // desertion and unrest (fix r2, S2-V2); from now on the garrison deserts in the siege evaluation.
        SettleTheatre(now);
        SettleTheatreLosses(i, now, s);
        x.Active = true;
        x.Defender = i.Controller;
        i.ActiveSiege = x.Spec.Id;
        x.StartTurnLengthMs = _cal.TurnLengthMs(_cal.TurnOf(new SimInstant(now)));
        x.CapMs = checked(now + 3 * x.StartTurnLengthMs);
        x.LastSettled = now;
        x.Morale = Q.Permille(x.Spec.InitialMoralePermille);
        x.MoraleA = Q.Permille(x.Spec.AttackerMoralePermille);
        x.Dis = Q.Permille(_k.DiseaseFloorPermille);
        x.DisA = Q.Permille(_k.DiseaseFloorPermille);
        x.Approach = x.Spec.ApproachWorkEngineerYears > 0 ? Q32.Zero : Q32.One;
        var d = Defender(i);
        x.G0 = GarrisonPersonnel(i);
        x.C0 = _ledger.Balance(i.Civ);
        x.A0 = AttackerTotal(x);
        x.LabourRef = Math.Max(1, checked(x.G0 + _k.CivilianLabourPermille * x.C0 / 1000));
        // The capability reference excludes the fortification: walls enter battles and the morale odds, not siege health twice.
        x.DRef = Math.Max(1, SiegeEquations.Capability(_k, d, Q32.One, Q32.One, Q32.Zero, Q32.One, Q32.One));
        x.ARef = Math.Max(1, SiegeEquations.AssaultPower(_k, AttackerCounts(x), x.Spec.Force.Ranged, EngineCounts(x), true, Q32.One, Q32.One, Q32.Zero, Q32.One));
        if (_clock.Kind == SiegeClockKind.WarPulse)
        {
            var c = _schedule.First(_warStartMs);
            while (c.EndMs <= now) c = _schedule.Next(c);
            x.Cursor = c;
        }
    }

    private void OnSiegePulseEnd(SiegeS x, EventScheduler s, long now)
    {
        if (!x.Active || x.PulseEnd != now) return; // ended at a pulse start or by an order: this end is void
        SettleTheatre(now);   // garrison pay up to now at the head count that held over it: Evaluate changes the garrison (S-V2); pay first (S2-V6)
        Settle(x, now);
        Evaluate(x, now, s);
        if (!x.Active) { ScheduleEnd(x, s); return; }
        if (now >= x.CapMs) { Adjudicate(x, now, s); ScheduleEnd(x, s); return; }
        s.Schedule(now, EventClass.System, 5, x.Spec.Id, SiegeKinds.SiegePulseStart);
    }

    private void ScheduleEnd(SiegeS x, EventScheduler s)
    {
        if (s.CurrentKey.Class < (byte)EventClass.Significant)
            s.Schedule(x.EndMs, EventClass.Significant, 0, x.Spec.Id, SiegeKinds.SiegeEnded, (long)x.Outcome);
    }

    // ================================================================== linear flows

    /// <summary>Settle every linear flow of siege x over [LastSettled, upTo): exact integer totals with carries.</summary>
    private void Settle(SiegeS x, long upTo)
    {
        long dt = upTo - x.LastSettled;
        if (dt <= 0) return;
        var i = x.I;
        long g = GarrisonPersonnel(i), c = _ledger.Balance(i.Civ), a = AttackerTotal(x);
        long y = SimInstant.MsPerYear;
        // inflows first (the corridor only once it is open; settlement is split at CorridorFromMs, see OnSiegePulseStart)
        bool corridor = x.LastSettled >= x.Spec.CorridorFromMs;
        Move(_homeFood[x.Defender], i.Granary, RateIntegrator.IntegrateExact(corridor ? x.Spec.CorridorFoodPerYear : 0, dt, y, ref x.CCorrFood));
        Move(_homeMunitions[x.Defender], i.Magazine, RateIntegrator.IntegrateExact(corridor ? x.Spec.CorridorMunitionsPerYear : 0, dt, y, ref x.CCorrMun));
        Move(_homeMissiles[x.Defender], i.MissileStore, RateIntegrator.IntegrateExact(corridor ? x.Spec.CorridorMissilesPerYear : 0, dt, y, ref x.CCorrMis));
        if (!x.Spec.WaterCut) Move(_nature, i.Cistern, RateIntegrator.IntegrateExact(i.Spec.WaterInflowPerYear, dt, y, ref x.CWaterIn));
        SupplyBesiegers(x, a, dt);
        // food at the current ration (lane E rations: soldiers base + field extra, civilians base), shared under shortage by the policy
        long dueG = RateIntegrator.IntegrateExact(x.Ration.Scale(checked(g * SoldierRation)), dt, y, ref x.CFoodG);
        long dueC = RateIntegrator.IntegrateExact(x.Ration.Scale(checked(c * _r.RationBase)), dt, y, ref x.CFoodC);
        var (fg, fc) = Share(_ledger.Balance(i.Granary), dueG, dueC);
        _ledger.Transfer(i.Granary, _foodEaten, checked(fg + fc));
        x.DueG += dueG; x.DrawG += fg; x.DueC += dueC; x.DrawC += fc;
        // water at max(ration, physiological minimum)
        Q32 rw = Q.Max(x.Ration, Q.Permille(_k.WaterMinRationPermille));
        long dueWG = RateIntegrator.IntegrateExact(rw.Scale(checked(g * _k.WaterPerPersonYear)), dt, y, ref x.CWaterG);
        long dueWC = RateIntegrator.IntegrateExact(rw.Scale(checked(c * _k.WaterPerPersonYear)), dt, y, ref x.CWaterC);
        var (wg, wc) = Share(_ledger.Balance(i.Cistern), dueWG, dueWC);
        _ledger.Transfer(i.Cistern, _waterDrunk, checked(wg + wc));
        x.DueWG += dueWG; x.DrawWG += wg; x.DueWC += dueWC; x.DrawWC += wc;
        // ammunition: missiles for the non-gunpowder shooters, lane E munitions (kg) for gunmen and manned guns; fire throughout
        var d = Defender(i);
        long dueMis = RateIntegrator.IntegrateExact(checked(d.Bowmen * _k.MissilesPerShooterYear), dt, y, ref x.CMis);
        long mis = Math.Min(dueMis, _ledger.Balance(i.MissileStore));
        _ledger.Transfer(i.MissileStore, _missilesSpent, mis);
        x.DueMis += dueMis; x.DrawMis += mis;
        long dueMun = RateIntegrator.IntegrateExact(DefenderMunitionsPerYear(d), dt, y, ref x.CMun);
        long mun = Math.Min(dueMun, _ledger.Balance(i.Magazine));
        _ledger.Transfer(i.Magazine, _munitionsSpent, mun);
        x.DueMun += dueMun; x.DrawMun += mun;
        // the besiegers eat from their field granary (full ration)
        long dueA = RateIntegrator.IntegrateExact(checked(a * SoldierRation), dt, y, ref x.CEatA);
        long fa = Math.Min(dueA, _ledger.Balance(x.AttGranary));
        _ledger.Transfer(x.AttGranary, _foodEaten, fa);
        x.DueA += dueA; x.DrawA += fa;
        // the besiegers' park: topped up from the attacker's home stocks to the need (lane E's munitions rule), then fired
        long dueMunA = RateIntegrator.IntegrateExact(AttackerMunitionsPerYear(x), dt, y, ref x.CMunA);
        Move(_homeMunitions[x.Spec.Attacker], x.AttMunitions, Math.Max(0, dueMunA - _ledger.Balance(x.AttMunitions)));
        long munA = Math.Min(dueMunA, _ledger.Balance(x.AttMunitions));
        _ledger.Transfer(x.AttMunitions, _munitionsSpent, munA);
        x.DueMunA += dueMunA; x.DrawMunA += munA;
        long dueMisA = RateIntegrator.IntegrateExact(AttackerMissilesPerYear(x), dt, y, ref x.CMisA);
        Move(_homeMissiles[x.Spec.Attacker], x.AttMissiles, Math.Max(0, dueMisA - _ledger.Balance(x.AttMissiles)));
        long misA = Math.Min(dueMisA, _ledger.Balance(x.AttMissiles));
        _ledger.Transfer(x.AttMissiles, _missilesSpent, misA);
        x.DueMisA += dueMisA; x.DrawMisA += misA;
        x.LastSettled = upTo;
    }

    /// <summary>
    /// The besiegers' food resupply over dt. With a supply line (lane E): their ration need over dt, grossed up for the carriage
    /// loss (<see cref="ArmyLogistics.Gross"/>), capped by the line's throughput over dt and by the home granary, is shipped; what
    /// arrives (<see cref="ArmyLogistics.Delivered"/>) enters the field granary and the rest is eaten on the road (a sink).
    /// Without one: the flat delivered rate of the spec.
    /// </summary>
    private void SupplyBesiegers(SiegeS x, long attackers, long dt)
    {
        long y = SimInstant.MsPerYear;
        int home = _homeFood[x.Spec.Attacker];
        if (!x.HasLine) { Move(home, x.AttGranary, RateIntegrator.IntegrateExact(x.Spec.AttackerSupplyPerYear, dt, y, ref x.CSupplyA)); return; }
        long need = RateIntegrator.IntegrateExact(checked(attackers * SoldierRation), dt, y, ref x.CSupplyA);
        long cap = RateIntegrator.IntegrateExact(x.LineCapacityPerYear, dt, y, ref x.CLineCap);
        long gross = ArmyLogistics.Gross(need, x.LineLoss);
        long ship = gross < 0 ? 0 : Math.Min(Math.Min(gross, cap), _ledger.Balance(home));
        if (ship <= 0) return;
        long del = ArmyLogistics.Delivered(ship, x.LineLoss);
        _ledger.Transfer(home, x.AttGranary, del);
        _ledger.Transfer(home, _carriageLoss, ship - del);
        x.Shipped = checked(x.Shipped + ship); x.Delivered = checked(x.Delivered + del);
    }

    private void Move(int from, int to, long due)
    {
        long m = Math.Min(due, _ledger.Balance(from));
        if (m > 0) _ledger.Transfer(from, to, m);
    }

    /// <summary>Split an available store between garrison and civilian dues by the explicit ShortfallPolicy (not chosen here).</summary>
    private (long G, long C) Share(long available, long dueG, long dueC)
    {
        long total = checked(dueG + dueC);
        if (available >= total) return (dueG, dueC);
        switch (_policy)
        {
            case ShortfallPolicy.CiviliansFirst: { long c = Math.Min(dueC, available); return (Math.Min(dueG, available - c), c); }
            case ShortfallPolicy.GarrisonFirst: { long gg = Math.Min(dueG, available); return (gg, Math.Min(dueC, available - gg)); }
            default:
                // floor of each pro-rata share; the at most one leftover unit stays in the store (no tie-break needed)
                return (FixedMath.MulDivFloor(available, dueG, total), FixedMath.MulDivFloor(available, dueC, total));
        }
    }

    private static Q32 Met(long draw, long due, long balanceAfter) => due > 0 ? Q.Clamp01(Q32.FromRatio(draw, due)) : (balanceAfter > 0 ? Q32.One : Q32.Zero);

    // ================================================================== the siege-health calculation (once per pulse)

    private void Evaluate(SiegeS x, long now, EventScheduler s)
    {
        _evaluations++;
        long dt = now - x.PulseStart;
        if (dt <= 0) throw new InvalidOperationException("empty siege pulse");
        var i = x.I;
        long[] eng = EngineCounts(x);
        long[] att = AttackerCounts(x);
        var d0 = Defender(i);
        long c0 = _ledger.Balance(i.Civ);
        long g0 = GarrisonPersonnel(i);
        long a0 = AttackerTotal(x);

        // what was actually eaten and drunk this pulse (share of the due at the current ration × the ration); ammunition met
        Q32 shareG = Met(x.DrawG, x.DueG, _ledger.Balance(i.Granary)), shareC = Met(x.DrawC, x.DueC, _ledger.Balance(i.Granary));
        Q32 rw = Q.Max(x.Ration, Q.Permille(_k.WaterMinRationPermille));
        Q32 waterG = rw.Mul(Met(x.DrawWG, x.DueWG, _ledger.Balance(i.Cistern))), waterC = rw.Mul(Met(x.DrawWC, x.DueWC, _ledger.Balance(i.Cistern)));
        x.MisShare = Met(x.DrawMis, x.DueMis, _ledger.Balance(i.MissileStore));
        x.MunShare = Met(x.DrawMun, x.DueMun, _ledger.Balance(i.Magazine));
        x.MisShareA = Met(x.DrawMisA, x.DueMisA, _ledger.Balance(x.AttMissiles));
        x.MunShareA = Met(x.DrawMunA, x.DueMunA, _ledger.Balance(x.AttMunitions));
        x.SupplyA = Met(x.DrawA, x.DueA, _ledger.Balance(x.AttGranary));

        // 1. nutrition (body reserves), 2. disease in both camps
        x.NutG = SiegeEquations.NutritionStep(_k, x.NutG, x.Ration.Mul(shareG), dt);
        x.NutC = SiegeEquations.NutritionStep(_k, x.NutC, x.Ration.Mul(shareC), dt);
        x.NutA = SiegeEquations.NutritionStep(_k, x.NutA, x.SupplyA, dt);
        long pop = checked(g0 + c0);
        Q32 nutAvg = pop <= 0 ? x.NutG : new Q32(FixedMath.MulDivFloor(x.NutG.Raw, g0, pop) + FixedMath.MulDivFloor(x.NutC.Raw, c0, pop));
        x.Dis = SiegeEquations.DiseaseStep(_k, x.Dis, Q.Ratio(pop, i.Spec.Housing), nutAvg, dt);
        x.DisA = SiegeEquations.DiseaseStep(_k, x.DisA, Q.Ratio(a0, x.Spec.CampCapacity), x.NutA, dt);

        // 3. powers at the start of the step's losses (used by fire, morale odds and the reference)
        Q32 hungerA = SiegeEquations.HungerFactor(_k, x.NutA), hungerG = SiegeEquations.HungerFactor(_k, x.NutG);
        Q32 fort = SiegeEquations.FortFactor(_k, i.Spec.Fort, i.Wall, i.Works);
        long dCap = SiegeEquations.Capability(_k, d0, x.MisShare, x.MunShare, x.Dis, x.NutG, fort);
        bool approachDone = x.Approach.Raw >= Q32.OneRaw;
        long pa = SiegeEquations.AssaultPower(_k, att, x.Spec.Force.Ranged, eng, approachDone, Q32.One, hungerA, x.DisA, x.MoraleA);

        // 4. aimed fire both ways (protection: the fortification for the garrison, the lines for the besiegers); missile and gunpowder
        //    fire each scaled by its own ammunition met
        Q32 crewsA = hungerA.Mul(Q.Sub(Q32.One, x.DisA));
        bool powderA = SiegeEquations.IsGunpowder(x.Spec.Force.Ranged);
        long shootA = checked(att[1] * _k.FireLethalityPermillePerYear[(int)x.Spec.Force.Ranged]);
        long toDef = (powderA ? x.MunShareA : x.Spec.Force.Ranged == RangedKind.None ? Q32.Zero : x.MisShareA).Scale(shootA);
        for (int e = 1; e < 4; e++)
        {
            long l = checked(eng[e] * _k.EngineLethalityPermillePerYear[e]);
            toDef = checked(toDef + (SiegeEquations.IsGunpowder((EngineClass)e) ? x.MunShareA.Scale(l) : l));
        }
        toDef = crewsA.Scale(toDef);
        Q32 crewsD = hungerG.Mul(Q.Sub(Q32.One, x.Dis));
        long toAtt = crewsD.Scale(checked(x.MisShare.Scale(d0.BowLethalityMilliPerYear)
                                          + x.MunShare.Scale(checked(d0.GunmenLethalityMilliPerYear + d0.GunsManned * _k.DefenderGunLethalityPermillePerYear))));
        Q32 lines = Q.Add(Q32.One, Q.Permille(_k.FieldWorksValuePermille).Mul(x.AttWorks));

        // 5. per-head loss RATES (‰/yr): fire, thirst, starvation, disease (lane E's mortality × prevalence over the background,
        //    Sanitation), desertion (lane E's rate on morale, pay-arrears age, hunger). Lane E's HeadcountLoss integrates them.
        bool sanD = _cap[x.Defender].Has(WarTech.Sanitation), sanA = _cap[x.Spec.Attacker].Has(WarTech.Sanitation);
        Span<long> rg = stackalloc long[RateFlows], rc = stackalloc long[RateFlows], ra = stackalloc long[RateFlows];
        rg[FCombat] = SiegeEquations.FirePermilleYear(toDef, fort, g0);
        rg[FThirst] = SiegeEquations.ThirstPermilleYear(_k, waterG);
        rg[FStarve] = SiegeEquations.StarvationPermilleYear(_k, x.NutG);
        rg[FDisease] = SiegeEquations.DiseaseDeathPermilleYear(_k, _r, x.Dis, x.NutG, sanD);
        // invested (no open corridor): the deserters must get through the besiegers' lines
        Q32 escape = CorridorOpen(x) ? Q32.One : Q.Sub(Q32.One, Q.Permille(_k.LinesContainDesertionPermille).Mul(Q.Clamp01(x.AttWorks)));
        rg[FDesert] = escape.Scale(SiegeEquations.DesertionPermilleYear(_r, x.Morale, ArrearsMonths(i, now), x.NutG));
        rc[FThirst] = SiegeEquations.ThirstPermilleYear(_k, waterC);
        rc[FStarve] = SiegeEquations.StarvationPermilleYear(_k, x.NutC);
        rc[FDisease] = SiegeEquations.DiseaseDeathPermilleYear(_k, _r, x.Dis, x.NutC, sanD);
        ra[FCombat] = SiegeEquations.FirePermilleYear(toAtt, lines, a0);
        ra[FStarve] = SiegeEquations.StarvationPermilleYear(_k, x.NutA);
        ra[FDisease] = SiegeEquations.DiseaseDeathPermilleYear(_k, _r, x.DisA, x.NutA, sanA);
        ra[FDesert] = SiegeEquations.DesertionPermilleYear(_r, x.MoraleA, 0, x.NutA);

        // 6. people move (each account's flows share one survival product; fixed account order: formations by id, civilians, arms)
        foreach (var f in _form)
        {
            if (f.State != FormationState.Garrisoned || f.At != i.Spec.Id) continue;
            LoseAtRates(f.Account, rg, dt, f.Carry, 0);
        }
        LoseAtRates(i.Civ, rc, dt, i.CivCarry, 0);
        bool open = x.PulseStart >= x.Spec.CorridorFromMs;
        long evac = Math.Min(_ledger.Balance(i.Civ), RateIntegrator.IntegrateExact(open ? x.Spec.EvacuationPerYear : 0, dt, SimInstant.MsPerYear, ref x.CEvac));
        _ledger.Transfer(i.Civ, _elsewhere[x.Defender], evac);
        long reinf = Math.Min(_ledger.Balance(_homeTroops[x.Defender]), RateIntegrator.IntegrateExact(open ? x.Spec.ReinforcementPerYear : 0, dt, SimInstant.MsPerYear, ref x.CReinf));
        reinf = Math.Min(reinf, Math.Max(0, i.Spec.Capacity - GarrisonPersonnel(i)));   // capacity binds reinforcement too
        if (reinf > 0)
        {
            foreach (var f in _form)
            {
                if (f.State != FormationState.Garrisoned || f.At != i.Spec.Id || f.Spec.Owner != x.Defender) continue;
                if (f.Spec.Family is FormationFamily.Naval or FormationFamily.Air) continue;
                _ledger.Transfer(_homeTroops[x.Defender], f.Account, reinf);
                break;
            }
        }
        long reinfA = Math.Min(_ledger.Balance(_homeTroops[x.Spec.Attacker]),
                               RateIntegrator.IntegrateExact(x.Spec.AttackerReinforcementPerYear, dt, SimInstant.MsPerYear, ref x.CReinfA));
        if (reinfA > 0) ReinforceAttacker(x, reinfA);
        for (int a = 0; a < 4; a++) LoseAtRates(x.AttPeople[a], ra, dt, x.AttCarry, a * FlowCount);
        // 7. counter-battery against the besiegers' machines (manned guns firing munitions)
        long engines = eng[0] + eng[1] + eng[2] + eng[3];
        Q32 cb = SiegeEquations.FireShare(x.MunShare.Scale(checked(d0.GunsManned * _k.CounterBatteryPermillePerYear)), Q32.One, engines, dt);
        for (int e = 0; e < 4; e++) Lose(x.AttEngines[e], cb, ref x.EngCarry[e], _equipLost);

        // 8. walls, field works, mining, approach
        var d1 = Defender(i);
        long g1 = GarrisonPersonnel(i), c1 = _ledger.Balance(i.Civ);
        Q32 labour = hungerG.Mul(Q.Sub(Q32.One, x.Dis)).Mul(Q.Min(Q32.One, Q.Ratio(checked(g1 + _k.CivilianLabourPermille * c1 / 1000), x.LabourRef)));
        eng = EngineCounts(x);
        att = AttackerCounts(x);
        Q32 capA = hungerA.Mul(Q.Sub(Q32.One, x.DisA));
        i.Wall = SiegeEquations.WallStep(_k, i.Spec.Fort, i.Wall, SiegeEquations.WallDamagePerYear(_k, i.Spec.Fort, eng, approachDone, capA, x.MunShareA), labour, dt);
        i.Works = SiegeEquations.FieldWorksStep(_k, i.Works, SiegeEquations.EarthDamagePerYear(_k, eng, capA, x.MunShareA), labour, dt);
        if (x.Spec.Mineable && i.Spec.Fort != FortKind.None)
        {
            x.Mining = Q.Add(x.Mining, SiegeEquations.MiningProgress(_k, att[3], d1.Engineers, capA, dt));
            int guard = 0;
            while (x.Mining.Raw >= Q32.OneRaw && guard++ < 64)
            {
                x.Mining = Q.Sub(x.Mining, Q32.One);
                i.Wall = Q.Clamp01(Q.Sub(i.Wall, Q.Permille(_k.MineDamagePermille[(int)i.Spec.Fort])));
                x.Mines++;
            }
        }
        if (!approachDone) x.Approach = Q.Min(Q32.One, Q.Add(x.Approach, SiegeEquations.ApproachProgress(_k, att[3], att[0], capA, x.Spec.ApproachWorkEngineerYears, dt)));

        // 9. morale (both camps) toward state-derived targets
        bool breach = i.Spec.Fort != FortKind.None && i.Wall.Raw <= Q.Permille(_k.BreachPracticablePermille).Raw;
        long both = Math.Max(1, checked(pa + dCap));
        Q32 tD = SiegeEquations.DefenderMoraleTarget(_k, i.Loyalty, x.NutG, x.Dis, breach, Q.Min(Q32.One, Q.Ratio(g1, Math.Max(1, x.G0))),
                                                      Q.Ratio(pa, both), i.UnpaidShare);
        if (x.ReliefFailed) tD = tD.Mul(Q.Sub(Q32.One, Q.Permille(_k.ReliefHopeLostPermille)));   // hope of relief is gone (state: the relief was beaten)
        Q32 tA = SiegeEquations.AttackerMoraleTarget(_k, x.NutA, x.DisA, Q.Min(Q32.One, Q.Ratio(AttackerTotal(x), Math.Max(1, x.A0))), Q.Ratio(dCap, both));
        x.Morale = SiegeEquations.MoraleStep(_k, x.Morale, tD, dt);
        x.MoraleA = SiegeEquations.MoraleStep(_k, x.MoraleA, tA, dt);

        // 10. siege health = will × capability (and the besiegers' own)
        long dNow = SiegeEquations.Capability(_k, d1, x.MisShare, x.MunShare, x.Dis, x.NutG, Q32.One);
        long aNow = SiegeEquations.AssaultPower(_k, att, x.Spec.Force.Ranged, eng, true, Q32.One, hungerA, x.DisA, Q32.One);
        x.HealthD = SiegeEquations.Health(x.Morale, dNow, x.DRef);
        x.HealthA = SiegeEquations.Health(x.MoraleA, aNow, x.ARef);
        var rec = new SiegePulseRecord(x.Spec.Id, x.PulseIndex, x.PulseStart, now, SiegeEquations.RegimeOf(eng), x.HealthD.Raw, x.HealthA.Raw,
            i.Wall.Raw, i.Works.Raw, x.Morale.Raw, x.MoraleA.Raw, x.Dis.Raw, x.NutG.Raw, g1, c1, AttackerTotal(x),
            _ledger.Balance(i.Granary), _ledger.Balance(i.Cistern), _ledger.Balance(i.Magazine), _ledger.Balance(i.MissileStore), dNow, pa, x.Ration.Raw);
        _pulseDigest = FoldPulse(_pulseDigest, rec);   // whether or not the record is kept (S2-V5)
        if (RecordPulses) _pulseRecords.Add(rec);

        // 11. termination by state
        if (g1 <= 0) { EndSiege(x, SiegeOutcome.GarrisonDestroyed, now, s); return; }
        if (x.HealthD.Raw <= Q.Permille(_k.SurrenderHealthPermille).Raw) { EndSiege(x, SiegeOutcome.Capitulated, now, s); return; }
        if (x.HealthA.Raw <= Q.Permille(_k.AttackerCollapsePermille).Raw || AttackerTotal(x) <= 0) { EndSiege(x, SiegeOutcome.Lifted, now, s); return; }
    }

    /// <summary>The installation is not fully invested this pulse: a corridor carries something and is open.</summary>
    private static bool CorridorOpen(SiegeS x)
        => x.PulseStart >= x.Spec.CorridorFromMs
           && (x.Spec.CorridorFoodPerYear | x.Spec.CorridorMunitionsPerYear | x.Spec.CorridorMissilesPerYear | x.Spec.EvacuationPerYear | x.Spec.ReinforcementPerYear) != 0;

    private int SinkOf(int flow) => flow switch
    {
        FCombat => _kia,
        FThirst => _thirst,
        FStarve => _starved,
        FDisease => _diseased,
        _ => _deserted,
    };

    /// <summary>
    /// Lane E's head-loss integration (<see cref="HeadcountLoss"/>): the account's competing flows (rates ‰/yr) share one survival
    /// product compounded per elapsed day over dt and split the loss pro rata to their rates; each flow keeps its own carry in
    /// units of 1/<see cref="HeadcountLoss.One"/> of a head (carry slot base + flow; <see cref="SiegeEquations.LossAtto"/>). Exact:
    /// a flow moves whole people into its sink, never more than are there. Fix r1 (S-V1): the carry was in thousandths of a head
    /// on a per-pulse loss already floored to thousandths, so any flow below 0.001 head per pulse lost nothing at all (on 60-second
    /// pulses: all background disease and most fire); now only the 10⁻¹⁸-head remainder of the survival product is floored.
    /// </summary>
    private void LoseAtRates(int account, ReadOnlySpan<long> rates, long dtMs, long[] carry, int carryBase)
    {
        long heads = _ledger.Balance(account);
        if (heads <= 0) return;
        long total = 0;
        foreach (long r in rates) total = checked(total + r);
        if (total <= 0) return;
        var survival = HeadcountLoss.Survival(total, dtMs);
        for (int f = 0; f < rates.Length; f++)
        {
            if (rates[f] <= 0) continue;
            Int128 due = checked(SiegeEquations.LossAtto(heads, rates[f], total, survival) + carry[carryBase + f]);
            long n = (long)(due / HeadcountLoss.One);
            long bal = _ledger.Balance(account);
            if (n >= bal) { n = bal; due = (Int128)n * HeadcountLoss.One; }
            carry[carryBase + f] = (long)(due - (Int128)n * HeadcountLoss.One);
            if (n > 0) _ledger.Transfer(account, SinkOf(f), n);
        }
    }

    /// <summary>One rate flow on one account (carry slot <paramref name="slot"/>, 1/<see cref="HeadcountLoss.One"/> of a head).</summary>
    private void LoseAtRate(int account, long permilleYear, long dtMs, long[] carry, int slot, int sink)
    {
        long heads = _ledger.Balance(account);
        if (heads <= 0 || permilleYear <= 0) return;
        var survival = HeadcountLoss.Survival(permilleYear, dtMs);
        Int128 due = checked(SiegeEquations.LossAtto(heads, permilleYear, permilleYear, survival) + carry[slot]);
        long n = (long)Int128.Min(due / HeadcountLoss.One, heads);
        carry[slot] = n == heads ? 0 : (long)(due - (Int128)n * HeadcountLoss.One);
        if (n > 0) _ledger.Transfer(account, sink, n);
    }

    /// <summary>Lose floor(balance × share + carry) people/units from an account into a sink, the remainder carried (Q32 units).</summary>
    private long Lose(int account, Q32 share, ref long carryRaw, int sink)
    {
        long bal = _ledger.Balance(account);
        if (share.Raw <= 0 || bal <= 0) return 0;
        Int128 due = (Int128)bal * share.Raw + carryRaw;
        long n = (long)(due >> Q32.FractionBits);
        long rem = (long)(due - ((Int128)n << Q32.FractionBits));
        if (n >= bal) { n = bal; rem = 0; }
        carryRaw = rem;
        if (n > 0) _ledger.Transfer(account, sink, n);
        return n;
    }

    /// <summary>Spread n reinforcements over the besiegers' arms in the opening proportions p_a / P (P = the opening total), each arm
    /// with an exact carry in units of 1/P of a person: arm a receives floor((n·p_a + carry_a) / P). What the floors leave (fewer than
    /// four people at any instant: each carry is below one person) stays in the home stock and joins as the carries fill, so the split
    /// is the same for any division of time into pulses (fix r1, found while verifying S-V1: the remainder of every pulse went to the
    /// largest arm, so on 60-second pulses — one person per pulse — every reinforcement joined the melee). Arms in index order; a home
    /// stock drawn down by another siege of the same polity gives what it has.</summary>
    private void ReinforceAttacker(SiegeS x, long n)
    {
        long[] p = x.Spec.Force.People;
        long total = p[0] + p[1] + p[2] + p[3];
        if (total <= 0) return;
        int home = _homeTroops[x.Spec.Attacker];
        for (int a = 0; a < 4; a++)
        {
            Int128 due = checked((Int128)n * p[a] + x.ReinfArmCarry[a]);
            long share = (long)(due / total);
            x.ReinfArmCarry[a] = (long)(due - (Int128)share * total);
            long give = Math.Min(share, _ledger.Balance(home));
            if (give > 0) _ledger.Transfer(home, x.AttPeople[a], give);
        }
    }

    private void Adjudicate(SiegeS x, long now, EventScheduler s) => EndSiege(x, CapOutcome(x.HealthD, x.HealthA), now, s);

    /// <summary>The maximum-duration rule: the side with the LOWER health yields; equal health → the defender holds (the burden
    /// of taking a city is on the besieger).</summary>
    public static SiegeOutcome CapOutcome(Q32 defenderHealth, Q32 attackerHealth)
        => defenderHealth.Raw < attackerHealth.Raw ? SiegeOutcome.CapCapitulation : SiegeOutcome.CapLifted;

    // ================================================================== discrete actions at a pulse start

    private void LatchSiegeOrders(SiegeS x, long now)
    {
        if (x.PendRationLog != NoOrder && x.PendRationIssued <= now)
        {
            x.Ration = Q.Permille(x.PendRation);
            x.RationHeld = true;   // a standing order: the doctrine below no longer sets the ration (S-V6)
            Latch(x, x.PendRationLog, x.PendRationIssuer, SiegeKinds.OrderRation, x.PendRationIssued, now, x.PendRation);
            x.PendRationLog = NoOrder;
        }
    }

    /// <summary>Record an order taking effect (doctrine decisions are not orders and are not recorded as latches).</summary>
    private void Latch(SiegeS x, int log, int issuer, int kind, long issued, long now, long value)
    {
        if (log >= 0) _latches.Add(new SiegeLatch(log, issuer, kind, x.Spec.Id, issued, now, x.PulseIndex, value));
    }

    /// <summary>Discrete actions at a pulse start, in a fixed order: relief battle, lift, sally, assault.</summary>
    private void ResolveActions(SiegeS x, long now, EventScheduler? s)
    {
        if (x.ReliefArrived && !x.ReliefResolved) { ResolveRelief(x, now, s); if (!x.Active) return; }
        if (x.PendLiftLog != NoOrder && x.PendLiftIssued <= now)
        {
            Latch(x, x.PendLiftLog, x.PendLiftIssuer, SiegeKinds.OrderLift, x.PendLiftIssued, now, 0);
            x.PendLiftLog = NoOrder;
            EndSiege(x, SiegeOutcome.Lifted, now, s);
            if (s is not null) ScheduleEnd(x, s);
            return;
        }
        if (x.PendSallyLog != NoOrder && x.PendSallyIssued <= now)
        {
            Latch(x, x.PendSallyLog, x.PendSallyIssuer, SiegeKinds.OrderSally, x.PendSallyIssued, now, x.PendSally);
            x.PendSallyLog = NoOrder;
            Sally(x, Q.Permille(x.PendSally), now);
        }
        if (x.PendAssaultLog != NoOrder && x.PendAssaultIssued <= now)
        {
            Latch(x, x.PendAssaultLog, x.PendAssaultIssuer, SiegeKinds.OrderAssault, x.PendAssaultIssued, now, x.PendAssault);
            x.PendAssaultLog = NoOrder;
            Assault(x, Q.Permille(x.PendAssault), now, s);
        }
    }

    /// <summary>The AI doctrine (calibration controller) for AI-controlled sides. Policy, not a siege equation. A human order for the
    /// same action takes precedence: a pending assault or lift suppresses the doctrine's own; the ration is a standing level, so once a
    /// human ration order has taken effect the doctrine no longer sets the ration for the rest of the siege (fix r1, S-V6: it used to
    /// overwrite the latched order at the same pulse start, so the latch record claimed an effect that never happened).</summary>
    private void RunDoctrine(SiegeS x, long now)
    {
        var dd = x.Spec.DefenderAi;
        if (dd is not null && !x.RationHeld)
        {
            // days of food at full ration against the NET draw: the full need minus what an open corridor brings in
            long need = checked(GarrisonPersonnel(x.I) * SoldierRation + _ledger.Balance(x.I.Civ) * _r.RationBase);
            long inflow = now >= x.Spec.CorridorFromMs ? x.Spec.CorridorFoodPerYear : 0;
            long net = need - inflow;
            long days = net <= 0 ? long.MaxValue : (long)((Int128)_ledger.Balance(x.I.Granary) * SimInstant.YearDays / net);
            long r = days < dd.LowTriggerDays ? dd.RationLowPermille : days < dd.RationTriggerDays ? dd.RationHighPermille : 1000;
            x.Ration = Q.Permille(Math.Max(r, _k.RationMinPermille));
        }
        var ad = x.Spec.AttackerAi;
        if (ad is not null && x.PendLiftLog == NoOrder && x.PendAssaultLog == NoOrder)
        {
            if (x.PulseIndex > 0 && x.HealthA.Raw <= Q.Permille(ad.LiftHealthPermille).Raw)
            {
                x.PendLiftLog = DoctrineOrder; x.PendLiftIssued = now; x.PendLiftIssuer = x.Spec.Attacker;
            }
            else if (now - x.LastAssaultMs >= ad.AssaultCooldownMs)
            {
                var (pa, pd) = AssaultPowers(x, Q.Permille(ad.AssaultCommitPermille));
                if (pa > 0 && (pd <= 0 || (Int128)pa * 1000 >= (Int128)pd * ad.AssaultMinOddsPermille))
                {
                    x.PendAssault = ad.AssaultCommitPermille; x.PendAssaultLog = DoctrineOrder; x.PendAssaultIssued = now; x.PendAssaultIssuer = x.Spec.Attacker;
                }
            }
        }
    }

    private (long Pa, long Pd) AssaultPowers(SiegeS x, Q32 commit)
    {
        var i = x.I;
        // No route to the wall until the approach works (ramp, causeway, saps) are complete: no assault is possible.
        bool reachable = x.Spec.ApproachWorkEngineerYears <= 0 || x.Approach.Raw >= Q32.OneRaw;
        long pa = !reachable ? 0 : SiegeEquations.AssaultPower(_k, AttackerCounts(x), x.Spec.Force.Ranged, EngineCounts(x), x.Approach.Raw >= Q32.OneRaw, commit,
                                              SiegeEquations.HungerFactor(_k, x.NutA), x.DisA, x.MoraleA);
        long d = SiegeEquations.Capability(_k, Defender(i), x.MisShare, x.MunShare, x.Dis, x.NutG, SiegeEquations.FortFactor(_k, i.Spec.Fort, i.Wall, i.Works));
        return (pa, Q.Permille(_k.DefenceConcentrationPermille).Mul(SiegeEquations.MoraleFight(x.Morale)).Scale(d));
    }

    private void Assault(SiegeS x, Q32 commit, long now, EventScheduler? s)
    {
        SettleTheatre(now);   // pay up to now before the garrison loses men (S-V2)
        var (pa, pd) = AssaultPowers(x, commit);
        var res = _resolver.Resolve(new AssaultInputs(pa, pd, _seed, x.Spec.Id, now, 1), _k);
        x.Assaults++;
        x.LastAssaultMs = now;
        // attacker losses: the committed melee bear the loss share; engineers half of it; ranged a quarter
        long lossA = 0;
        long committed = commit.Scale(_ledger.Balance(x.AttPeople[0]));
        long mLoss = res.AttackerLossShare.Scale(committed);
        _ledger.Transfer(x.AttPeople[0], _kia, mLoss); lossA += mLoss;
        lossA += Lose(x.AttPeople[3], new Q32(res.AttackerLossShare.Raw / 2), ref x.AttCarry[3 * FlowCount + FAssault], _kia);
        lossA += Lose(x.AttPeople[1], new Q32(res.AttackerLossShare.Raw / 4), ref x.AttCarry[1 * FlowCount + FAssault], _kia);
        long lossD = 0;
        foreach (var f in _form)
            if (f.State == FormationState.Garrisoned && f.At == x.I.Spec.Id) lossD += Lose(f.Account, res.DefenderLossShare, ref f.Carry[FAssault], _kia);
        _battles.Add(new BattleRecord(x.Spec.Id, 1, now, pa, pd, res.Odds.Raw, res.Fortune.Raw, res.Success, lossA, lossD));
        if (s is not null && s.CurrentKey.Class < (byte)EventClass.Significant)
            s.Schedule(now, EventClass.Significant, 0, x.Spec.Id, SiegeKinds.AssaultResolved, res.Success ? 1 : 0);
        if (res.Success) { EndSiege(x, SiegeOutcome.Stormed, now, s); if (s is not null) ScheduleEnd(x, s); return; }
        x.FailedAssaults++;
        x.Morale = Q.Min(Q32.One, Q.Add(x.Morale, Q.Permille(_k.RepelledAssaultBoostPermille)));
        x.MoraleA = Q.Max(Q32.Zero, Q.Sub(x.MoraleA, Q.Permille(_k.FailedAssaultAttackerShockPermille)));
    }

    private void ResolveRelief(SiegeS x, long now, EventScheduler? s)
    {
        x.ReliefResolved = true;
        var rel = new long[4];
        for (int a = 0; a < 4; a++) rel[a] = _ledger.Balance(x.Relief[a]);
        long pr = SiegeEquations.FieldAttackPower(_k, rel, x.Spec.Relief!.Ranged);
        // the garrison sallies in support when its morale is at least one half
        if (x.Morale.Raw >= Q32.OneRaw / 2)
        {
            var d = Defender(x.I);
            var sally = new long[] { d.Melee, 0, d.Cavalry, 0 };
            pr = checked(pr + SiegeEquations.HungerFactor(_k, x.NutG).Mul(new Q32(Q32.OneRaw / 2)).Scale(SiegeEquations.FieldAttackPower(_k, sally, RangedKind.None)));
        }
        long pb = SiegeEquations.LinePower(_k, AttackerCounts(x), x.Spec.Force.Ranged, x.AttWorks, SiegeEquations.HungerFactor(_k, x.NutA), x.DisA, x.MoraleA);
        pb = Q.Sub(Q32.One, Q.Permille(_k.ReliefContainmentPermille)).Scale(pb);   // part of the besiegers must keep facing the city
        var res = _resolver.Resolve(new AssaultInputs(pr, pb, _seed, x.Spec.Id, now, 2), _k);
        long lossR = 0, lossB = 0;
        for (int a = 0; a < 4; a++)
        {
            lossR += Lose(x.Relief[a], res.AttackerLossShare, ref x.ReliefCarry[a], _kia);
            lossB += Lose(x.AttPeople[a], res.DefenderLossShare, ref x.AttCarry[a * FlowCount + FAssault], _kia);
        }
        _battles.Add(new BattleRecord(x.Spec.Id, 2, now, pr, pb, res.Odds.Raw, res.Fortune.Raw, res.Success, lossR, lossB));
        if (res.Success) { EndSiege(x, SiegeOutcome.Relieved, now, s); if (s is not null) ScheduleEnd(x, s); return; }
        x.Morale = Q.Max(Q32.Zero, Q.Sub(x.Morale, Q.Permille(_k.FailedReliefShockPermille)));
        x.ReliefFailed = true;
    }

    private void Sally(SiegeS x, Q32 commit, long now)
    {
        SettleTheatre(now);   // pay up to now before the sally party loses men (S-V2)
        x.Sallies++;
        var d = Defender(x.I);
        var party = new long[] { commit.Scale(d.Melee), 0, commit.Scale(d.Cavalry), 0 };
        long ps = SiegeEquations.HungerFactor(_k, x.NutG).Mul(Q.Sub(Q32.One, x.Dis)).Mul(SiegeEquations.MoraleFight(x.Morale))
                  .Scale(SiegeEquations.FieldAttackPower(_k, party, RangedKind.None));
        long pg = Q.Permille(_k.SallyGuardPermille).Scale(SiegeEquations.LinePower(_k, AttackerCounts(x), x.Spec.Force.Ranged, x.AttWorks, SiegeEquations.HungerFactor(_k, x.NutA), x.DisA, x.MoraleA));
        var res = _resolver.Resolve(new AssaultInputs(ps, pg, _seed, x.Spec.Id, now, 3), _k);
        long lossS = 0, lossG = 0;
        Q32 partyShare = commit.Mul(res.AttackerLossShare);
        foreach (var f in _form)
            if (f.State == FormationState.Garrisoned && f.At == x.I.Spec.Id && (f.Spec.Family == FormationFamily.Infantry || f.Spec.Family == FormationFamily.Cavalry))
                lossS += Lose(f.Account, partyShare, ref f.Carry[FAssault], _kia);
        Q32 guardShare = Q.Permille(_k.SallyGuardPermille).Mul(res.DefenderLossShare);
        for (int a = 0; a < 4; a++) lossG += Lose(x.AttPeople[a], guardShare, ref x.AttCarry[a * FlowCount + FAssault], _kia);
        if (res.Success)
        {
            Q32 burnt = Q.Min(new Q32(Q32.OneRaw / 2), new Q32(res.Odds.Mul(res.Fortune).Raw / 4));
            for (int e = 0; e < 4; e++) Lose(x.AttEngines[e], burnt, ref x.EngCarry[e], _equipLost);
            x.Mining = Q32.Zero;
            if (x.Spec.ApproachWorkEngineerYears > 0) x.Approach = Q.Max(Q32.Zero, Q.Sub(x.Approach, new Q32(Q32.OneRaw / 4)));
        }
        _battles.Add(new BattleRecord(x.Spec.Id, 3, now, ps, pg, res.Odds.Raw, res.Fortune.Raw, res.Success, lossS, lossG));
    }

    /// <param name="s">The scheduler when the caller has one (a revolt crossed in the settled span raises its decision-window event);
    /// null from an order executed at once (no SiegeEnded event is raised there either).</param>
    private void EndSiege(SiegeS x, SiegeOutcome outcome, long now, EventScheduler? s)
    {
        if (!x.Active) throw new InvalidOperationException("siege already ended");
        x.Active = false;
        x.Outcome = outcome;
        x.EndMs = now;
        var i = x.I;
        // The besieged part of the theatre pulse (fix r2, S2-V2): no theatre desertion (the siege evaluation charged it), unrest under
        // the controller that held it. From now on a garrison that stays deserts on the theatre clock again.
        SettleTheatreLosses(i, now, s);
        i.ActiveSiege = -1;
        long prisoners = 0, marched = 0;
        long garrisonAtEnd = GarrisonPersonnel(i);
        int from = i.Controller;
        switch (outcome)
        {
            case SiegeOutcome.Capitulated:
            case SiegeOutcome.Stormed:
            case SiegeOutcome.GarrisonDestroyed:
                SettleTheatre(now);
                foreach (var f in _form)
                {
                    if (f.State != FormationState.Garrisoned || f.At != i.Spec.Id) continue;
                    long n = _ledger.Balance(f.Account);
                    _ledger.Transfer(f.Account, _prisoners[x.Spec.Attacker], n);
                    prisoners += n;
                    f.State = FormationState.Captured; f.At = -1;
                }
                i.Controller = x.Spec.Attacker;
                ClearPay(i);
                _captures.Add(new CaptureRecord(i.Spec.Id, from, i.Controller, now, outcome, prisoners, 0));
                break;
            case SiegeOutcome.CapCapitulation:
                SettleTheatre(now);
                foreach (var f in _form)
                {
                    if (f.State != FormationState.Garrisoned || f.At != i.Spec.Id) continue;
                    marched += _ledger.Balance(f.Account);
                    f.State = FormationState.Mobile; f.At = -1; f.X = i.Spec.ExitX; f.Y = i.Spec.ExitY;
                }
                i.Controller = x.Spec.Attacker;
                ClearPay(i);
                _captures.Add(new CaptureRecord(i.Spec.Id, from, i.Controller, now, outcome, 0, marched));
                break;
            default: break;
        }
        _ends.Add(new SiegeEndRecord(x.Spec.Id, outcome, now, _cal.TurnOf(new SimInstant(Math.Min(now, _cal.CampaignEnd.Ms - 1))), now - x.Spec.StartMs,
            x.PulseIndex + 1, x.Assaults, x.FailedAssaults, x.Mines, x.HealthD.Raw, x.HealthA.Raw, garrisonAtEnd, _ledger.Balance(i.Civ), AttackerTotal(x)));
    }

    /// <summary>Control passes: the old controller's pay arrears to a garrison that is now prisoner or gone are written off (the
    /// installation's pay state restarts for its new controller).</summary>
    private static void ClearPay(Inst i)
    {
        i.UpkeepArrears = 0; i.ArrearsSinceMs = -1; i.UpkeepCarry = default; i.UnpaidShare = Q32.Zero;
    }

    // ================================================================== orders

    private void ExecuteOrder(in ScheduledEvent ev, long now)
    {
        int log = (int)ev.Key.SourceId;
        if (ev.A < 0) { Refuse(log, -1, ev.Kind, now, SiegeRefusal.BadTarget); return; }
        long issuerL = ev.A / WarKinds.PolityStride;
        int target = (int)(ev.A % WarKinds.PolityStride);
        if (issuerL >= _polityCount) { Refuse(log, -1, ev.Kind, now, SiegeRefusal.BadTarget); return; }
        int issuer = (int)issuerL;
        switch (ev.Kind)
        {
            case SiegeKinds.OrderRation:
            case SiegeKinds.OrderAssault:
            case SiegeKinds.OrderSally:
            case SiegeKinds.OrderLift:
                SiegeOrder(log, issuer, ev.Kind, target, ev.B, now);
                return;
            case SiegeKinds.OrderGarrisonAssign:
            case SiegeKinds.OrderGarrisonWithdraw:
            case SiegeKinds.OrderGarrisonReplace:
            case SiegeKinds.OrderGarrisonReinforce:
            {
                var o = new PendingGarrison(log, issuer, ev.Kind, target, ev.B, now);
                // WA1: issued exactly at a theatre pulse start → that pulse (zero simulated time has passed in it).
                if (_tpActive && _tp.StartMs == now) ApplyGarrison(o, now, _tp.Index);
                else _pendingGarrison.Add(o);
                return;
            }
            default: Refuse(log, issuer, ev.Kind, now, SiegeRefusal.UnknownKind); return;
        }
    }

    private void SiegeOrder(int log, int issuer, int kind, int siegeId, long value, long now)
    {
        if (siegeId < 0 || siegeId >= _sieges.Length) { Refuse(log, issuer, kind, now, SiegeRefusal.BadTarget); return; }
        var x = _sieges[siegeId];
        if (!x.Active) { Refuse(log, issuer, kind, now, x.Begun ? SiegeRefusal.SiegeOver : SiegeRefusal.BadTarget); return; }
        bool defenderOrder = kind == SiegeKinds.OrderRation || kind == SiegeKinds.OrderSally;
        if (issuer != (defenderOrder ? x.Defender : x.Spec.Attacker)) { Refuse(log, issuer, kind, now, SiegeRefusal.WrongSide); return; }
        if (kind == SiegeKinds.OrderRation && (value < _k.RationMinPermille || value > 1000)) { Refuse(log, issuer, kind, now, SiegeRefusal.BadValue); return; }
        if ((kind == SiegeKinds.OrderAssault || kind == SiegeKinds.OrderSally) && (value < 1 || value > 1000)) { Refuse(log, issuer, kind, now, SiegeRefusal.BadValue); return; }
        bool immediate = x.PulseStart == now;
        switch (kind)
        {
            case SiegeKinds.OrderRation:
                if (immediate)
                {
                    // no time has passed in this pulse: the new ration covers all of it (flows settle lazily at the ration in force)
                    x.Ration = Q.Permille(value);
                    x.RationHeld = true;
                    Latch(x, log, issuer, kind, now, now, value);
                    x.PendRationLog = NoOrder;
                }
                else { x.PendRation = value; x.PendRationLog = log; x.PendRationIssued = now; x.PendRationIssuer = issuer; }
                return;
            case SiegeKinds.OrderLift:
                if (immediate)
                {
                    Latch(x, log, issuer, kind, now, now, 0);
                    EndSiege(x, SiegeOutcome.Lifted, now, null);
                }
                else { x.PendLiftLog = log; x.PendLiftIssued = now; x.PendLiftIssuer = issuer; }
                return;
            case SiegeKinds.OrderSally:
                if (immediate) { Latch(x, log, issuer, kind, now, now, value); Sally(x, Q.Permille(value), now); }
                else { x.PendSally = value; x.PendSallyLog = log; x.PendSallyIssued = now; x.PendSallyIssuer = issuer; }
                return;
            case SiegeKinds.OrderAssault:
                if (immediate) { Latch(x, log, issuer, kind, now, now, value); Assault(x, Q.Permille(value), now, null); }
                else { x.PendAssault = value; x.PendAssaultLog = log; x.PendAssaultIssued = now; x.PendAssaultIssuer = issuer; }
                return;
        }
    }

    private void Refuse(int log, int issuer, int kind, long now, int reason) => _refusals.Add(new SiegeOrderRefusal(log, issuer, kind, now, reason));

    // ================================================================== helpers

    private long[] AttackerCounts(SiegeS x)
    {
        var a = new long[4];
        for (int k = 0; k < 4; k++) a[k] = _ledger.Balance(x.AttPeople[k]);
        return a;
    }

    private long[] EngineCounts(SiegeS x)
    {
        var e = new long[4];
        for (int k = 0; k < 4; k++) e[k] = _ledger.Balance(x.AttEngines[k]);
        return e;
    }

    private long AttackerTotal(SiegeS x)
    {
        long t = 0;
        for (int k = 0; k < 4; k++) t = checked(t + _ledger.Balance(x.AttPeople[k]));
        return t;
    }

    // ================================================================== persistence rows and hashing

    internal Inst[] InstallationsInternal => _inst;
    internal Form[] FormationsInternal => _form;
    internal SiegeS[] SiegesInternal => _sieges;

    /// <summary>Fold one pulse record into the running digest (every field, in declaration order; <see cref="Rng.CounterRng.Mix"/>, the
    /// SplitMix64 finaliser, per field). A replay-comparison digest, not a cryptographic one; never a logic input.</summary>
    public static ulong FoldPulse(ulong h, in SiegePulseRecord x)
    {
        h = Mix(h, x.SiegeId); h = Mix(h, x.Index); h = Mix(h, x.StartMs); h = Mix(h, x.EndMs); h = Mix(h, (long)x.Regime); h = Mix(h, x.HealthRaw);
        h = Mix(h, x.AttackerHealthRaw); h = Mix(h, x.WallRaw); h = Mix(h, x.WorksRaw); h = Mix(h, x.MoraleRaw); h = Mix(h, x.AttackerMoraleRaw);
        h = Mix(h, x.DiseaseRaw); h = Mix(h, x.NutritionRaw); h = Mix(h, x.Garrison); h = Mix(h, x.Civilians); h = Mix(h, x.Attackers); h = Mix(h, x.Food);
        h = Mix(h, x.Water); h = Mix(h, x.Munitions); h = Mix(h, x.Missiles); h = Mix(h, x.CapabilityMilli); h = Mix(h, x.AssaultPowerMilli);
        return Mix(h, x.RationRaw);
    }

    private static ulong Mix(ulong h, long v) => Rng.CounterRng.Mix(h ^ Bits(v) ^ Rng.CounterRng.GoldenGamma);

    /// <summary>The two's-complement bits of v as an unsigned word, without a wrapping cast (the build checks overflow).</summary>
    private static ulong Bits(long v) => v >= 0 ? (ulong)v : (ulong)(v & long.MaxValue) | 0x8000000000000000UL;

    /// <summary>The running digest of every pulse record evaluated so far (test and bench surface).</summary>
    public ulong PulseDigest => _pulseDigest;

    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        _ledger.AppendTo(h);
        foreach (var r in SiegeCodec.Installations(this)) { var buf = new byte[SiegeCodec.InstallationRowBytes]; SiegeCodec.Encode(r, buf); h.AppendData(buf); }
        foreach (var r in SiegeCodec.Formations(this)) { var buf = new byte[SiegeCodec.FormationRowBytes]; SiegeCodec.Encode(r, buf); h.AppendData(buf); }
        foreach (var r in SiegeCodec.Sieges(this)) { var buf = new byte[SiegeCodec.SiegeRowBytes]; SiegeCodec.Encode(r, buf); h.AppendData(buf); }
        // raid state (fix r1, S-V12: an arrived, unresolved raid was invisible to the hash until it produced a record). Raids have no
        // row of their own (resuming a world from rows is not built); their accounts' balances are in the ledger.
        L(_raids.Length);
        foreach (var r in _raids) { L(r.Spec.Id); L(r.PeopleAcct); L(r.LootAcct); L(r.Arrived ? 1 : 0); L(r.Resolved ? 1 : 0); }
        L(_tp.Index); L(_tp.StartMs); L(_tp.EndMs); L(_tpActive ? 1 : 0); L(_tpLastSettled);
        L(_pendingGarrison.Count);
        foreach (var o in _pendingGarrison) { L(o.LogIndex); L(o.Issuer); L(o.Kind); L(o.FormationId); L(o.Value); L(o.IssuedMs); }
        // every pulse record ever evaluated, through the running digest: independent of RecordPulses (fix r2, S2-V5)
        L((long)(_pulseDigest >> 32)); L((long)(_pulseDigest & 0xFFFF_FFFFUL));
        L(_ends.Count);
        foreach (var x in _ends)
        {
            L(x.SiegeId); L((long)x.Outcome); L(x.AtMs); L(x.Turn); L(x.DurationMs); L(x.Pulses); L(x.Assaults); L(x.FailedAssaults); L(x.Mines);
            L(x.HealthRaw); L(x.AttackerHealthRaw); L(x.Garrison); L(x.Civilians); L(x.Attackers);
        }
        L(_battles.Count);
        foreach (var x in _battles) { L(x.SiegeOrRaidId); L(x.Purpose); L(x.AtMs); L(x.AttackMilli); L(x.DefenceMilli); L(x.OddsRaw); L(x.FortuneRaw); L(x.Success ? 1 : 0); L(x.AttackerLosses); L(x.DefenderLosses); }
        L(_latches.Count);
        foreach (var x in _latches) { L(x.LogIndex); L(x.Issuer); L(x.Kind); L(x.TargetId); L(x.IssuedMs); L(x.EffectiveMs); L(x.EffectiveIndex); L(x.Value); }
        L(_refusals.Count);
        foreach (var x in _refusals) { L(x.LogIndex); L(x.Issuer); L(x.Kind); L(x.AtMs); L(x.Reason); }
        L(_captures.Count);
        foreach (var x in _captures) { L(x.InstallationId); L(x.From); L(x.To); L(x.AtMs); L((long)x.Cause); L(x.Prisoners); L(x.MarchedOut); }
        L(_raidRecords.Count);
        foreach (var x in _raidRecords) { L(x.RaidId); L(x.InstallationId); L(x.AtMs); L(x.Success ? 1 : 0); L(x.Pillaged); L(x.RaiderLosses); L(x.GarrisonLosses); }
        L(_revolts.Count);
        foreach (var x in _revolts) { L(x.InstallationId); L(x.AtMs); }
        L(_snapshots.Count);
        foreach (var x in _snapshots) { L(x.Turn); L(x.AtMs); foreach (var v in x.Balances) L(v); }
        L(_evaluations);
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
