using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Time;
using Proto.M7.War;

namespace Proto.M7.WarEcon;

/// <summary>A polity's opening state. Money in soldier-months; grain in gm; taxes as soldier-months per 1000 civilians per
/// year; trade income in soldier-months per year; blockade as ‰ of trade income lost (an input from naval/occupation lanes).</summary>
public sealed record PolitySetup(int Id, PolityCapability Capability, long Population, long Granary, long Treasury, long PrivateMoney,
                                 long Livestock, long Munitions, long Kit, long Fuel, long LenderCapital,
                                 long BaseTaxPerMilleCivYear, long WarTaxPerMilleCivYear, long TradeIncomePerYear, long BlockadePermille,
                                 long GrowthPerCapitaYear)
{
    /// <summary>Bunker coal in kg (steam-powered navies only; lane E naval logistics).</summary>
    public long Coal { get; init; }
}

/// <summary>A settlement in the theatre an army may forage from: owner, civilians, grain stock (gm) and money.</summary>
public sealed record LocalitySetup(int Id, int Owner, long Civilians, long Grain, long Money)
{
    /// <summary>Share (‰) of its owner's trade income that passes through this locality (a port, a market town on a trade
    /// route). While a hostile force stands in it, that share is lost to the owner (occupation; with a squadron in a port,
    /// a close blockade). PROPOSAL input; 0 = no trade passes here.</summary>
    public long TradeSharePermille { get; init; }
}

/// <summary>An army in one war. Position is metres along its route from its depot; March false = halted. CombatDays lists
/// (pulse index, days of battle in that pulse), the input the battle layer would supply. TrainGrain, PayChest and
/// Munitions are its opening stores.</summary>
public sealed record ArmySetup(int Id, int Owner, int WarId, int Priority, ArmyComposition Composition, Route Route, long Position,
                               bool March, long TrainGrain, long PayChest, long Munitions, (long Pulse, long Days)[] CombatDays);

public sealed record WarEconSetup(FoodOrdering Ordering, LogisticsRates Rates, TerrainLogistics Terrain, PolitySetup[] Polities,
                                  LocalitySetup[] Localities, ArmySetup[] Armies)
{
    /// <summary>[D] Optional support-capability overlay (lane D, <see cref="ISupportOverlay"/>). Null = no support layer: the
    /// world behaves exactly as before the hook existed (every lane E pin unchanged, measured).</summary>
    public ISupportOverlay? Support { get; init; }
}

public enum ArmyCondition : byte { Loyal = 0, Mutinous = 1, Disbanded = 2 }

public enum CascadeKind : byte
{
    ArrearsBegan = 1, ArrearsCleared = 2, HungerBegan = 3, MovementRestricted = 4, ForcedRequisition = 5, Plunder = 6,
    MutinyBegan = 7, MutinyEnded = 8, Disbanded = 9, CreditDefault = 10, Borrowed = 11, LocalFamine = 12, CivilianFamine = 13,
    Contribution = 14, Arrived = 15,
    /// <summary>Deaths in a theatre locality at a campaign settlement (turn-keyed).</summary>
    LocalityFamineDeaths = 16,
    /// <summary>A force began to stand in a foreign locality (Value = locality id): its trade share is lost to the owner.</summary>
    Occupation = 17,
    /// <summary>A force left (or disbanded in) the foreign locality it occupied (Value = locality id).</summary>
    OccupationEnded = 18,
    /// <summary>A naval force took fresh food aboard in port (Value = locality id): the scurvy clock restarts.</summary>
    PortCall = 19,
    /// <summary>Contributions levied IN KIND (grain, gm) by a polity without coinage: they go to the army's wage chest,
    /// which pays its wages in kind (fix round 2, E2-7). <see cref="Contribution"/> stays money (soldier-months).</summary>
    ContributionInKind = 20,
}

/// <summary>A notable step of the war economy (candidates for the D-079 Q2 significant-event list; NOT significant here).</summary>
public readonly record struct CascadeEvent(long AtMs, int ArmyId, int Polity, CascadeKind Kind, long Value);

/// <summary>One army over one War Pulse: what it needed, where it came from, what it did, what it cost.</summary>
public readonly record struct ArmyPulseRow(
    int WarId, int ArmyId, long PulseIndex, long StartMs, long EndMs, ArmyCondition ConditionAtStart,
    long MenStart, long MenEnd, long StandingNeed, long MovementNeed, long Foraged, long Seized, long Shipped, long Delivered,
    long CarriageLoss, FoodStage Stage, long FoodEaten, long HungerPpm, long MovementPpm, long DistanceM, long PositionEnd,
    long PayDue, long PayPaid, long BackPay, long ArrearsEnd, long MoraleEnd, long CohesionEnd, ArmyCondition ConditionEnd,
    long Deserted, long DiseaseDeaths, long AnimalDeaths, long MunitionsNeed, long MunitionsUsed, long KitReplaced, long KitDeficit,
    long FuelUsed, long DesertionPermilleYear, long DiseasePermilleYear, long SupplyQ32Raw, long CoalUsed, long ScurvyPermilleYear);

/// <summary>A campaign settlement of one polity (the stand-in for the turn-atomic campaign economy).</summary>
public readonly record struct TurnSettlementRow(long Turn, long AtMs, int Polity, long Civilians, long CivilianDue, long CivilianEaten,
                                                long FamineDeaths, long GrowthDemand, long GrowthGranted, long InterestDue, long InterestPaid,
                                                long Borrowed, long Debt, bool CreditClosed);

public readonly record struct AllocationRow(long AtMs, int Polity, int WarId, long PulseIndex, FoodAllocation Allocation, MilitaryFoodRequest[] Requests);

/// <summary>
/// Phase C lane E: realistic per-pulse war economics, the insolvency cascade and movement logistics (DIR5 §2.4 "economic
/// effects occur per operational pulse", §2.11 logistics; D-080 WA4 "resolve this as realistically as possible").
///
/// COMPOSITION, NOT EDIT. The world wraps T2's <see cref="WarTheaterWorld"/> (pulse clocks, the straddle split, fog,
/// windows, the order gate) and changes nothing in it: every event goes to the inner world first; afterwards this world
/// settles each NEW sub-step record the inner world appended, plans at a PulseStart, applies the discrete updates at a
/// PulseEnd and runs the campaign settlement at a TurnStart. The inner world must carry zero T2 upkeep and supply rates
/// (its placeholder economics are replaced by this settlement set).
///
/// WHAT SETTLES WHEN (the WA4 settlement set; reports/H-E.md §2):
///   PulseStart (plan, all from state at that instant): march plan; needs by composition (rations, fodder, fuel,
///     munitions, equipment wear); orderly foraging and contributions; the supply line (carriage loss, throughput);
///     the polity's granary allocation under the named FoodOrdering (tiers growth → movement → starvation); forced
///     requisition when hungry; the pay chest (coin) or wage chest (in kind) and back pay; latched desertion, disease and
///     animal-loss rates. Everything the pulse will consume is reserved into the army's own stores.
///   Each sub-step [a, b) of the pulse (b a turn start or the pulse end): consumption, distance, pay due and paid, arrears,
///     desertion (to a deserters-in-transit account), disease deaths, animal deaths — each the exact pro-rata share of
///     the pulse total (RateIntegrator.IntegrateExact over the pulse length, carries reset per pulse) or, for pay, the
///     exact per-year integration with a persistent carry. Partition-invariant by construction.
///   PulseEnd: deserters reach home; morale and cohesion; mutiny; disbandment.
///   Polity "touch" (at its wars' pulse starts and at turn starts): harvest (labour withdrawn by mobilisation) and revenue
///     (taxes, war tax, trade less blockade) accrue as pure inflows at rates latched only at those instants.
///   TurnStart (campaign settlement): civilian consumption and famine, growth, theatre localities, interest, default,
///     borrowing. These are campaign-turn quantities by design (the campaign economy is turn-atomic).
/// CONSERVATION: every movement of people, grain, animals, money, munitions, kit and fuel is a WarLedger.Transfer between
/// accounts (sources and sinks are accounts), so each commodity total is constant (tests check it by exact equality).
/// </summary>
public sealed class WarEconomyWorld : IEventWorld
{
    private sealed class Polity
    {
        public PolitySetup S = null!;
        public int Civ, Granary, Treasury, Purse, Livestock, MunStock, KitStock, FuelStock, Lender, CoalStock;
        public long LastTouchMs, LastSettleMs, HarvestRateScaled, RevenueRateScaled, Debt;
        /// <summary>What its own mutinous armies plundered from its own localities, kept in their own units (grain gm, money
        /// soldier-months): the unrest inputs (fix round 1, E-V9: no longer one number mixing grain and money).</summary>
        public long OwnPlunderGrain, OwnPlunderMoney;
        public ExactFraction HarvestCarry, RevenueCarry, InterestCarry, CivCarry, GrowthCarry;
        public long InterestAccrued;
        public bool CreditClosed;
    }

    private sealed class Locality { public LocalitySetup S = null!; public int Civ, Grain, Money; public ExactFraction ProdCarry, EatCarry; }

    private sealed class Army
    {
        public ArmySetup S = null!;
        public ArmyComposition C = null!;
        public int Men, Horses, Draft, Train, PayChest, WageChest, Ammo, KitTrain, FuelTank, Deserting, Bunker;
        public long Position, Morale, Cohesion, Arrears, ArrearsSinceMs = -1, ZeroCohesionSinceMs = -1, MenLatched, InitialMen;
        /// <summary>Naval: instant of the last fresh food (scurvy clock). Occupation: the foreign locality it stands in, or -1.</summary>
        public long FreshSinceMs;
        public int OccupiedLoc = -1;
        public ArmyCondition Cond;
        public ExactFraction StandCarry, MoveCarry, PayCarry;
        /// <summary>Persistent carries of drill munitions and equipment wear by time and by distance (fix round 1, E-V8: no
        /// per-pulse rounding up, so totals do not drift with the number of pulses).</summary>
        public ExactFraction MunDrillCarry, KitTimeCarry, KitDistCarry;
        /// <summary>The instant inside the current pulse at which the pay chest runs dry (planned at the pulse start; the pulse
        /// end if it does not): the start of new arrears (fix round 1, E-V8: no backdating to the pulse start).</summary>
        public long DryAtMs;
        // ---- the current pulse's plan (reset at each pulse start) ----
        public bool Planned;
        public long PulseIndex, PulseStart, PulseEnd, HorsesLatched, DraftLatched;
        public long FoodTotal, FuelTotal, AmmoTotal, KitTotal, DistTotal, PayRateScaled, PayLen, CoalTotal;
        public long DesertPm, DiseasePm, AnimalPm, ScurvyPm;
        /// <summary>The pulse's planned head losses in thousandths of a head, compounded per day on the declining strength
        /// (fix round 2, E2-1: no explicit Euler over the pulse); spread linearly over its sub-steps.</summary>
        public long DesMilli, DisMilli, AniHMilli, AniDMilli;
        public ExactFraction FoodC, FuelC, AmmoC, KitC, DistC, CoalC;
        /// <summary>Persistent carries of the head-loss flows (fix round 2, E2-1: a part-head is no longer discarded at each
        /// pulse end, which made small armies under short pulses never lose anyone).</summary>
        public ExactFraction DesC, DisC, AniHC, AniDC;
        /// <summary>[D] The support latched for the current pulse (None without a support overlay).</summary>
        public SupportComposition Sup;
        // row accumulators
        public ArmyCondition CondStart;
        public long MenStart, NS, NM, Foraged, Seized, Shipped, Delivered, Loss, HungerPpm, MovePpm, PayDue, PayPaid, BackPay;
        public long Deserted, Died, AnimalsDied, MunNeed, KitReplaced, KitDeficit, FuelUsed, SupplyQ32, CoalUsed;
        public FoodStage Stage;
    }

    private readonly WarTheaterWorld _inner;
    private readonly CampaignCalendar _cal;
    private readonly WarEconSetup _setup;
    private readonly LogisticsRates _r;
    private readonly WarLedger _l = new();
    private readonly Polity[] _p;
    private readonly Locality[] _loc;
    private readonly Army[] _a;          // indexed by army id
    private readonly int[] _armyKey;     // army ids sorted by (Priority, Id)
    private readonly int _harvestSource, _grainEaten, _carriageLoss, _civEaten, _growthUsed, _inKindWages, _dead, _animalsDead,
                         _munSpent, _kitWorn, _fuelBurnt, _coalBurnt;
    private readonly List<ArmyPulseRow> _rows = new();
    private readonly List<CascadeEvent> _events = new();
    private readonly List<TurnSettlementRow> _turnRows = new();
    private readonly List<AllocationRow> _allocs = new();
    private int _seenSubSteps;

    private static readonly long Y = SimInstant.MsPerYear;
    private const long Day = SimInstant.MsPerDay;
    private const long Micro = 1_000_000;
    /// <summary>Opening balance of the harvest source account: large enough never to run dry in any run here.</summary>
    public const long HarvestSourceOpening = 4_000_000_000_000_000_000;

    public WarEconomyWorld(WarTheaterWorld inner, CampaignCalendar turnCalendar, WarEconSetup setup)
    {
        _inner = inner;
        _cal = turnCalendar;
        _setup = setup;
        _r = setup.Rates;
        if (setup.Ordering != FoodOrdering.CivilianMilitaryGrowth && setup.Ordering != FoodOrdering.CivilianGrowthMilitary)
            throw new ArgumentOutOfRangeException(nameof(setup), "a food ordering must be named (no default)");

        _p = new Polity[setup.Polities.Length];
        foreach (var ps in setup.Polities)
        {
            if (ps.Id < 0 || ps.Id >= _p.Length || _p[ps.Id] is not null) throw new ArgumentException("polity ids must be 0..n-1, each once");
            if (inner.UpkeepRateOf(ps.Id) != 0) throw new ArgumentException("the inner T2 world must carry zero upkeep when the WarEcon layer is attached");
            _p[ps.Id] = new Polity { S = ps };
        }
        _a = new Army[setup.Armies.Length];
        long[] mobilised = new long[_p.Length];
        foreach (var s in setup.Armies)
        {
            if (s.Id < 0 || s.Id >= _a.Length || _a[s.Id] is not null) throw new ArgumentException("army ids must be 0..n-1, each once");
            if (s.Owner < 0 || s.Owner >= _p.Length) throw new ArgumentOutOfRangeException(nameof(setup), "army owner");
            string? bad = s.Composition.Validate(_p[s.Owner].S.Capability);
            if (bad is not null) throw new ArgumentException($"army {s.Id}: {bad}");
            if (s.Position < 0 || s.Position > s.Route.Length) throw new ArgumentOutOfRangeException(nameof(setup), "position");
            mobilised[s.Owner] = checked(mobilised[s.Owner] + s.Composition.Men);
            _a[s.Id] = new Army { S = s, C = s.Composition };
        }

        // ---- accounts: global sources and sinks first, then polities, localities, armies (fixed order = fixed hash) ----
        _harvestSource = _l.Open(Commodity.Grain, HarvestSourceOpening);
        _grainEaten = _l.Open(Commodity.Grain, 0);
        _carriageLoss = _l.Open(Commodity.Grain, 0);
        _civEaten = _l.Open(Commodity.Grain, 0);
        _growthUsed = _l.Open(Commodity.Grain, 0);
        _inKindWages = _l.Open(Commodity.Grain, 0);
        _dead = _l.Open(Commodity.People, 0);
        _animalsDead = _l.Open(Commodity.Animals, 0);
        _munSpent = _l.Open(Commodity.Munitions, 0);
        _kitWorn = _l.Open(Commodity.Kit, 0);
        _fuelBurnt = _l.Open(Commodity.Fuel, 0);
        _coalBurnt = _l.Open(Commodity.Coal, 0);
        for (int i = 0; i < _p.Length; i++)
        {
            var p = _p[i];
            long civ = p.S.Population - mobilised[i];
            if (civ < 0) throw new ArgumentException($"polity {i}: armies exceed the population");
            p.Civ = _l.Open(Commodity.People, civ);
            p.Granary = _l.Open(Commodity.Grain, p.S.Granary);
            p.Treasury = _l.Open(Commodity.Money, p.S.Treasury);
            p.Purse = _l.Open(Commodity.Money, p.S.PrivateMoney);
            p.Livestock = _l.Open(Commodity.Animals, p.S.Livestock);
            p.MunStock = _l.Open(Commodity.Munitions, p.S.Munitions);
            p.KitStock = _l.Open(Commodity.Kit, p.S.Kit);
            p.FuelStock = _l.Open(Commodity.Fuel, p.S.Fuel);
            p.Lender = _l.Open(Commodity.Money, p.S.LenderCapital);
            p.CoalStock = _l.Open(Commodity.Coal, p.S.Coal);
            if (!p.S.Capability.HasNaval(Proto.M7.Tactical.NavalCapability.SteamPower) && p.S.Coal != 0) throw new ArgumentException("coal without SteamPower");
            if (!p.S.Capability.Has(WarTech.Gunpowder) && p.S.Munitions != 0) throw new ArgumentException("munitions without Gunpowder");
            if (!p.S.Capability.Has(WarTech.MotorTransport) && p.S.Fuel != 0) throw new ArgumentException("fuel without MotorTransport");
        }
        _loc = new Locality[setup.Localities.Length];
        foreach (var ls in setup.Localities)
        {
            if (ls.Id < 0 || ls.Id >= _loc.Length || _loc[ls.Id] is not null) throw new ArgumentException("locality ids must be 0..n-1, each once");
            if (ls.TradeSharePermille < 0 || ls.TradeSharePermille > 1000) throw new ArgumentOutOfRangeException(nameof(setup), "trade share is a per-mille");
            _loc[ls.Id] = new Locality { S = ls };
        }
        foreach (var lo in _loc)
        {
            lo.Civ = _l.Open(Commodity.People, lo.S.Civilians);
            lo.Grain = _l.Open(Commodity.Grain, lo.S.Grain);
            lo.Money = _l.Open(Commodity.Money, lo.S.Money);
        }
        foreach (var a in _a)
        {
            var c = a.C;
            a.Men = _l.Open(Commodity.People, c.Men);
            a.Horses = _l.Open(Commodity.Animals, c.Horses);
            a.Draft = _l.Open(Commodity.Animals, c.DraftAnimals);
            a.Train = _l.Open(Commodity.Grain, a.S.TrainGrain);
            a.PayChest = _l.Open(Commodity.Money, a.S.PayChest);
            a.WageChest = _l.Open(Commodity.Grain, 0);
            a.Ammo = _l.Open(Commodity.Munitions, a.S.Munitions);
            a.KitTrain = _l.Open(Commodity.Kit, 0);
            a.FuelTank = _l.Open(Commodity.Fuel, 0);
            a.Deserting = _l.Open(Commodity.People, 0);
            a.Bunker = _l.Open(Commodity.Coal, 0);
            a.Position = a.S.Position;
            if ((StandingSegment(a).Terrain == Proto.M7.Spatial.TerrainClass.Water) != c.Naval)
                throw new ArgumentException($"army {a.S.Id}: a naval force starts on Water, a land army ashore");
            a.Morale = 800 * 1000;
            a.Cohesion = 900 * 1000;
            a.MenLatched = c.Men;
            a.InitialMen = c.Men;
            if (!_p[a.S.Owner].S.Capability.Has(WarTech.Gunpowder) && a.S.Munitions != 0) throw new ArgumentException("munitions without Gunpowder");
        }
        _armyKey = new int[_a.Length];
        for (int i = 0; i < _armyKey.Length; i++) _armyKey[i] = i;
        Array.Sort(_armyKey, (x, y) =>
        {
            int c = _a[x].S.Priority.CompareTo(_a[y].S.Priority);
            return c != 0 ? c : x.CompareTo(y);
        });
    }

    // ------------------------------------------------------------------------------------------------ public surface
    public WarTheaterWorld Inner => _inner;
    public WarLedger Ledger => _l;
    public IReadOnlyList<ArmyPulseRow> Rows => _rows;
    public IReadOnlyList<CascadeEvent> Events => _events;
    public IReadOnlyList<TurnSettlementRow> TurnRows => _turnRows;
    public IReadOnlyList<AllocationRow> Allocations => _allocs;
    public long MenOf(int army) => _l.Balance(_a[army].Men);
    public long TrainOf(int army) => _l.Balance(_a[army].Train);
    public long PositionOf(int army) => _a[army].Position;
    public long ArrearsOf(int army) => _a[army].Arrears;
    public long MoraleOf(int army) => _a[army].Morale;
    public long CohesionOf(int army) => _a[army].Cohesion;
    public ArmyCondition ConditionOf(int army) => _a[army].Cond;
    public long GranaryOf(int polity) => _l.Balance(_p[polity].Granary);
    public long TreasuryOf(int polity) => _l.Balance(_p[polity].Treasury);
    public long CiviliansOf(int polity) => _l.Balance(_p[polity].Civ);
    public long CoalStockOf(int polity) => _l.Balance(_p[polity].CoalStock);
    public long BunkerOf(int army) => _l.Balance(_a[army].Bunker);
    public int OccupiedLocalityOf(int army) => _a[army].OccupiedLoc;
    /// <summary>The polity's latched revenue rate, soldier-months per year x1000 (taxes, war tax, trade less what blockade
    /// and occupation cut).</summary>
    public long RevenueRateScaledOf(int polity) => _p[polity].RevenueRateScaled;
    public int CoalBurntAccount => _coalBurnt;
    public long DebtOf(int polity) => _p[polity].Debt;
    public bool CreditClosedOf(int polity) => _p[polity].CreditClosed;
    /// <summary>Grain (gm) the polity's own mutinous armies plundered from its own localities (an unrest input).</summary>
    public long OwnSidePlunderGrainOf(int polity) => _p[polity].OwnPlunderGrain;
    /// <summary>Money (soldier-months) the polity's own mutinous armies plundered from its own localities (an unrest input).</summary>
    public long OwnSidePlunderMoneyOf(int polity) => _p[polity].OwnPlunderMoney;
    /// <summary>The instant the army's current arrears began (-1 when it owes nothing).</summary>
    public long ArrearsSinceOf(int army) => _a[army].ArrearsSinceMs;
    /// <summary>The instant the army's cohesion reached zero (interpolated on the pulse's path), or -1 (fix round 2, E2-2).</summary>
    public long ZeroCohesionSinceOf(int army) => _a[army].ZeroCohesionSinceMs;
    public long LocalityGrainOf(int loc) => _l.Balance(_loc[loc].Grain);
    public long LocalityCiviliansOf(int loc) => _l.Balance(_loc[loc].Civ);
    public long LocalityMoneyOf(int loc) => _l.Balance(_loc[loc].Money);
    public int DeadAccount => _dead;
    public int GrainEatenAccount => _grainEaten;
    public int CarriageLossAccount => _carriageLoss;
    // ---- [D] lane D support layer: read access to the accounts a battle collapse moves people between, the latched support,
    // and ONE mutator, the morale a battle leaves (the TacticalResolver's end morale, DIR5 §2.12 "Morale decreases when troops
    // participate in combat"). Nothing else of this world is writable from outside.
    public int MenAccountOf(int army) => _a[army].Men;
    public int CivilianAccountOf(int polity) => _p[polity].Civ;
    public SupportComposition SupportOf(int army) => _a[army].Sup;
    public long MenLatchedOf(int army) => _a[army].MenLatched;

    /// <summary>[D] Sets the army's campaign morale (micro units, 0..1,000,000) to what a battle left it. Pulse-end recovery and
    /// penalties then run on it as before. Refused for a disbanded army or an out-of-range value.</summary>
    public void SetMoraleAfterBattle(int army, long moraleMicro)
    {
        if (moraleMicro < 0 || moraleMicro > 1000 * 1000) throw new ArgumentOutOfRangeException(nameof(moraleMicro));
        if (_a[army].Cond == ArmyCondition.Disbanded) throw new InvalidOperationException("a disbanded army has no morale");
        _a[army].Morale = moraleMicro;
    }

    public void Begin(EventScheduler s)
    {
        _inner.Begin(s);
        long now = s.NowMs;
        foreach (var a in _a) a.FreshSinceMs = now;   // a squadron puts to sea freshly victualled
        foreach (var p in _p) { p.LastTouchMs = now; p.LastSettleMs = now; Relatch(p); }
        _locLastSettle = now;
    }

    public bool IsLive(EntityRef entity) => _inner.IsLive(entity);

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        _inner.Execute(in ev, s);
        long now = ev.Key.TimeMs;
        var subs = _inner.SubSteps;
        for (; _seenSubSteps < subs.Count; _seenSubSteps++) SettleSubStep(subs[_seenSubSteps]);
        if (ev.Key.Class == (byte)EventClass.PlayerOrder) return;
        switch (ev.Kind)
        {
            case WarKinds.PulseEnd: OnPulseEnd((int)ev.Key.SourceId, ev.A, now); break;
            case WarKinds.TurnStart: OnTurnStart(ev.A, now); break;
            case WarKinds.PulseStart: OnPulseStart((int)ev.Key.SourceId, new WarPulse(ev.A, ev.B, ev.C), now); break;
        }
    }

    // ------------------------------------------------------------------------------------------------ polity touch
    /// <summary>Accrue the polity's pure inflows (harvest, revenue) and interest over [LastTouch, now) at the latched rates.</summary>
    private void Touch(Polity p, long now)
    {
        long dt = now - p.LastTouchMs;
        if (dt < 0) throw new InvalidOperationException("polity touched in the past");
        if (dt == 0) return;
        long harvest = RateIntegrator.IntegrateExact(p.HarvestRateScaled, dt, Y * 1000, ref p.HarvestCarry);
        _l.Transfer(_harvestSource, p.Granary, harvest);
        long revenue = RateIntegrator.IntegrateExact(p.RevenueRateScaled, dt, Y * 1000, ref p.RevenueCarry);
        _l.Transfer(p.Purse, p.Treasury, Math.Min(revenue, _l.Balance(p.Purse)));
        p.InterestAccrued = checked(p.InterestAccrued + RateIntegrator.IntegrateExact(checked(p.Debt * _r.InterestPermilleYear), dt, Y * 1000, ref p.InterestCarry));
        p.LastTouchMs = now;
    }

    /// <summary>Latch harvest and revenue rates from state that changes only at pulse boundaries and settlements: civilians
    /// (deserters arrive at pulse ends) and each army's men at its latest pulse start (labour withdrawn).</summary>
    private void Relatch(Polity p)
    {
        long mob = 0;
        foreach (var a in _a) if (a.S.Owner == p.S.Id && a.Cond != ArmyCondition.Disbanded) mob = checked(mob + a.MenLatched);
        long civ = _l.Balance(p.Civ);
        // Labour withdrawn (DIR5 audit; F-economy measured 1.3-1.9 grain per soldier-year): each mobilised man removes
        // LostHarvestPerSoldierYear from the harvest his household would otherwise have brought in.
        long harvestPerYear = Math.Max(0, checked((civ + mob) * _r.HarvestPerCapitaYear - mob * _r.LostHarvestPerSoldierYear));
        p.HarvestRateScaled = checked(harvestPerYear * 1000);
        // Trade disruption: a blockade (input) and every locality of this polity a hostile force stands in (its trade
        // share; each locality once, however many forces are there), together at most the whole trade income.
        long occupied = 0;
        foreach (var lo in _loc)
            if (lo.S.Owner == p.S.Id && IsOccupied(lo.S.Id)) occupied = checked(occupied + lo.S.TradeSharePermille);
        long lost = Math.Min(1000, checked(p.S.BlockadePermille + occupied));
        p.RevenueRateScaled = checked(civ * (p.S.BaseTaxPerMilleCivYear + p.S.WarTaxPerMilleCivYear)
                                      + p.S.TradeIncomePerYear * (1000 - lost));
    }

    /// <summary>The segment the force physically stands on. At a boundary between land and Water it is the side the force
    /// can be on: a land army at the water's edge is ashore, a fleet at the shore is still afloat.</summary>
    private static RouteSegment StandingSegment(Army a)
    {
        var r = a.S.Route;
        int i = r.SegmentAt(a.Position);
        if (i > 0 && a.Position == r.StartOf(i) && (r[i].Terrain == Proto.M7.Spatial.TerrainClass.Water) != a.C.Naval) i--;
        return r[i];
    }

    private bool IsOccupied(int loc)
    {
        foreach (var a in _a) if (a.Cond != ArmyCondition.Disbanded && a.OccupiedLoc == loc) return true;
        return false;
    }

    /// <summary>True when the owner is the army's enemy: another polity that is a participant of the army's war (fix round 1,
    /// E-V6). A locality of a polity outside the war is neutral ground: no foraging, contributions, requisition or
    /// occupation (mutineers still plunder it: discipline has collapsed).</summary>
    private bool Hostile(Army a, int owner) => owner != a.S.Owner && _inner.IsParticipant(a.S.WarId, owner);

    /// <summary>The enemy locality the force stands in (its current segment's locality when an enemy owns it), or -1.</summary>
    private int OccupationAt(Army a)
    {
        int loc = StandingSegment(a).Locality;
        return loc >= 0 && Hostile(a, _loc[loc].S.Owner) ? loc : -1;
    }

    private long NextSettlement(long now)
    {
        if (now >= _cal.CampaignEnd.Ms) return now;
        long turn = _cal.TurnOf(new SimInstant(now));
        return turn + 1 < _cal.TotalTurns ? _cal.TurnStart(turn + 1).Ms : _cal.CampaignEnd.Ms;
    }

    // ------------------------------------------------------------------------------------------------ pulse start: the plan
    private void OnPulseStart(int warId, WarPulse pulse, long now)
    {
        long w = pulse.EndMs - pulse.StartMs;
        var touched = new bool[_p.Length];
        foreach (int id in _armyKey)
        {
            var a = _a[id];
            if (a.S.WarId != warId || a.Cond == ArmyCondition.Disbanded) continue;
            if (!touched[a.S.Owner]) { touched[a.S.Owner] = true; Touch(_p[a.S.Owner], now); }
            a.MenLatched = _l.Balance(a.Men);
            // Occupation changes only at the force's pulse start (its position then) and at its disbandment: the victims'
            // revenue is accrued at the old rate up to now before the change, then relatched (crossing-invariant instants).
            int occ = OccupationAt(a);
            if (occ != a.OccupiedLoc)
            {
                foreach (int l in new[] { a.OccupiedLoc, occ })
                    if (l >= 0 && !touched[_loc[l].S.Owner]) { touched[_loc[l].S.Owner] = true; Touch(_p[_loc[l].S.Owner], now); }
                if (a.OccupiedLoc >= 0) _events.Add(new CascadeEvent(now, a.S.Id, _loc[a.OccupiedLoc].S.Owner, CascadeKind.OccupationEnded, a.OccupiedLoc));
                if (occ >= 0) _events.Add(new CascadeEvent(now, a.S.Id, _loc[occ].S.Owner, CascadeKind.Occupation, occ));
                a.OccupiedLoc = occ;
            }
        }
        for (int i = 0; i < _p.Length; i++) if (touched[i]) Relatch(_p[i]);

        // 1. per army: needs, march plan, orderly foraging, contributions, requests (in (Priority, Id) order).
        var reqs = new List<MilitaryFoodRequest>[_p.Length];
        var lines = new SupplyLine[_a.Length];
        var march = new MarchPlan[_a.Length];
        var wagesDue = new long[_a.Length];
        foreach (int id in _armyKey)
        {
            var a = _a[id];
            if (a.S.WarId != warId || a.Cond == ArmyCondition.Disbanded) continue;
            var cap = _p[a.S.Owner].S.Capability;
            ResetPlan(a, pulse, w);
            long men = a.MenLatched;
            a.HorsesLatched = _l.Balance(a.Horses);
            a.DraftLatched = _l.Balance(a.Draft);
            // [D] Panel-managed support for this pulse (None without an overlay): its wagons and draft animals join the army's
            // own train in lane E's logistics arithmetic below; its staff, animals and patients add to the needs.
            a.Sup = _setup.Support?.Latch(a.S.Id, a.S.Owner, warId, pulse.Index, now, men, a.C.Naval) ?? SupportComposition.None;
            var ce = SupportedComposition(a);
            bool moving = a.S.March && a.Cond == ArmyCondition.Loyal && a.Position < a.S.Route.Length;
            march[id] = moving ? ArmyLogistics.PlanMarch(a.S.Route, a.Position, w, ce, cap, _r, _setup.Terrain) : default;
            var seg = StandingSegment(a);
            long graze = _setup.Terrain.GrazePermille(seg.Terrain);
            // Standing need over W: rations (base + field), animals' rest feed, roughage not met by grazing (as grain).
            // At sea the sea ration replaces the field extra (NavalLogistics).
            long extra = a.C.Naval ? _r.RationSeaExtra : _r.RationFieldExtra;
            long standScaled = checked(men * (_r.RationBase + extra) * 1000
                + a.HorsesLatched * _r.HorseHardRest * 1000 + a.DraftLatched * _r.DraftHardRest * 1000
                + (a.HorsesLatched * _r.HorseRoughageAsGrain + a.DraftLatched * _r.DraftRoughageAsGrain) * (1000 - graze)
                // [D] support staff (field ration), patients (base ration), support draft animals (rest feed, roughage)
                + a.Sup.Staff * (_r.RationBase + extra) * 1000 + a.Sup.Patients * _r.RationBase * 1000
                + a.Sup.DraftAnimals * _r.DraftHardRest * 1000 + a.Sup.DraftAnimals * _r.DraftRoughageAsGrain * (1000 - graze));
            a.NS = RateIntegrator.IntegrateExact(standScaled, w, Y * 1000, ref a.StandCarry);
            // Movement need over the marching time: exertion of men, working feed of animals.
            // A naval force: only rowers labour to move it (sail and steam have no exertion term; NavalLogistics).
            long moveRate = a.C.Naval
                ? checked(NavalLogistics.Rowers(a.C, men, _r) * _r.RationRowExtra)
                : checked(men * _r.RationMarchExtra + a.HorsesLatched * _r.HorseHardWorkExtra + a.DraftLatched * _r.DraftHardWorkExtra
                          + a.Sup.Staff * _r.RationMarchExtra + a.Sup.DraftAnimals * _r.DraftHardWorkExtra);   // [D] support marches too
            a.NM = RateIntegrator.IntegrateExact(moveRate, march[id].MarchMs, Y, ref a.MoveCarry);

            // Orderly foraging (own locality: only the surplus above its civilians' reserve; enemy: a stock draw at a per-year
            // rate) and, in an enemy locality, contributions in money. A mutinous army plunders instead (forced rate, any owner;
            // soldiers pay themselves). A neutral locality (owner outside the war) gives nothing (E-V6). Every share of a stock
            // is a StockDraw over the pulse length (Law 3, E-V1).
            long trainCap = TrainCapacity(a, ce, checked(men + a.Sup.Staff));   // [D] support wagons and staff carry too
            long want = Math.Max(0, checked(a.NS + a.NM + trainCap - _l.Balance(a.Train)));
            if (seg.Locality >= 0)
            {
                var lo = _loc[seg.Locality];
                if (a.Cond == ArmyCondition.Mutinous) { if (want > 0) Plunder(a, lo, now, want, w); }
                else if (lo.S.Owner == a.S.Owner)
                {
                    if (want > 0)
                    {
                        long reserve = LocalReserve(lo, now);
                        long take = Math.Min(want, Math.Max(0, _l.Balance(lo.Grain) - reserve));
                        _l.Transfer(lo.Grain, a.Train, take);
                        a.Foraged += take;
                    }
                }
                else if (Hostile(a, lo.S.Owner))
                {
                    long take = Math.Min(want, StockDraw.Share(_l.Balance(lo.Grain), _r.ForageForeignPermilleYear, w));
                    _l.Transfer(lo.Grain, a.Train, take);
                    a.Foraged += take;
                    // Contributions are a levy on the occupied place, raised whatever the army's grain want ("war feeds
                    // itself": Wallenstein's and the French revolutionary armies' contribution systems; unverified). A polity
                    // with coinage levies money into the pay chest; one without levies in kind (grain) into the wage chest
                    // that pays its soldiers in kind (fix round 2, E2-7: money it cannot pay with no longer strips the town).
                    if (cap.Has(WarTech.Coinage))
                    {
                        long contrib = StockDraw.Share(_l.Balance(lo.Money), _r.ContributionPermilleYear, w);
                        if (contrib > 0) { _l.Transfer(lo.Money, a.PayChest, contrib); _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.Contribution, contrib)); }
                    }
                    else
                    {
                        long contrib = StockDraw.Share(_l.Balance(lo.Grain), _r.ContributionPermilleYear, w);
                        if (contrib > 0) { _l.Transfer(lo.Grain, a.WageChest, contrib); _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.ContributionInKind, contrib)); }
                    }
                }
            }

            // Requests at the army, then grossed up for the carriage loss and capped by throughput in tier order.
            long own = _l.Balance(a.Train);
            long reqS = Math.Max(0, a.NS - own), o1 = Math.Max(0, own - a.NS);
            long reqM = Math.Max(0, a.NM - o1), o2 = Math.Max(0, o1 - a.NM);
            long reqR = Math.Max(0, trainCap - o2);
            bool coin = cap.Has(WarTech.Coinage);
            a.PayLen = checked(Y * a.InitialMen);
            a.PayRateScaled = checked(men * (coin
                ? a.C.Foot * _r.PayFoot + a.C.Mounted * _r.PayMounted + a.C.GunCrew * _r.PayGunCrew + a.C.Seamen * _r.PaySeaman
                : a.C.Foot * _r.WageInKindFoot + a.C.Mounted * _r.WageInKindMounted + a.C.GunCrew * _r.WageInKindGunCrew
                  + a.C.Seamen * _r.WageInKindSeaman)
                // [D] support staff draw a foot soldier's pay (PROPOSAL); the scale InitialMen matches PayLen = Y * InitialMen
                + a.Sup.Staff * (coin ? _r.PayFoot : _r.WageInKindFoot) * a.InitialMen);
            var probe = a.PayCarry;
            long pulseDue = RateIntegrator.IntegrateExact(a.PayRateScaled, w, a.PayLen, ref probe);
            long reqW = coin ? 0 : Math.Max(0, checked(a.Arrears + pulseDue - _l.Balance(a.WageChest)));
            wagesDue[id] = pulseDue;

            var line = ArmyLogistics.Evaluate(a.S.Route, a.Position, w, ce, cap, _r, _setup.Terrain);   // [D] supported train
            lines[id] = line;
            long capLeft = line.CapacityPerPulse;
            long FuelCap(long left)
            {
                if (line.MotorHaulM == 0) return left;
                long fuel = _l.Balance(_p[a.S.Owner].FuelStock);
                Int128 maxGm = (Int128)fuel * _r.TruckPayloadGm * 1_000_000 / ((Int128)2 * line.MotorHaulM * _r.FuelPerTruck1000Km);
                return maxGm < left ? (long)maxGm : left;
            }
            capLeft = FuelCap(capLeft);
            long Ship(long atArmy)
            {
                long g = ArmyLogistics.Gross(atArmy, line.Loss);
                if (g < 0) return 0;
                long s = Math.Min(g, capLeft);
                capLeft -= s;
                return s;
            }
            long dS = Ship(reqS), dM = Ship(reqM), dR = Ship(reqR), dW = Ship(reqW);
            (reqs[a.S.Owner] ??= new List<MilitaryFoodRequest>()).Add(new MilitaryFoodRequest(a.S.Id, a.S.Priority, dS, dM, dR, dW));
        }

        // 2. per polity: the automatic allocation (DIR5 tiers under the named ordering), then delivery.
        for (int pi = 0; pi < _p.Length; pi++)
        {
            if (reqs[pi] is null) continue;
            var p = _p[pi];
            long next = NextSettlement(now);
            long civ = _l.Balance(p.Civ);
            long civCharge = MulDivCeil128(civ, _r.RationBase, next - p.LastSettleMs, Y);
            long toCome = (long)((Int128)p.HarvestRateScaled * (next - now) / ((Int128)Y * 1000));
            long civReserve = Math.Max(0, civCharge - toCome);
            long growth = MulDivCeil128(civ, p.S.GrowthPerCapitaYear, next - p.LastSettleMs, Y);
            var reqArr = reqs[pi].ToArray();
            var alloc = WarFoodAllocator.Allocate(_setup.Ordering, _l.Balance(p.Granary), civReserve, growth, reqArr);
            _allocs.Add(new AllocationRow(now, pi, warId, pulse.Index, alloc, reqArr));
            foreach (var g in alloc.Grants)
            {
                var a = _a[g.ArmyId];
                var line = lines[g.ArmyId];
                a.Stage = alloc.Stage;
                long food = checked(g.Standing + g.Movement + g.Restock);
                long del = ArmyLogistics.Delivered(food, line.Loss);
                _l.Transfer(p.Granary, a.Train, del);
                _l.Transfer(p.Granary, _carriageLoss, food - del);
                long wDel = ArmyLogistics.Delivered(g.Wages, line.Loss);
                _l.Transfer(p.Granary, a.WageChest, wDel);
                _l.Transfer(p.Granary, _carriageLoss, g.Wages - wDel);
                a.Shipped += food + g.Wages;
                a.Delivered += del + wDel;
                a.Loss += food - del + g.Wages - wDel;
                long fuel = ArmyLogistics.CarriageFuel(food + g.Wages, line, _r);
                _l.Transfer(p.FuelStock, _fuelBurnt, Math.Min(fuel, _l.Balance(p.FuelStock)));
            }
        }

        // 3. per army: forced requisition when hungry, the consumption plan, munitions, kit, fuel, pay, latched rates.
        foreach (int id in _armyKey)
        {
            var a = _a[id];
            if (a.S.WarId != warId || a.Cond == ArmyCondition.Disbanded || !a.Planned) continue;
            var p = _p[a.S.Owner];
            var cap = p.S.Capability;
            long men = a.MenLatched;
            var seg = StandingSegment(a);
            if (_l.Balance(a.Train) < a.NS && seg.Locality >= 0 && a.Cond == ArmyCondition.Loyal)
            {
                // Supply has failed after home supply and orderly foraging: the commander seizes food. In an enemy locality
                // always; at home never while the army is loyal (the polity's automatic priority protects its civilians, DIR5
                // §2.11); never in a neutral one (E-V6).
                var lo = _loc[seg.Locality];
                if (Hostile(a, lo.S.Owner))
                {
                    long take = Math.Min(a.NS - _l.Balance(a.Train), StockDraw.Share(_l.Balance(lo.Grain), _r.ForageForcedPermilleYear, w));
                    if (take > 0)
                    {
                        _l.Transfer(lo.Grain, a.Train, take);
                        a.Seized += take;
                        _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.ForcedRequisition, take));
                        FamineRisk(lo, now, a);
                    }
                }
            }
            // A naval force in port takes fresh food aboard (own port; abroad only if it got food there): scurvy clock restarts.
            if (a.C.Naval && seg.Locality >= 0 && (_loc[seg.Locality].S.Owner == a.S.Owner || a.Foraged + a.Seized > 0))
            {
                a.FreshSinceMs = now;
                _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.PortCall, seg.Locality));
            }
            long train = _l.Balance(a.Train);
            long cs = Math.Min(a.NS, train);
            long mNum = 0, mDen = 1;
            if (cs == a.NS && a.NM > 0) { mNum = Math.Min(a.NM, train - a.NS); mDen = a.NM; }
            else if (cs == a.NS && march[id].DistanceM > 0) { mNum = 1; mDen = 1; }
            // Motor movement also needs fuel; trucks without fuel stop (movement restricted, never a starvation).
            long fuelNeed = a.C.Trucks > 0 ? MulDivCeil128(a.C.Trucks, _r.FuelPerTruck1000Km, march[id].DistanceM, 1_000_000) : 0;
            if (fuelNeed > 0)
            {
                long ship = Math.Min(fuelNeed - Math.Min(fuelNeed, _l.Balance(a.FuelTank)), _l.Balance(p.FuelStock));
                _l.Transfer(p.FuelStock, a.FuelTank, ship);
                long have = _l.Balance(a.FuelTank);
                if ((Int128)have * mDen < (Int128)fuelNeed * mNum) { mNum = have; mDen = fuelNeed; }
            }
            // Steam vessels need bunker coal to move (colliers bring it from the polity's stock); without it they lie in
            // port or at anchor (movement restricted, never a starvation). Oared and sailing vessels burn none.
            long coalNeed = NavalLogistics.CoalFor(a.C, march[id].DistanceM, _r);
            if (coalNeed > 0)
            {
                long ship = Math.Min(coalNeed - Math.Min(coalNeed, _l.Balance(a.Bunker)), _l.Balance(p.CoalStock));
                _l.Transfer(p.CoalStock, a.Bunker, ship);
                long have = _l.Balance(a.Bunker);
                if ((Int128)have * mDen < (Int128)coalNeed * mNum) { mNum = have; mDen = coalNeed; }
            }
            long cm = FixedMath.MulDivFloor(a.NM, mNum, mDen);
            a.FoodTotal = cs + cm;
            a.DistTotal = FixedMath.MulDivFloor(march[id].DistanceM, mNum, mDen);
            a.FuelTotal = Math.Min(_l.Balance(a.FuelTank), FixedMath.MulDivFloor(fuelNeed, mNum, mDen));
            a.CoalTotal = Math.Min(_l.Balance(a.Bunker), FixedMath.MulDivFloor(coalNeed, mNum, mDen));
            a.HungerPpm = a.NS == 0 ? 0 : FixedMath.MulDivFloor(a.NS - cs, Micro, a.NS);
            a.MovePpm = march[id].DistanceM == 0 ? 0 : FixedMath.MulDivFloor(a.DistTotal, Micro, march[id].DistanceM);
            if (a.HungerPpm > 0) _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.HungerBegan, a.HungerPpm));
            else if (march[id].DistanceM > 0 && a.MovePpm < Micro) _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.MovementRestricted, a.MovePpm));

            // Munitions (gunpowder only): drill over W plus combat days; supplied from the polity's stock to the ammunition train.
            long combat = CombatDays(a.S, pulse.Index);
            long firearmMen = a.C.Firearms ? FixedMath.MulDivFloor(men, a.C.Foot, a.InitialMen) : 0;
            long munNeed = 0;
            if (cap.Has(WarTech.Gunpowder))
                munNeed = checked(RateIntegrator.IntegrateExact(checked(firearmMen * _r.MunitionsPerFirearmYearDrill), w, Y, ref a.MunDrillCarry)
                                  + combat * (firearmMen * _r.MunitionsPerFirearmCombatDay + a.C.Guns * _r.MunitionsPerGunCombatDay
                                              + (a.C.Naval && NavalLogistics.GunArmed(a.C.Vessel!) ? a.C.Hulls * _r.MunitionsPerArmedHullCombatDay : 0)));
            long munShip = Math.Min(Math.Max(0, munNeed - _l.Balance(a.Ammo)), _l.Balance(p.MunStock));
            _l.Transfer(p.MunStock, a.Ammo, munShip);
            a.MunNeed = munNeed;
            a.AmmoTotal = Math.Min(munNeed, _l.Balance(a.Ammo));

            // Equipment: wear by time, by distance (scaled by the ground) and by combat; replaced from the polity's stock.
            long wear = checked(RateIntegrator.IntegrateExact(checked(men * _r.KitWearPerManYear), w, Y, ref a.KitTimeCarry)
                                + RateIntegrator.IntegrateExact(checked(men * _r.KitWearPerMan1000Km), FixedMath.MulDivFloor(march[id].WearM, mNum, mDen), 1_000_000, ref a.KitDistCarry)
                                + men * combat * _r.KitWearPerManCombatDay);
            long replace = Math.Min(checked(wear + a.KitDeficit), _l.Balance(p.KitStock));
            _l.Transfer(p.KitStock, a.KitTrain, replace);
            a.KitTotal = replace;
            a.KitReplaced = replace;
            a.KitDeficit = Math.Min(checked(men * 1000), checked(a.KitDeficit + wear - replace));

            // Pay: contributions are already in the chest; fill it from the treasury (coin) for arrears plus this pulse's due;
            // back pay goes out first. In kind, the wage chest was filled by the allocator.
            bool coin = cap.Has(WarTech.Coinage);
            int chest = coin ? a.PayChest : a.WageChest;
            if (coin)
            {
                long fill = Math.Min(Math.Max(0, checked(a.Arrears + wagesDue[id] - _l.Balance(chest))), _l.Balance(p.Treasury));
                _l.Transfer(p.Treasury, chest, fill);
            }
            long back = Math.Min(a.Arrears, _l.Balance(chest));
            if (back > 0)
            {
                _l.Transfer(chest, coin ? p.Purse : _inKindWages, back);
                a.Arrears -= back;
                a.BackPay = back;
                if (a.Arrears == 0) { a.ArrearsSinceMs = -1; _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.ArrearsCleared, back)); }
            }
            // Pay accrues linearly over the pulse: the chest runs dry at the share of the pulse it covers (planned from the
            // state at the pulse start, so it does not depend on where turn boundaries split the pulse).
            long chestNow = _l.Balance(chest), dueNow = wagesDue[id];
            a.DryAtMs = chestNow >= dueNow || dueNow == 0 ? a.PulseEnd : a.PulseStart + (long)((Int128)w * chestNow / dueNow);

            // Latched rates for the pulse (state at the pulse start: morale, arrears age, hunger, kit, mutiny, sanitation).
            long ageMonths = ArrearsMonths(a, now);
            long hungerPm = a.HungerPpm / 1000;
            // [lane S] the rate formulas moved, unchanged, to TroopLossRates (shared with the siege world).
            long des = TroopLossRates.DesertionPermilleYear(_r, a.Morale, ageMonths, hungerPm, a.Cond == ArmyCondition.Mutinous);
            long kitDefPm = men == 0 ? 0 : Math.Min(1000, a.KitDeficit / men);
            long dis = TroopLossRates.DiseasePermilleYear(_r, hungerPm, kitDefPm, cap.Has(WarTech.Sanitation));
            dis += TroopLossRates.StarvationPermilleYear(_r, hungerPm);   // starvation itself
            // Scurvy (naval forces): time since fresh food at the pulse start; not reduced by Sanitation (it is dietary).
            a.ScurvyPm = a.C.Naval ? NavalLogistics.ScurvyPermilleYear(now - a.FreshSinceMs, cap, _r) : 0;
            dis += a.ScurvyPm;
            a.DesertPm = des;
            a.DiseasePm = dis;
            a.AnimalPm = _r.AnimalDeathBasePermilleYear + _r.AnimalDeathStarvePermilleYear * hungerPm / 1000;
            // The pulse's head losses at the latched rates, compounded per day on the declining strength (E2-1); desertion
            // and disease compete for the same men and split one survival product pro rata to their rates.
            var sMen = HeadcountLoss.Survival(checked(des + dis), w);
            a.DesMilli = HeadcountLoss.LossMilli(men, des, des + dis, sMen);
            a.DisMilli = HeadcountLoss.LossMilli(men, dis, des + dis, sMen);
            var sAni = HeadcountLoss.Survival(a.AnimalPm, w);
            a.AniHMilli = HeadcountLoss.LossMilli(a.HorsesLatched, a.AnimalPm, a.AnimalPm, sAni);
            a.AniDMilli = HeadcountLoss.LossMilli(a.DraftLatched, a.AnimalPm, a.AnimalPm, sAni);

            // Combat supply state for the battle layer (ConditionMath supply input): the worst of fed, munitioned, equipped.
            long fed = Micro - a.HungerPpm;
            long mun = munNeed == 0 || combat == 0 ? Micro : FixedMath.MulDivFloor(a.AmmoTotal, Micro, munNeed);
            long kit = men == 0 ? Micro : Micro - FixedMath.MulDivFloor(Math.Min(a.KitDeficit, men * 1000), Micro, men * 1000);
            a.SupplyQ32 = Q32.FromRatio(Math.Min(fed, Math.Min(mun, kit)), Micro).Raw;
        }
    }

    private void ResetPlan(Army a, WarPulse pulse, long w)
    {
        a.Planned = true;
        a.PulseIndex = pulse.Index; a.PulseStart = pulse.StartMs; a.PulseEnd = pulse.EndMs;
        a.FoodTotal = a.FuelTotal = a.AmmoTotal = a.KitTotal = a.DistTotal = 0;
        a.FoodC = a.FuelC = a.AmmoC = a.KitC = a.DistC = ExactFraction.Zero;
        a.DesMilli = a.DisMilli = a.AniHMilli = a.AniDMilli = 0;
        a.CondStart = a.Cond;
        a.MenStart = _l.Balance(a.Men);
        a.NS = a.NM = a.Foraged = a.Seized = a.Shipped = a.Delivered = a.Loss = a.HungerPpm = a.MovePpm = 0;
        a.PayDue = a.PayPaid = a.BackPay = a.Deserted = a.Died = a.AnimalsDied = a.MunNeed = a.KitReplaced = a.FuelUsed = 0;
        a.CoalTotal = a.CoalUsed = a.ScurvyPm = 0;
        a.CoalC = ExactFraction.Zero;
        a.SupplyQ32 = Q32.OneRaw;
        a.Stage = FoodStage.Unconstrained;
        if (w <= 0) throw new InvalidOperationException("empty pulse");
    }

    /// <summary>What the army can carry: its wagons' and trucks' payload plus the rations its people carry. [D] c is the
    /// composition with the pulse's support wagons (<see cref="SupportedComposition"/>), men the people who carry their own
    /// rations (soldiers and support staff); with no support both equal the original inputs exactly.</summary>
    private long TrainCapacity(Army a, ArmyComposition c, long men)
        => a.C.Naval
            ? FixedMath.MulDivFloor(checked(men * (_r.RationBase + _r.RationSeaExtra)), NavalLogistics.StoresDays(a.C.Vessel!, _r), SimInstant.YearDays)
            : checked(c.Wagons * _r.WagonPayloadGm + c.Trucks * _r.TruckPayloadGm
                      + FixedMath.MulDivFloor(men * _r.RationBase, _r.CarryDaysPerMan, SimInstant.YearDays));

    /// <summary>[D] The army's composition for the pulse's logistics: its own, plus the latched support wagons and their draft
    /// animals (the very object lane E's train arithmetic already models; no second supply model). The same instance when
    /// nothing is attached.</summary>
    private static ArmyComposition SupportedComposition(Army a)
        => a.Sup.Wagons == 0 && a.Sup.DraftAnimals == 0
            ? a.C
            : a.C with { Wagons = checked(a.C.Wagons + a.Sup.Wagons), DraftAnimals = checked(a.C.DraftAnimals + a.Sup.DraftAnimals) };

    /// <summary>The locals' reserve: exactly what the next campaign settlement will charge them — their consumption over the
    /// whole interval since their LAST settlement (eaten or not yet; with its carry), not only from now on — before any
    /// harvest (the stand-in credits it in a lump at the settlement while people eat throughout). Fix round 1, E-V2: the
    /// reserve from now on let a loyal army at home take food the settlement then charged, so the town starved there.</summary>
    private long LocalReserve(Locality lo, long now)
    {
        var probe = lo.EatCarry;
        return RateIntegrator.IntegrateExact(checked(_l.Balance(lo.Civ) * _r.RationBase), NextSettlement(now) - _locLastSettle, Y, ref probe);
    }

    private void FamineRisk(Locality lo, long now, Army a)
    {
        long reserve = LocalReserve(lo, now);
        long stock = _l.Balance(lo.Grain);
        if (stock < reserve) _events.Add(new CascadeEvent(now, a.S.Id, lo.S.Owner, CascadeKind.LocalFamine, reserve - stock));
    }

    /// <summary>A mutinous army plunders the locality it stands in, whoever owns it: grain into its train (as much as it
    /// needs and can carry), money into the soldiers' own hands (it reduces their arrears: they pay themselves). Unrest
    /// rises where the victim is the army's own polity.</summary>
    private void Plunder(Army a, Locality lo, long now, long want, long w)
    {
        long grain = Math.Min(want, StockDraw.Share(_l.Balance(lo.Grain), _r.ForageForcedPermilleYear, w));
        long money = StockDraw.Share(_l.Balance(lo.Money), _r.ForageForcedPermilleYear, w);
        if (grain == 0 && money == 0) return;   // nothing left to take
        _l.Transfer(lo.Grain, a.Train, grain);
        _l.Transfer(lo.Money, _p[a.S.Owner].Purse, money);
        a.Seized += grain;
        long relief = Math.Min(a.Arrears, money);
        a.Arrears -= relief;
        // Arrears cleared by plunder restart the clock exactly as back pay does (fix round 2, E2-6).
        if (relief > 0 && a.Arrears == 0) { a.ArrearsSinceMs = -1; _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.ArrearsCleared, relief)); }
        if (lo.S.Owner == a.S.Owner)
        {
            var own = _p[a.S.Owner];
            own.OwnPlunderGrain = checked(own.OwnPlunderGrain + grain);
            own.OwnPlunderMoney = checked(own.OwnPlunderMoney + money);
        }
        _events.Add(new CascadeEvent(now, a.S.Id, lo.S.Owner, CascadeKind.Plunder, grain));
        FamineRisk(lo, now, a);
    }

    private static long CombatDays(ArmySetup s, long pulseIndex)
    {
        long d = 0;
        foreach (var (pulse, days) in s.CombatDays) if (pulse == pulseIndex) d = checked(d + days);
        return d;
    }

    private static long ArrearsMonths(Army a, long now) => a.Arrears > 0 && a.ArrearsSinceMs >= 0 ? Math.Max(0, now - a.ArrearsSinceMs) / (30 * Day) : 0;

    private static long MulDivCeil128(long a, long b, long c, long d)
    {
        if (c <= 0) return 0;
        Int128 n = (Int128)a * b * c;
        return (long)((n + d - 1) / d);
    }

    // ------------------------------------------------------------------------------------------------ sub-step settlement
    private void SettleSubStep(SubStepRecord x)
    {
        foreach (int id in _armyKey)
        {
            var a = _a[id];
            if (a.S.WarId != x.WarId || a.Cond == ArmyCondition.Disbanded || !a.Planned) continue;
            if (a.PulseIndex != x.PulseIndex) throw new InvalidOperationException("sub-step of a pulse that was not planned");
            var p = _p[a.S.Owner];
            long dt = x.EndMs - x.StartMs, w = a.PulseEnd - a.PulseStart;
            long food = RateIntegrator.IntegrateExact(a.FoodTotal, dt, w, ref a.FoodC);
            _l.Transfer(a.Train, _grainEaten, food);
            long fuel = RateIntegrator.IntegrateExact(a.FuelTotal, dt, w, ref a.FuelC);
            _l.Transfer(a.FuelTank, _fuelBurnt, fuel);
            a.FuelUsed += fuel;
            long coal = RateIntegrator.IntegrateExact(a.CoalTotal, dt, w, ref a.CoalC);
            _l.Transfer(a.Bunker, _coalBurnt, coal);
            a.CoalUsed += coal;
            _l.Transfer(a.Ammo, _munSpent, RateIntegrator.IntegrateExact(a.AmmoTotal, dt, w, ref a.AmmoC));
            _l.Transfer(a.KitTrain, _kitWorn, RateIntegrator.IntegrateExact(a.KitTotal, dt, w, ref a.KitC));
            long adv = RateIntegrator.IntegrateExact(a.DistTotal, dt, w, ref a.DistC);
            a.Position = Math.Min(a.S.Route.Length, a.Position + adv);

            bool coin = p.S.Capability.Has(WarTech.Coinage);
            int chest = coin ? a.PayChest : a.WageChest;
            long due = RateIntegrator.IntegrateExact(a.PayRateScaled, dt, a.PayLen, ref a.PayCarry);
            long paid = Math.Min(due, _l.Balance(chest));
            _l.Transfer(chest, coin ? p.Purse : _inKindWages, paid);
            a.Arrears = checked(a.Arrears + due - paid);
            a.PayDue += due;
            a.PayPaid += paid;

            long des = Math.Min(_l.Balance(a.Men), RateIntegrator.IntegrateExact(a.DesMilli, dt, checked(w * 1000), ref a.DesC));
            _l.Transfer(a.Men, a.Deserting, des);
            long died = Math.Min(_l.Balance(a.Men), RateIntegrator.IntegrateExact(a.DisMilli, dt, checked(w * 1000), ref a.DisC));
            _l.Transfer(a.Men, _dead, died);
            long dh = Math.Min(_l.Balance(a.Horses), RateIntegrator.IntegrateExact(a.AniHMilli, dt, checked(w * 1000), ref a.AniHC));
            _l.Transfer(a.Horses, _animalsDead, dh);
            long dd = Math.Min(_l.Balance(a.Draft), RateIntegrator.IntegrateExact(a.AniDMilli, dt, checked(w * 1000), ref a.AniDC));
            _l.Transfer(a.Draft, _animalsDead, dd);
            a.Deserted += des;
            a.Died += died;
            a.AnimalsDied += dh + dd;
        }
    }

    // ------------------------------------------------------------------------------------------------ pulse end: discrete updates
    private void OnPulseEnd(int warId, long pulseIndex, long now)
    {
        foreach (int id in _armyKey)
        {
            var a = _a[id];
            if (a.S.WarId != warId || a.Cond == ArmyCondition.Disbanded || !a.Planned) continue;
            if (a.PulseIndex != pulseIndex || a.PulseEnd != now) throw new InvalidOperationException("pulse end of an unplanned pulse");
            var p = _p[a.S.Owner];
            long w = a.PulseEnd - a.PulseStart;
            // Deserters reach home at the pulse end (they walk; production sees them from the next latch).
            _l.Transfer(a.Deserting, p.Civ, _l.Balance(a.Deserting));
            // New arrears date from the instant the chest ran dry inside this pulse (E-V8), not from the pulse start.
            if (a.Arrears > 0 && a.ArrearsSinceMs < 0) { a.ArrearsSinceMs = Math.Min(a.DryAtMs, now); _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.ArrearsBegan, a.Arrears)); }
            long ageMonths = ArrearsMonths(a, a.PulseStart);
            bool ok = a.HungerPpm == 0 && a.Arrears == 0 && a.Cond == ArmyCondition.Loyal;
            // Morale and cohesion (micro units): per-year rates times the exact pulse length (Law 3).
            long dm = (ok ? Scale(_r.MoraleRecoveryPerYear * 1000, w) : 0)
                      - Scale128(_r.MoraleHungerPenaltyPerYear * 1000, a.HungerPpm, w)
                      - Scale(_r.MoraleArrearsPenaltyPerMonthPerYear * 1000 * ageMonths, w);
            long dc = (ok ? Scale(_r.CohesionRecoveryPerYear * 1000, w) : 0)
                      - Scale128(_r.CohesionHungerPenaltyPerYear * 1000, a.HungerPpm, w)
                      - Scale(_r.CohesionArrearsPenaltyPerMonthPerYear * 1000 * ageMonths, w);
            a.Morale = Math.Clamp(a.Morale + dm, 0, 1000 * 1000);
            long cohesionBefore = a.Cohesion;
            a.Cohesion = Math.Clamp(a.Cohesion + dc, 0, 1000 * 1000);
            // The instant cohesion reached zero: interpolated on the pulse's linear path (Law 3, E-V1: disbandment waits a
            // duration at zero, not a number of pulses).
            if (a.Cohesion > 0) a.ZeroCohesionSinceMs = -1;
            else if (a.ZeroCohesionSinceMs < 0)
                a.ZeroCohesionSinceMs = cohesionBefore == 0 || dc >= 0 ? a.PulseStart : a.PulseStart + (long)((Int128)w * cohesionBefore / -dc);
            if (a.Cond == ArmyCondition.Loyal && a.Cohesion < _r.MutinyCohesionThreshold * 1000)
            {
                a.Cond = ArmyCondition.Mutinous;
                _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.MutinyBegan, a.Arrears));
            }
            else if (a.Cond == ArmyCondition.Mutinous && a.HungerPpm == 0 && a.Arrears <= MonthOfPay(a))
            {
                a.Cond = ArmyCondition.Loyal;
                a.Cohesion = Math.Max(a.Cohesion, _r.MutinyRecoveredCohesion * 1000);
                a.ZeroCohesionSinceMs = -1;
                a.ArrearsSinceMs = a.Arrears > 0 ? now : -1;   // the settlement restarts the clock on what is still owed
                _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.MutinyEnded, 0));
            }
            if (a.S.March && a.Position >= a.S.Route.Length && a.DistTotal > 0) _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.Arrived, a.Position));
            long men = _l.Balance(a.Men);
            var condEnd = a.Cond;
            if (men * 1000 <= a.InitialMen * _r.DisbandStrengthPermille
                || (a.Cond == ArmyCondition.Mutinous && a.ZeroCohesionSinceMs >= 0 && now - a.ZeroCohesionSinceMs >= _r.DisbandZeroCohesionDays * Day))
                condEnd = ArmyCondition.Disbanded;
            _rows.Add(new ArmyPulseRow(warId, a.S.Id, a.PulseIndex, a.PulseStart, a.PulseEnd, a.CondStart, a.MenStart, men, a.NS, a.NM,
                a.Foraged, a.Seized, a.Shipped, a.Delivered, a.Loss, a.Stage, a.FoodTotal, a.HungerPpm, a.MovePpm, a.DistTotal, a.Position,
                a.PayDue, a.PayPaid, a.BackPay, a.Arrears, a.Morale, a.Cohesion, condEnd, a.Deserted, a.Died, a.AnimalsDied,
                a.MunNeed, a.AmmoTotal, a.KitReplaced, a.KitDeficit, a.FuelUsed, a.DesertPm, a.DiseasePm, a.SupplyQ32, a.CoalUsed, a.ScurvyPm));
            if (condEnd == ArmyCondition.Disbanded) Disband(a, p, now);
            a.Planned = false;
        }
    }

    /// <summary>One month (30 days) of the army's current pay: a mutiny is settled once the back pay is down to this
    /// (mutineers demanded their arrears; the current month's pay then runs on as usual).</summary>
    private static long MonthOfPay(Army a) => a.PayLen == 0 ? 0 : (long)((Int128)a.PayRateScaled * 30 * Day / a.PayLen);

    private static long Scale(long perYear, long w) => FixedMath.MulDivFloor(perYear, w, Y);
    private static long Scale128(long perYear, long ppm, long w) => (long)((Int128)perYear * ppm * w / ((Int128)Micro * Y));

    /// <summary>Disbandment: the men go home, the animals to the polity's herds, the stores back to its stocks.</summary>
    private void Disband(Army a, Polity p, long now)
    {
        a.Cond = ArmyCondition.Disbanded;
        long men = _l.Balance(a.Men);
        _l.Transfer(a.Men, p.Civ, men);
        _l.Transfer(a.Horses, p.Livestock, _l.Balance(a.Horses));
        _l.Transfer(a.Draft, p.Livestock, _l.Balance(a.Draft));
        _l.Transfer(a.Train, p.Granary, _l.Balance(a.Train));
        _l.Transfer(a.WageChest, p.Granary, _l.Balance(a.WageChest));
        _l.Transfer(a.PayChest, p.Treasury, _l.Balance(a.PayChest));
        _l.Transfer(a.Ammo, p.MunStock, _l.Balance(a.Ammo));
        _l.Transfer(a.KitTrain, p.KitStock, _l.Balance(a.KitTrain));
        _l.Transfer(a.FuelTank, p.FuelStock, _l.Balance(a.FuelTank));
        _l.Transfer(a.Bunker, p.CoalStock, _l.Balance(a.Bunker));
        a.MenLatched = 0;
        if (a.OccupiedLoc >= 0)
        {
            // The occupation ends with the force: the victim's trade share returns from this instant.
            var victim = _p[_loc[a.OccupiedLoc].S.Owner];
            Touch(victim, now);
            _events.Add(new CascadeEvent(now, a.S.Id, victim.S.Id, CascadeKind.OccupationEnded, a.OccupiedLoc));
            a.OccupiedLoc = -1;
            Relatch(victim);
        }
        _events.Add(new CascadeEvent(now, a.S.Id, a.S.Owner, CascadeKind.Disbanded, men));
    }

    // ------------------------------------------------------------------------------------------------ turn start: campaign settlement
    private void OnTurnStart(long turn, long now)
    {
        foreach (var p in _p)
        {
            Touch(p, now);
            long dt = now - p.LastSettleMs;
            long civ = _l.Balance(p.Civ);
            long due = RateIntegrator.IntegrateExact(checked(civ * _r.RationBase), dt, Y, ref p.CivCarry);
            long eaten = Math.Min(due, _l.Balance(p.Granary));
            _l.Transfer(p.Granary, _civEaten, eaten);
            long famine = Math.Min(civ, FixedMath.MulDivFloor(due - eaten, _r.FamineMortalityPermille, 1000 * 1000));
            _l.Transfer(p.Civ, _dead, famine);
            if (famine > 0) _events.Add(new CascadeEvent(now, -1, p.S.Id, CascadeKind.CivilianFamine, famine));
            long gDemand = RateIntegrator.IntegrateExact(checked(civ * p.S.GrowthPerCapitaYear), dt, Y, ref p.GrowthCarry);
            long gGranted = Math.Min(gDemand, _l.Balance(p.Granary));
            _l.Transfer(p.Granary, _growthUsed, gGranted);

            // Finance: interest accrued over the turn is due now; failure to pay is a default that closes credit (the lenders
            // stop the transfers that paid the armies: Spain 1575 -> unpaid Army of Flanders -> Antwerp 1576, reports §2).
            long interest = p.InterestAccrued, paidInt = Math.Min(interest, _l.Balance(p.Treasury));
            _l.Transfer(p.Treasury, p.Lender, paidInt);
            p.InterestAccrued = 0;
            if (paidInt < interest)
            {
                p.Debt = checked(p.Debt + interest - paidInt);
                if (!p.CreditClosed) { p.CreditClosed = true; _events.Add(new CascadeEvent(now, -1, p.S.Id, CascadeKind.CreditDefault, interest - paidInt)); }
            }
            long borrowed = 0;
            if (p.S.Capability.Has(WarTech.Banking) && !p.CreditClosed)
            {
                long next = NextSettlement(now) - now;
                long need = 0;
                foreach (var a in _a)
                    if (a.S.Owner == p.S.Id && a.Cond != ArmyCondition.Disbanded)
                        need = checked(need + a.Arrears + (long)((Int128)a.PayRateScaled * next / Math.Max(1, a.PayLen)));
                long revenuePerYear = p.RevenueRateScaled / 1000;
                long limit = FixedMath.MulDivFloor(revenuePerYear, _r.CreditLimitPermilleOfRevenue, 1000);
                borrowed = Math.Min(Math.Max(0, need - _l.Balance(p.Treasury)), Math.Min(Math.Max(0, limit - p.Debt), _l.Balance(p.Lender)));
                if (borrowed > 0)
                {
                    _l.Transfer(p.Lender, p.Treasury, borrowed);
                    p.Debt = checked(p.Debt + borrowed);
                    _events.Add(new CascadeEvent(now, -1, p.S.Id, CascadeKind.Borrowed, borrowed));
                }
            }
            _turnRows.Add(new TurnSettlementRow(turn, now, p.S.Id, civ, due, eaten, famine, gDemand, gGranted, interest, paidInt, borrowed, p.Debt, p.CreditClosed));
            p.LastSettleMs = now;
            Relatch(p);
        }
        SettleLocalities(now);
    }

    private long _locLastSettle;

    private void SettleLocalities(long now)
    {
        long dt = now - _locLastSettle;
        _locLastSettle = now;
        if (dt <= 0) return;
        foreach (var lo in _loc)
        {
            long civ = _l.Balance(lo.Civ);
            long prod = RateIntegrator.IntegrateExact(checked(civ * _r.HarvestPerCapitaYear), dt, Y, ref lo.ProdCarry);
            _l.Transfer(_harvestSource, lo.Grain, prod);
            long due = RateIntegrator.IntegrateExact(checked(civ * _r.RationBase), dt, Y, ref lo.EatCarry);
            long eaten = Math.Min(due, _l.Balance(lo.Grain));
            _l.Transfer(lo.Grain, _civEaten, eaten);
            long famine = Math.Min(civ, FixedMath.MulDivFloor(due - eaten, _r.FamineMortalityPermille, 1000 * 1000));
            _l.Transfer(lo.Civ, _dead, famine);
            if (famine > 0) _events.Add(new CascadeEvent(now, -1, lo.S.Owner, CascadeKind.LocalityFamineDeaths, famine));
        }
    }

    // ------------------------------------------------------------------------------------------------ hashes
    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendArmySide(h, includeTurnKeyed: true);
        foreach (var p in _p)
        {
            L(h, p.LastTouchMs); L(h, p.LastSettleMs); L(h, p.HarvestRateScaled); L(h, p.RevenueRateScaled); L(h, p.Debt);
            L(h, p.OwnPlunderGrain); L(h, p.OwnPlunderMoney);
            F(h, p.HarvestCarry); F(h, p.RevenueCarry); F(h, p.InterestCarry); F(h, p.CivCarry); F(h, p.GrowthCarry);
            L(h, p.InterestAccrued); L(h, p.CreditClosed ? 1 : 0);
        }
        // Fix round 2, E2-9: the locality settlement clock and carries, and the allocation decisions (turn-keyed inputs).
        L(h, _locLastSettle);
        foreach (var lo in _loc) { F(h, lo.ProdCarry); F(h, lo.EatCarry); }
        L(h, _allocs.Count);
        foreach (var x in _allocs)
        {
            L(h, x.AtMs); L(h, x.Polity); L(h, x.WarId); L(h, x.PulseIndex);
            var al = x.Allocation;
            L(h, (long)al.Ordering); L(h, al.Free); L(h, al.CivilianReserve); L(h, al.CivilianProtected); L(h, al.CivilianShortfall);
            L(h, al.GrowthDemand); L(h, al.GrowthLeft); L(h, al.MilitaryRequested); L(h, al.MilitaryGranted); L(h, al.Unallocated); L(h, (long)al.Stage);
            foreach (var g in al.Grants) { L(h, g.ArmyId); L(h, g.Priority); L(h, g.Standing); L(h, g.Movement); L(h, g.Restock); L(h, g.Wages); }
            foreach (var q in x.Requests) { L(h, q.ArmyId); L(h, q.Priority); L(h, q.Standing); L(h, q.Movement); L(h, q.Restock); L(h, q.Wages); }
        }
        _l.AppendTo(h);
        L(h, _turnRows.Count);
        foreach (var t in _turnRows)
        {
            L(h, t.Turn); L(h, t.AtMs); L(h, t.Polity); L(h, t.Civilians); L(h, t.CivilianDue); L(h, t.CivilianEaten); L(h, t.FamineDeaths);
            L(h, t.GrowthDemand); L(h, t.GrowthGranted); L(h, t.InterestDue); L(h, t.InterestPaid); L(h, t.Borrowed); L(h, t.Debt); L(h, t.CreditClosed ? 1 : 0);
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }

    /// <summary>The war side only: army stores, condition, rows and cascade events (no ledger-wide balances, no campaign
    /// settlement rows). It is a function of pulse boundaries alone when the granary does not bind, so it must not depend
    /// on where turn boundaries fall (crossing invariance).</summary>
    public string ArmySideHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendArmySide(h, includeTurnKeyed: false);
        return Convert.ToHexString(h.GetHashAndReset());
    }

    private void AppendArmySide(IncrementalHash h, bool includeTurnKeyed)
    {
        foreach (var a in _a)
        {
            foreach (int acc in new[] { a.Men, a.Horses, a.Draft, a.Train, a.PayChest, a.WageChest, a.Ammo, a.KitTrain, a.FuelTank, a.Deserting, a.Bunker })
                L(h, _l.Balance(acc));
            L(h, a.FreshSinceMs); L(h, a.OccupiedLoc);
            L(h, a.Position); L(h, a.Morale); L(h, a.Cohesion); L(h, a.Arrears); L(h, a.ArrearsSinceMs); L(h, a.ZeroCohesionSinceMs);
            L(h, (long)a.Cond); L(h, a.KitDeficit); F(h, a.StandCarry); F(h, a.MoveCarry); F(h, a.PayCarry);
            F(h, a.MunDrillCarry); F(h, a.KitTimeCarry); F(h, a.KitDistCarry);
            // Fix round 2, E2-9: the in-pulse plan and its accumulators (they decide the rest of the pulse).
            L(h, a.Planned ? 1 : 0); L(h, a.PulseIndex); L(h, a.PulseStart); L(h, a.PulseEnd); L(h, a.MenLatched); L(h, a.HorsesLatched); L(h, a.DraftLatched);
            L(h, a.FoodTotal); L(h, a.FuelTotal); L(h, a.AmmoTotal); L(h, a.KitTotal); L(h, a.DistTotal); L(h, a.CoalTotal);
            L(h, a.PayRateScaled); L(h, a.PayLen); L(h, a.DryAtMs);
            L(h, a.DesertPm); L(h, a.DiseasePm); L(h, a.AnimalPm); L(h, a.ScurvyPm); L(h, a.DesMilli); L(h, a.DisMilli); L(h, a.AniHMilli); L(h, a.AniDMilli);
            F(h, a.FoodC); F(h, a.FuelC); F(h, a.AmmoC); F(h, a.KitC); F(h, a.DistC); F(h, a.CoalC); F(h, a.DesC); F(h, a.DisC); F(h, a.AniHC); F(h, a.AniDC);
            L(h, (long)a.CondStart); L(h, a.MenStart); L(h, a.NS); L(h, a.NM); L(h, a.Foraged); L(h, a.Seized); L(h, a.Shipped); L(h, a.Delivered);
            L(h, a.Loss); L(h, a.HungerPpm); L(h, a.MovePpm); L(h, a.PayDue); L(h, a.PayPaid); L(h, a.BackPay); L(h, a.Deserted); L(h, a.Died);
            L(h, a.AnimalsDied); L(h, a.MunNeed); L(h, a.KitReplaced); L(h, a.FuelUsed); L(h, a.SupplyQ32); L(h, a.CoalUsed); L(h, (long)a.Stage);
        }
        L(h, _rows.Count);
        foreach (var x in _rows)
        {
            L(h, x.WarId); L(h, x.ArmyId); L(h, x.PulseIndex); L(h, x.StartMs); L(h, x.EndMs); L(h, (long)x.ConditionAtStart);
            L(h, x.MenStart); L(h, x.MenEnd); L(h, x.StandingNeed); L(h, x.MovementNeed); L(h, x.Foraged); L(h, x.Seized);
            L(h, x.Shipped); L(h, x.Delivered); L(h, x.CarriageLoss); L(h, (long)x.Stage); L(h, x.FoodEaten); L(h, x.HungerPpm); L(h, x.MovementPpm);
            L(h, x.DistanceM); L(h, x.PositionEnd); L(h, x.PayDue); L(h, x.PayPaid); L(h, x.BackPay); L(h, x.ArrearsEnd);
            L(h, x.MoraleEnd); L(h, x.CohesionEnd); L(h, (long)x.ConditionEnd); L(h, x.Deserted); L(h, x.DiseaseDeaths);
            L(h, x.AnimalDeaths); L(h, x.MunitionsNeed); L(h, x.MunitionsUsed); L(h, x.KitReplaced); L(h, x.KitDeficit);
            L(h, x.FuelUsed); L(h, x.DesertionPermilleYear); L(h, x.DiseasePermilleYear); L(h, x.SupplyQ32Raw);
            L(h, x.CoalUsed); L(h, x.ScurvyPermilleYear);
        }
        foreach (var e in _events)
        {
            if (!includeTurnKeyed && IsTurnKeyed(e.Kind)) continue;
            L(h, e.AtMs); L(h, e.ArmyId); L(h, e.Polity); L(h, (long)e.Kind); L(h, e.Value);
        }
    }

    /// <summary>Events produced by the campaign settlement at turn starts (they depend on the turn calendar by design).</summary>
    public static bool IsTurnKeyed(CascadeKind k)
        => k is CascadeKind.CivilianFamine or CascadeKind.Borrowed or CascadeKind.CreditDefault or CascadeKind.LocalityFamineDeaths;

    private static void L(IncrementalHash h, long v)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, v);
        h.AppendData(b);
    }

    private static void F(IncrementalHash h, ExactFraction f) { L(h, f.Num); L(h, f.Den); }
}
