using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;

namespace Proto.M7.War.Outcomes;

/// <summary>The result of an outcome order: Reason 0 = applied, otherwise an <see cref="OutcomeRefusal"/> code (no state change).</summary>
public readonly record struct OutcomeOrderResult(int LogIndex, int Issuer, int Kind, long AtMs, int Reason);

/// <summary>Ledger balances at a campaign-turn start (conservation and sub-stepping checks).</summary>
public sealed record OutcomeTurnSnapshot(long Turn, long AtMs, long[] Balances);

/// <summary>
/// Phase C lane W world: war outcomes. The leader-execution transition (DIR5 §4.1), the post-battle choices (§4.2, PROPOSAL),
/// the military record (§4.3) and the post-battle collapse of battle entities into formation counts (§2.9). Every person, coin,
/// gm of grain and building moves only through <see cref="WarLedger"/> transfers (Law 1; exact totals, no tolerance).
///
/// REUSED, NOT REBUILT. Settlements can be BOUND to lane S installations: this world then wraps lane S's
/// <see cref="SiegeWorld"/> (it is the event world the runner drives; every lane S event and order is delegated to it unchanged)
/// and shares its ledger, so lane S's siege endings ARE the captures here (control passes only through lane S's EndSiege), the
/// garrison lane S takes prisoner enters this world's custody, a sack moves lane S's civilians and granary, the refugees go to
/// lane S's "elsewhere" account and the victors' treasuries and home granaries are lane S's. A sack takes movable wealth at lane
/// E's forced plunder rate through lane E's <see cref="StockDraw"/>; civilian deaths and flight are lane E's competing head-loss
/// flows (<see cref="HeadcountLoss"/>); prisoners eat lane E's base ration (<see cref="LogisticsRates.RationBase"/>), integrated
/// exactly with T1's <see cref="RateIntegrator.IntegrateExact"/> and split at campaign-turn starts (T2's sub-stepping). The
/// occupation grievance a sack leaves is the INPUT of lane S's occupation unrest (InstallationSpec.GrievancePermillePerYear),
/// derived here from the treatment (lane S flagged it as a bare input). Battle results come from lane TR / lane N's tactical
/// engine (<see cref="BattleCollapse.FromTactical"/>; the TR end reasons, Standoff included, are carried as end codes).
///
/// THE LEADER RULE (C31; the existing rules on the polity afterwards are stated, not invented):
///   "Cities" are the settlements a polity controls that are cities (IsCity; lane S InstallationKind.City). C31's READING is "the
///   settlements the polity controls"; excluding forts, harbours, airports and depots is THIS LANE's narrower reading (open
///   question DQ7; fix r1, W-R1-7). A polity whose city count was ≥ 1 at the last settled instant and is 0 now has lost all its
///   cities: its leader is executed AT THAT INSTANT (fate Executed, a Ledger flow of the leader's person to the executed sink, a
///   fate record with the instant, turn, the war of the capture that took the last city and that war's War Pulse; the polity
///   leaves its wars as Eliminated, and a war whose whole side is eliminated ends in Conquest). A polity whose leader is already
///   dead (executed as a captive) still leaves its wars as Eliminated at that instant; no second fate is recorded (fix r1, W-R1-1).
///   THE WAR OF A RECORD (fix r1, W-R1-5): a battle row and the fate it causes name the same war (the capture's). A capture is
///   recorded in war w only if both parties are current participants of w on opposite sides and w is ongoing (the capture device
///   refuses anything else; a lane S capture takes the lowest-id such war, or none). Fix r2 (W-R2-5, W-R2-9): the same holds for the
///   other records at their instant: a field battle names its declared war only if both still fight it at the battle's end, a raised
///   siege takes the war as a lane S capture does, and a captive-leader decision names the captivity's war only while both still
///   fight it; otherwise the record names no war.
///   SETTLEMENT OF AN INSTANT. All control changes made by world events at instant T (class &lt; Resolve) are settled together by
///   one ControlSettle event at (T, Resolve): simultaneous losses give one execution; a polity that loses its last city and takes
///   another at the same instant ends T with a city and keeps its leader (the simultaneity rule of the event classes: all damage
///   lands, then deaths resolve). A change made by an ORDER at T (class PlayerOrder, after every world event) is settled when that
///   order completes (orders are sequential in log order, ADR-061). A loss at T1 and a re-capture later (T2 &gt; T1, the same pulse
///   or not) is an execution at T1 that stands: a fate never reverts.
///   CAPTURE vs RAZE. You raze what you hold: a raze is the victor's disposition of a settlement it has just taken, so the
///   former owner lost it at the capture instant either way, and the fate record names the settlement, whatever its disposition.
///   A raze of the razer's own last city is refused (it would make the razer lose all its cities by its own choice). A seat razed by
///   its own polity (fix r2, W-R2-3) was taken by no one: the leader moves to the polity's lowest-id city; it is never its own captive.
///   AFTERWARDS (existing rules, nothing added): extinction is DERIVED, as in Sim.Core EmpireQuery.IsExtinct, where an Empire is
///   extinct while it controls no settlement and is not extinct once it regains one; HERE the test is "controls no city" (the
///   lane's reading above, DQ7: a polity holding only a fort is extinct here); its identity persists (a row may remain for an
///   extinct polity); "A player with zero territories loses" (ADR-019 §7.3); its claims are inherited, not deleted (D-037 C6, M8);
///   a ruler's final settlement never revolts away (D-048 ruling 5), so only war triggers the rule; M8 owns the diplomatic
///   consequences. No succession rule exists: a polity that regains a city after the execution stays leaderless (open question).
///
/// EVENTS (world classes; ties by priority then source):
///   (T, System, 6) Capture (test device), BattleEnded (field battle result) · lane S events at their own priorities 0-5
///   (T, System, 7) TurnStart: prisoner rations settle up to T; ledger snapshot
///   (T, Resolve, 0) ControlSettle: the leader rule, seats taken, AI dispositions, war ends
///   (T, Significant) SettlementTaken (0), LeaderFateChanged (1), BattleResolved (2), Probe (3): the post-battle window
/// ORDERS: one-shot transactions applied AT the window instant T at which they are issued (T3's rule for agent actions); standing
/// terms and offers take effect at T and stay until replaced. A disposition is offered only at the capture instant.
/// </summary>
public sealed class WarOutcomeWorld : IEventWorld
{
    private sealed class Pol
    {
        public OutcomePolity Spec = null!;
        public LeaderFate Fate;
        public int Seat, HeldBy = -1;
        public long CitiesSettled;
        public int LastLossBy = -1, LastLossSettlement = -1, LastLossWar = -1;
        public long MenTerms, LeaderTerms;
        public int Treasury, Granary, Troops, Refugees, Paroled, Impressed, Enslaved, Leader;
        public int[] Accounts => new[] { Treasury, Granary, Troops, Refugees, Paroled, Impressed, Enslaved, Leader };
    }

    private sealed class Set
    {
        public OutcomeSettlement Spec = null!;
        public int Controller;
        public bool Razed;
        public int Civ, Grain, Money, Buildings;
        public long Grievance;
        public long TakenAtMs = -1;
        public int TakenBy = -1, TakenFrom = -1, TakenBattle = -1, TakenWar = -1;
        public bool Disposed;
        public long Sackers;
        public int OutcomeRow = -1;
    }

    private sealed class Cust
    {
        public int Captor, Origin, Account;
        public long SettledMs, Arrears, Offer;
        public ExactFraction Carry;
    }

    private sealed class WarS
    {
        public OutcomeWar Spec = null!;
        public long EndMs = -1;
        public WarOutcome Outcome;
        public int Winner = -1;
        /// <summary>A cache of the latest pulse looked up (not state: results never depend on it).</summary>
        public WarPulse Cursor;
        public bool HasCursor;
    }

    private sealed class Part
    {
        public int WarId, Polity, Side;
        public long JoinedMs, LeftMs = -1;
        public ParticipantExit Exit;
    }

    private sealed class Form
    {
        public OutcomeFormation Spec = null!;
        public int Account, Wounded;
    }

    private const long Day = SimInstant.MsPerDay;
    private const int SourceBase = 2_000_000;

    private readonly CampaignCalendar _cal;
    private readonly WarPulseSchedule _schedule;
    private readonly OutcomeCoefficients _k;
    private readonly LogisticsRates _r;
    private readonly CaptiveExecutionRule _captiveRule;
    private readonly RazePopulationRule _razeRule;
    private readonly int[] _ai;
    private readonly SiegeWorld? _siege;
    private readonly WarLedger _ledger;
    private readonly Pol[] _p;
    private readonly Set[] _x;
    private readonly WarS[] _w;
    private readonly List<Part> _parts = new();
    private readonly Cust?[] _cust;
    private readonly Form[] _f;
    private readonly int _kia, _civKilled, _deserted, _executed, _ruins, _grainBurnt, _prisonerFood;
    private readonly List<BattleRow> _battles = new();
    private readonly List<BattleSideRow> _sides = new();
    private readonly List<CasualtyRow> _casualties = new();
    private readonly List<LeaderFateRow> _fates = new();
    private readonly List<SettlementOutcomeRow> _outcomes = new();
    private readonly List<PrisonerRow> _prisonerRows = new();
    private readonly List<OutcomeOrderResult> _orders = new();
    private readonly List<OutcomeTurnSnapshot> _snapshots = new();
    private readonly List<FieldBattle> _fieldBattles = new();
    private readonly List<bool> _fieldBattleDone = new();
    private readonly List<long> _fieldBattleEnd = new();
    private long _settleAt = -1;
    private int _siegeSeen, _siegeEndSeen;

    /// <param name="captiveExecution">DIRECTOR-LEVEL (C32 flag): may a captor execute a captive leader outside the leader rule.
    /// Every caller passes one; there is no default.</param>
    /// <param name="razePopulation">DIRECTOR-LEVEL (C32 flag, fix r1 W-R1-8): does a raze kill or displace the population. Every
    /// caller passes one; there is no default.</param>
    /// <param name="aiDisposition">Per polity: -1 = human (it chooses at the window); otherwise the <see cref="Disposition"/> its
    /// doctrine takes at the capture instant (through the same eligibility checks; a refused choice falls back to Occupy).</param>
    /// <param name="rates">Lane E's table (forced plunder rate, base ration); null = its PROPOSAL, or the siege world's own when bound.</param>
    /// <param name="siege">Lane S world to wrap and bind to (its ledger becomes this world's ledger). Null = standalone.</param>
    public WarOutcomeWorld(CampaignCalendar calendar, WarPulseSchedule schedule, OutcomePolity[] polities, OutcomeSettlement[] settlements,
                           OutcomeWar[] wars, OutcomeFormation[] formations, OutcomeCoefficients coefficients, CaptiveExecutionRule captiveExecution,
                           RazePopulationRule razePopulation, int[]? aiDisposition = null, LogisticsRates? rates = null, SiegeWorld? siege = null)
    {
        coefficients.Validate();
        if (captiveExecution != CaptiveExecutionRule.NotPermitted && captiveExecution != CaptiveExecutionRule.Permitted)
            throw new ArgumentOutOfRangeException(nameof(captiveExecution));
        if (razePopulation != RazePopulationRule.Displace && razePopulation != RazePopulationRule.KillThenDisplace)
            throw new ArgumentOutOfRangeException(nameof(razePopulation));
        int n = polities.Length;
        if (n <= 0 || n > 64) throw new ArgumentOutOfRangeException(nameof(polities), "1..64 polities");
        _cal = calendar; _schedule = schedule; _k = coefficients; _captiveRule = captiveExecution; _razeRule = razePopulation; _siege = siege;
        _r = rates ?? siege?.Rates ?? LogisticsRates.Proposal();
        _ledger = siege?.Ledger ?? new WarLedger();
        _ai = aiDisposition is null ? Filled(n, -1) : (int[])aiDisposition.Clone();
        if (_ai.Length != n) throw new ArgumentException("one AI disposition per polity");
        foreach (var d in _ai) if (d < -1 || d > (int)Disposition.Raze) throw new ArgumentOutOfRangeException(nameof(aiDisposition));

        polities = SortById(polities, x => x.Id);
        settlements = SortById(settlements, x => x.Id);
        wars = SortById(wars, x => x.Id);
        formations = SortById(formations, x => x.Id);
        CheckIds(polities, x => x.Id, "polity"); CheckIds(settlements, x => x.Id, "settlement"); CheckIds(wars, x => x.Id, "war"); CheckIds(formations, x => x.Id, "formation");

        // Accounts are opened in id order (polities, custody, settlements, formations, sinks): numbering never depends on input order.
        _p = new Pol[n];
        for (int i = 0; i < n; i++)
        {
            var s = polities[i];
            if (s.Treasury < 0 || s.Granary < 0 || s.HomeTroops < 0) throw new ArgumentOutOfRangeException(nameof(polities));
            if (s.LeaderId < 0) throw new ArgumentException($"polity {s.Id}: every empire has a leader (DIR5 §4.1 presupposes one)");
            var p = new Pol { Spec = s, Seat = s.Seat, Fate = LeaderFate.Reigning };
            if (siege is not null)
            {
                if (s.Treasury != 0 || s.Granary != 0 || s.HomeTroops != 0) throw new ArgumentException("bound: money, grain and troops are lane S's");
                p.Treasury = siege.TreasuryOf(i); p.Granary = siege.HomeFoodOf(i); p.Troops = siege.HomeTroopsOf(i); p.Refugees = siege.ElsewhereOf(i);
            }
            else
            {
                p.Treasury = _ledger.Open(Commodity.Money, s.Treasury); p.Granary = _ledger.Open(Commodity.Grain, s.Granary);
                p.Troops = _ledger.Open(Commodity.People, s.HomeTroops); p.Refugees = _ledger.Open(Commodity.People, 0);
            }
            p.Paroled = _ledger.Open(Commodity.People, 0); p.Impressed = _ledger.Open(Commodity.People, 0); p.Enslaved = _ledger.Open(Commodity.People, 0);
            p.Leader = _ledger.Open(Commodity.People, 1);   // the leader is one person
            _p[i] = p;
        }
        _cust = new Cust?[n * n];
        for (int c = 0; c < n; c++)
            for (int o = 0; o < n; o++)
                if (c != o) _cust[c * n + o] = new Cust { Captor = c, Origin = o, Account = _ledger.Open(Commodity.People, 0) };

        _x = new Set[settlements.Length];
        for (int i = 0; i < settlements.Length; i++)
        {
            var s = settlements[i];
            if (s.Controller < 0 || s.Controller >= n || s.PopulationPolity < -1 || s.PopulationPolity >= n) throw new ArgumentOutOfRangeException(nameof(settlements));
            if (s.Civilians < 0 || s.Grain < 0 || s.Money < 0 || s.Buildings < 0 || s.BaseGrievancePermilleYear < 0) throw new ArgumentOutOfRangeException(nameof(settlements));
            var x = new Set { Spec = s, Controller = s.Controller };
            if (s.Installation >= 0)
            {
                if (siege is null) throw new ArgumentException($"settlement {s.Id} is bound but no siege world is given");
                if (s.Civilians != 0 || s.Grain != 0) throw new ArgumentException("bound: civilians and grain are lane S's");
                for (int j = 0; j < i; j++) if (_x[j].Spec.Installation == s.Installation) throw new ArgumentException("installation bound twice");
                var inst = siege.InstallationsInternal[s.Installation];
                if (siege.ControllerOf(s.Installation) != s.Controller) throw new ArgumentException($"settlement {s.Id}: controller differs from lane S");
                if (s.IsCity != (inst.Spec.Kind == InstallationKind.City)) throw new ArgumentException($"settlement {s.Id}: city flag differs from lane S's kind");
                // fix r2 (W-R2-1): whose people live there is lane S's too (its occupation unrest reads it); the two worlds must agree
                int lanePopulation = inst.Spec.PopulationPolity >= 0 ? inst.Spec.PopulationPolity : inst.Spec.Controller;
                if (PopulationOf(x) != lanePopulation) throw new ArgumentException($"settlement {s.Id}: population polity differs from lane S's");
                x.Civ = siege.CivilianAccount(s.Installation); x.Grain = siege.GranaryAccount(s.Installation);
            }
            else
            {
                x.Civ = _ledger.Open(Commodity.People, s.Civilians); x.Grain = _ledger.Open(Commodity.Grain, s.Grain);
            }
            x.Money = _ledger.Open(Commodity.Money, s.Money);
            x.Buildings = _ledger.Open(Commodity.Structures, s.Buildings);
            _x[i] = x;
        }
        foreach (var p in _p)
            if (p.Seat >= 0 && (p.Seat >= _x.Length || _x[p.Seat].Controller != p.Spec.Id)) throw new ArgumentException($"polity {p.Spec.Id}: its seat is not its own settlement");
        // fix r2 (W-R2-4): the leader rule counts the cities this world sees; a lane S city it does not see would be lost unnoticed
        if (siege is not null)
            foreach (var li in siege.InstallationsInternal)
            {
                if (li.Spec.Kind != InstallationKind.City) continue;
                bool seen = false;
                foreach (var y in _x) if (y.Spec.Installation == li.Spec.Id) seen = true;
                if (!seen) throw new ArgumentException($"lane S city installation {li.Spec.Id} is not bound to a settlement: the leader rule would not see it");
            }

        _f = new Form[formations.Length];
        for (int i = 0; i < formations.Length; i++)
        {
            var s = formations[i];
            if (s.Owner < 0 || s.Owner >= n || s.Personnel < 0) throw new ArgumentOutOfRangeException(nameof(formations));
            _f[i] = new Form { Spec = s, Account = _ledger.Open(Commodity.People, s.Personnel), Wounded = _ledger.Open(Commodity.People, 0) };
        }

        _w = new WarS[wars.Length];
        for (int i = 0; i < wars.Length; i++)
        {
            var s = wars[i];
            _w[i] = new WarS { Spec = s };
            for (int side = 0; side < 2; side++)
                foreach (var q in side == 0 ? s.Side0 : s.Side1)
                {
                    if (q < 0 || q >= n) throw new ArgumentOutOfRangeException(nameof(wars));
                    foreach (var e in _parts) if (e.WarId == s.Id && e.Polity == q) throw new ArgumentException($"war {s.Id}: polity {q} on both sides or twice");
                    _parts.Add(new Part { WarId = s.Id, Polity = q, Side = side, JoinedMs = s.StartMs });
                }
        }

        _kia = _ledger.Open(Commodity.People, 0);
        _civKilled = _ledger.Open(Commodity.People, 0);
        _deserted = _ledger.Open(Commodity.People, 0);
        _executed = _ledger.Open(Commodity.People, 0);
        _ruins = _ledger.Open(Commodity.Structures, 0);
        _grainBurnt = _ledger.Open(Commodity.Grain, 0);
        _prisonerFood = _ledger.Open(Commodity.Grain, 0);
        foreach (var p in _p) p.CitiesSettled = CityCount(p.Spec.Id);
    }

    private static int[] Filled(int n, int v) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = v; return a; }

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

    private static void CheckIds<T>(T[] sorted, Func<T, int> id, string what)
    {
        for (int i = 0; i < sorted.Length; i++)
            if (id(sorted[i]) != i) throw new ArgumentException($"{what} ids must be 0..n-1, each once");
    }

    // ================================================================== read surface

    public WarLedger Ledger => _ledger;
    public SiegeWorld? Siege => _siege;
    public int PolityCount => _p.Length;
    public LeaderFate FateOf(int polity) => _p[polity].Fate;
    public int HeldBy(int polity) => _p[polity].HeldBy;
    public int SeatOf(int polity) => _p[polity].Seat;
    public int ControllerOf(int settlement) => _x[settlement].Controller;
    public bool IsRazed(int settlement) => _x[settlement].Razed;
    public long GrievanceOf(int settlement) => _x[settlement].Grievance;
    /// <summary>Cities the polity controls now (razed settlements are no city).</summary>
    public long CityCount(int polity)
    {
        long c = 0;
        foreach (var x in _x) if (x.Spec.IsCity && !x.Razed && x.Controller == polity) c++;
        return c;
    }
    /// <summary>DERIVED (not a stored flag), like Sim.Core EmpireQuery.IsExtinct; but that query is "controls no settlement", and this
    /// one is "controls no city" (forts, harbours, airports and depots do not keep a polity alive here): the lane's reading, DQ7.</summary>
    public bool IsExtinct(int polity) => CityCount(polity) == 0;
    public IReadOnlyList<LeaderFateRow> Fates => _fates;
    public IReadOnlyList<BattleRow> Battles => _battles;
    public IReadOnlyList<BattleSideRow> BattleSides => _sides;
    public IReadOnlyList<CasualtyRow> Casualties => _casualties;
    public IReadOnlyList<SettlementOutcomeRow> SettlementOutcomes => _outcomes;
    public IReadOnlyList<PrisonerRow> PrisonerRows => _prisonerRows;
    public IReadOnlyList<OutcomeOrderResult> OrderResults => _orders;
    public IReadOnlyList<OutcomeTurnSnapshot> TurnSnapshots => _snapshots;
    public int TreasuryOf(int p) => _p[p].Treasury;
    public int GranaryOf(int p) => _p[p].Granary;
    public int TroopsOf(int p) => _p[p].Troops;
    public int RefugeesOf(int p) => _p[p].Refugees;
    public int ParoledOf(int p) => _p[p].Paroled;
    public int ImpressedOf(int p) => _p[p].Impressed;
    public int EnslavedOf(int p) => _p[p].Enslaved;
    public int LeaderAccountOf(int p) => _p[p].Leader;
    public int CustodyOf(int captor, int origin) => (_cust[captor * _p.Length + origin] ?? throw new ArgumentException("no custody of one's own men")).Account;
    public long FoodArrearsOf(int captor, int origin) => _cust[captor * _p.Length + origin]!.Arrears;
    public int CivilianAccount(int s) => _x[s].Civ;
    public int GrainAccount(int s) => _x[s].Grain;
    public int MoneyAccount(int s) => _x[s].Money;
    public int BuildingAccount(int s) => _x[s].Buildings;
    public int FormationAccount(int f) => _f[f].Account;
    public int WoundedAccount(int f) => _f[f].Wounded;
    public int KilledSink => _kia;
    public int CiviliansKilledSink => _civKilled;
    public int DesertedSink => _deserted;
    public int ExecutedSink => _executed;
    public int RuinsSink => _ruins;
    public int GrainBurntSink => _grainBurnt;
    public int PrisonerFoodSink => _prisonerFood;
    public OutcomeCoefficients Coefficients => _k;
    public LogisticsRates Rates => _r;
    public RazePopulationRule RazeRule => _razeRule;

    /// <summary>The War Pulse of war <paramref name="warId"/>'s clock that contains instant t (-1 before the war or without a war).</summary>
    public long PulseOf(int warId, long t)
    {
        if (warId < 0) return -1;
        var w = _w[warId];
        if (t < w.Spec.StartMs) return -1;
        if (!w.HasCursor || t < w.Cursor.StartMs) { w.Cursor = _schedule.First(w.Spec.StartMs); w.HasCursor = true; }
        while (w.Cursor.EndMs <= t && _schedule.HasNext(w.Cursor)) w.Cursor = _schedule.Next(w.Cursor);
        return w.Cursor.Index;
    }

    /// <summary>The campaign turn of instant t (the last turn for an instant at or after the campaign end, as lane S records it).</summary>
    public long TurnAt(long t) => _cal.TurnOf(new SimInstant(Math.Min(t, _cal.CampaignEnd.Ms - 1)));

    // ================================================================== setup and test devices

    /// <summary>Begin the wrapped siege world (if any) and schedule the first campaign-turn start at or after now.</summary>
    public void Begin(EventScheduler s)
    {
        _siege?.Begin(s);
        long now = s.NowMs;
        if (now >= _cal.CampaignEnd.Ms) return;
        long turn = _cal.TurnOf(new SimInstant(now));
        if (_cal.TurnStart(turn).Ms < now) turn++;
        if (turn < _cal.TotalTurns) s.Schedule(_cal.TurnStart(turn).Ms, EventClass.System, 7, 0, OutcomeKinds.TurnStart, turn);
        foreach (var c in _cust) if (c is not null) c.SettledMs = now;
    }

    /// <summary>TEST DEVICE (stand-in for the M7.13 capture paths other than a lane S siege): settlement <paramref name="settlement"/>
    /// passes to <paramref name="to"/> at <paramref name="atMs"/>, in war <paramref name="warId"/>, with <paramref name="sackers"/> of the
    /// victor's men present. Bound settlements change hands only through lane S.</summary>
    public static void ScheduleCapture(EventScheduler s, long atMs, int settlement, int to, int warId, long sackers)
        => s.Schedule(atMs, EventClass.System, 6, settlement, OutcomeKinds.Capture, settlement, to, OutcomeKinds.PackCapture(warId, sackers));

    /// <summary>A finished field battle arrives at its end instant <paramref name="atMs"/> (stand-in for the M7.15 hand-off).</summary>
    public void ScheduleBattle(EventScheduler s, long atMs, FieldBattle battle)
    {
        if (battle.WarId < -1 || battle.WarId >= _w.Length) throw new ArgumentOutOfRangeException(nameof(battle));
        if (battle.HoldsField < -1 || battle.HoldsField > 1 || battle.StartMs > atMs) throw new ArgumentOutOfRangeException(nameof(battle));
        // fix r1 (W-R1-11a): the two sides' polities are checked here, not at the battle-end instant inside the event loop
        if (battle.Side0Polity < 0 || battle.Side0Polity >= _p.Length || battle.Side1Polity < 0 || battle.Side1Polity >= _p.Length)
            throw new ArgumentOutOfRangeException(nameof(battle), "no such polity");
        if (battle.Side0Polity == battle.Side1Polity) throw new ArgumentException("a battle has two different polities");
        // fix r1 (W-R1-5): a battle is recorded in a war only if its polities stand on opposite sides of it
        if (battle.WarId >= 0 && !Opposed(battle.WarId, battle.Side0Polity, battle.Side1Polity, activeOnly: false))
            throw new ArgumentException($"polities {battle.Side0Polity} and {battle.Side1Polity} are not on opposite sides of war {battle.WarId}");
        for (int i = 0; i < battle.Formations.Length; i++)
        {
            var f = battle.Formations[i];
            if (f.CampaignFormation < 0 || f.CampaignFormation >= _f.Length || f.Side < 0 || f.Side > 1) throw new ArgumentOutOfRangeException(nameof(battle));
            if (_f[f.CampaignFormation].Spec.Owner != (f.Side == 0 ? battle.Side0Polity : battle.Side1Polity))
                throw new ArgumentException($"campaign formation {f.CampaignFormation} does not belong to side {f.Side}'s polity");
            // fix r1 (W-R1-2): one collapse input per campaign formation (its whole balance is split by one input's entity counts)
            for (int j = 0; j < i; j++)
                if (battle.Formations[j].CampaignFormation == f.CampaignFormation)
                    throw new ArgumentException($"campaign formation {f.CampaignFormation} appears twice: one collapse input per campaign formation");
            // fix r2 (W-R2-2): nor in two battles at once. Battle k occupies its formations over (start, end]; two spans that share
            // more than a boundary instant, or that end at the same instant (the order of two collapses at one instant would decide
            // the split), overlap. A battle that starts at or after another's end is fought by what that battle left (sequential).
            for (int k = 0; k < _fieldBattles.Count; k++)
            {
                var o = _fieldBattles[k];
                long oEnd = _fieldBattleEnd[k];
                if (oEnd != atMs && Math.Max(o.StartMs, battle.StartMs) >= Math.Min(oEnd, atMs)) continue;
                foreach (var g in o.Formations)
                    if (g.CampaignFormation == f.CampaignFormation)
                        throw new ArgumentException($"campaign formation {f.CampaignFormation} is in field battle {k} over an overlapping span: one battle at a time");
            }
        }
        _fieldBattles.Add(battle);
        _fieldBattleEnd.Add(atMs);
        _fieldBattleDone.Add(false);
        s.Schedule(atMs, EventClass.System, 6, SourceBase + _fieldBattles.Count - 1, OutcomeKinds.BattleEnded, _fieldBattles.Count - 1);
    }

    /// <summary>TEST DEVICE: open a decision window at atMs.</summary>
    public static void ScheduleProbe(EventScheduler s, long atMs) => s.Schedule(atMs, EventClass.Significant, 3, 0, OutcomeKinds.Probe);

    /// <summary>The significant kinds of this world (with lane S's when bound) for the runner.</summary>
    public static SignificantKinds Significant(bool withSiege) => withSiege
        ? new SignificantKinds(OutcomeKinds.SettlementTaken, OutcomeKinds.LeaderFateChanged, OutcomeKinds.BattleResolved, OutcomeKinds.Probe,
                               SiegeKinds.Probe, SiegeKinds.SiegeEnded, SiegeKinds.AssaultResolved)
        : new SignificantKinds(OutcomeKinds.SettlementTaken, OutcomeKinds.LeaderFateChanged, OutcomeKinds.BattleResolved, OutcomeKinds.Probe);

    public bool IsLive(EntityRef entity) => _siege?.IsLive(entity) ?? true;

    // ================================================================== event dispatch

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        long now = ev.Key.TimeMs;
        if (!OutcomeKinds.IsOwn(ev.Kind))
        {
            if (_siege is null) throw new InvalidOperationException($"unknown kind {ev.Kind}");
            _siege.Execute(in ev, s);       // lane S unchanged; then pick up what it did
            ObserveSiege(now, s);
            return;
        }
        if (ev.Key.Class == (byte)EventClass.PlayerOrder) { ExecuteOrder(in ev, now, s); return; }
        switch (ev.Kind)
        {
            case OutcomeKinds.TurnStart: OnTurnStart(ev.A, now, s); break;
            case OutcomeKinds.Capture: OnCapture(in ev, now, s); break;
            case OutcomeKinds.ControlSettle: Settle(now, s); break;
            case OutcomeKinds.BattleEnded: OnBattleEnded((int)ev.A, now, s); break;
            case OutcomeKinds.SettlementTaken:
            case OutcomeKinds.LeaderFateChanged:
            case OutcomeKinds.BattleResolved:
            case OutcomeKinds.Probe:
                break;
            default: throw new InvalidOperationException($"unknown kind {ev.Kind}");
        }
    }

    private void OnTurnStart(long turn, long now, EventScheduler s)
    {
        foreach (var c in _cust) if (c is not null) SettleCustody(c, now);
        _snapshots.Add(new OutcomeTurnSnapshot(turn, now, _ledger.Snapshot()));
        if (turn + 1 < _cal.TotalTurns) s.Schedule(_cal.TurnStart(turn + 1).Ms, EventClass.System, 7, 0, OutcomeKinds.TurnStart, turn + 1);
    }

    private void OnCapture(in ScheduledEvent ev, long now, EventScheduler s)
    {
        if (ev.A < 0 || ev.A >= _x.Length) throw new InvalidOperationException("capture: no such settlement");
        var x = _x[(int)ev.A];
        int to = (int)ev.B;
        int warRaw = (int)(ev.C % 65_536);
        int war = warRaw == OutcomeKinds.NoWar ? -1 : warRaw;
        long sackers = ev.C / 65_536;
        if (x.Spec.Installation >= 0) throw new InvalidOperationException("a bound settlement changes hands only through lane S");
        if (x.Razed) throw new InvalidOperationException("a razed settlement cannot be taken");
        if (to < 0 || to >= _p.Length || to == x.Controller) throw new InvalidOperationException("capture: bad victor");
        if (war >= _w.Length) throw new InvalidOperationException("capture: no such war");
        int from = x.Controller;
        // fix r1 (W-R1-5b): a capture in war w needs both parties as current participants of w on opposite sides, w ongoing
        if (war >= 0 && !Opposed(war, from, to, activeOnly: true))
            throw new InvalidOperationException($"capture: polities {from} and {to} are not opposed in war {war} (allies, not in it, one has left it, or it has ended)");
        x.Controller = to;
        Take(x, from, to, now, war, sackers, BattleKind.Storming, BattleMechanism.StandIn, 0, now, sackers, 0, 0, 0, s);
    }

    /// <summary>Lane S's captures since the last look: bind them into this world at their instant.</summary>
    private void ObserveSiege(long now, EventScheduler s)
    {
        var caps = _siege!.Captures;
        while (_siegeSeen < caps.Count)
        {
            var c = caps[_siegeSeen++];
            if (c.AtMs != now) throw new InvalidOperationException("lane S capture observed at another instant");
            int war = ActiveWarBetween(c.To, c.From);   // fix r1 (W-R1-5): only a war in which both are opposed now
            // The garrison lane S took prisoner enters custody (exactly that capture's prisoners, from lane S's per-captor account).
            if (c.Prisoners > 0)
            {
                var cu = CustodyState(c.To, c.From);
                SettleCustody(cu, now);
                _ledger.Transfer(_siege.PrisonersOf(c.To), cu.Account, c.Prisoners);
            }
            int siegeId = -1;
            foreach (var e in _siege.Ends)
                if (e.AtMs == now && _siege.SiegesInternal[e.SiegeId].Spec.InstallationId == c.InstallationId) siegeId = e.SiegeId;
            if (siegeId < 0) throw new InvalidOperationException("lane S capture without its siege end");
            var sx = _siege.SiegesInternal[siegeId];
            long attackers = _siege.AttackerPeople(siegeId);
            Set? bound = null;
            foreach (var x in _x) if (x.Spec.Installation == c.InstallationId) bound = x;
            if (bound is not null)
            {
                if (bound.Controller != c.From || _siege.ControllerOf(c.InstallationId) != c.To) throw new InvalidOperationException("bound control drift");
                bound.Controller = c.To;
                Take(bound, c.From, c.To, now, war, attackers, BattleKind.Siege, BattleMechanism.SiegeHealth, (byte)c.Cause, sx.Spec.StartMs, sx.A0, sx.G0,
                     c.Prisoners, c.MarchedOut, s);
            }
            else
            {
                // An unbound installation (a fort, a depot): no city changed hands, but the record and the prisoners stand.
                int b = AddBattle(war, BattleKind.Siege, BattleMechanism.SiegeHealth, (byte)c.Cause, sx.Spec.StartMs, now, -1, 0);
                _sides.Add(new BattleSideRow(b, 0, c.To, 0, sx.A0));
                _sides.Add(new BattleSideRow(b, 1, c.From, 0, sx.G0));
                AddCasualty(b, 1, c.From, CasualtyCause.Captured, c.Prisoners);
                AddCasualty(b, 1, c.From, CasualtyCause.MarchedOut, c.MarchedOut);
            }
            if (c.Prisoners > 0) _prisonerRows.Add(new PrisonerRow(_prisonerRows.Count, now, c.To, c.From, (byte)PrisonerAction.Intake, c.Prisoners, 0, _battles.Count - 1));
        }
        // fix r2 (W-R2-9): a siege that ends WITHOUT a capture (Relieved, Lifted, CapLifted) is a battle of the record too: side 0 the
        // besieger, side 1 the defender, who kept the place (winner side 1); the war is chosen as for a capture; no casualty is
        // attributable (lane S's loss sinks are shared by both sides, limitation 4) and nothing changes hands.
        var ends = _siege.Ends;
        while (_siegeEndSeen < ends.Count)
        {
            var e = ends[_siegeEndSeen++];
            if (e.AtMs != now) throw new InvalidOperationException("lane S siege end observed at another instant");
            var sx = _siege.SiegesInternal[e.SiegeId];
            int inst = sx.Spec.InstallationId;
            bool captured = false;
            foreach (var c in caps) if (c.AtMs == now && c.InstallationId == inst) captured = true;
            if (captured) continue;   // recorded above, as the capture
            int besieger = sx.Spec.Attacker, defender = _siege.ControllerOf(inst);
            int settlement = -1;
            foreach (var x in _x) if (x.Spec.Installation == inst) settlement = x.Spec.Id;
            int b = AddBattle(ActiveWarBetween(besieger, defender), BattleKind.Siege, BattleMechanism.SiegeHealth, (byte)e.Outcome, sx.Spec.StartMs, now,
                              settlement, 1);
            _sides.Add(new BattleSideRow(b, 0, besieger, 0, sx.A0));
            _sides.Add(new BattleSideRow(b, 1, defender, 0, sx.G0));
        }
    }

    /// <summary>Settlement x passed from <paramref name="from"/> to <paramref name="to"/> at now (control already set): the record,
    /// the open post-battle choice, and the settlement of the instant.</summary>
    private void Take(Set x, int from, int to, long now, int war, long sackers, BattleKind kind, BattleMechanism mech, byte endCode, long startMs,
                      long attackersStart, long defendersStart, long prisoners, long marched, EventScheduler s)
    {
        var pf = _p[from];
        pf.LastLossBy = to;
        pf.LastLossSettlement = x.Spec.Id;
        pf.LastLossWar = war;
        int b = AddBattle(war, kind, mech, endCode, startMs, now, x.Spec.Id, 0);   // side 0 = the taker, side 1 = the defender
        _sides.Add(new BattleSideRow(b, 0, to, 0, attackersStart));
        _sides.Add(new BattleSideRow(b, 1, from, 0, defendersStart));
        AddCasualty(b, 1, from, CasualtyCause.Captured, prisoners);
        AddCasualty(b, 1, from, CasualtyCause.MarchedOut, marched);
        x.TakenAtMs = now; x.TakenBy = to; x.TakenFrom = from; x.TakenBattle = b; x.TakenWar = war; x.Disposed = false; x.Sackers = sackers;
        x.Grievance = to == PopulationOf(x) ? 0 : x.Spec.BaseGrievancePermilleYear;
        x.OutcomeRow = _outcomes.Count;
        _outcomes.Add(new SettlementOutcomeRow(x.Spec.Id, b, now, from, to, (byte)Disposition.Occupy, 0, 0, 0, 0, 0, 0, x.Grievance));
        RequestSettle(now, s);
    }

    private int AddBattle(int war, BattleKind kind, BattleMechanism mech, byte endCode, long startMs, long endMs, int settlement, int winnerSide)
    {
        int b = _battles.Count;
        _battles.Add(new BattleRow(b, war, (byte)kind, (byte)mech, endCode, startMs, endMs, TurnAt(endMs), PulseOf(war, endMs), settlement, winnerSide));
        return b;
    }

    private void AddCasualty(int battle, int side, int polity, CasualtyCause cause, long count)
    {
        if (count > 0) _casualties.Add(new CasualtyRow(battle, (byte)side, polity, (byte)cause, count));
    }

    private static int PopulationOf(Set x) => x.Spec.PopulationPolity >= 0 ? x.Spec.PopulationPolity : x.Spec.Controller;

    /// <summary>The lowest-id war in which a and b stand on opposite sides (-1: none).</summary>
    public int WarBetween(int a, int b)
    {
        foreach (var w in _w)
        {
            int sa = -1, sb = -1;
            foreach (var e in _parts)
            {
                if (e.WarId != w.Spec.Id) continue;
                if (e.Polity == a) sa = e.Side;
                if (e.Polity == b) sb = e.Side;
            }
            if (sa >= 0 && sb >= 0 && sa != sb) return w.Spec.Id;
        }
        return -1;
    }

    /// <summary>Do a and b stand on opposite sides of war <paramref name="warId"/>? With <paramref name="activeOnly"/>: both still take
    /// part (neither has left it) and the war is ongoing.</summary>
    private bool Opposed(int warId, int a, int b, bool activeOnly)
    {
        if (warId < 0 || warId >= _w.Length) return false;
        if (activeOnly && _w[warId].Outcome != WarOutcome.Ongoing) return false;
        int sa = -1, sb = -1;
        foreach (var e in _parts)
        {
            if (e.WarId != warId || (activeOnly && e.LeftMs >= 0)) continue;
            if (e.Polity == a) sa = e.Side;
            if (e.Polity == b) sb = e.Side;
        }
        return sa >= 0 && sb >= 0 && sa != sb;
    }

    /// <summary>The lowest-id ongoing war in which a and b both still take part on opposite sides (-1: none).</summary>
    public int ActiveWarBetween(int a, int b)
    {
        foreach (var w in _w) if (Opposed(w.Spec.Id, a, b, activeOnly: true)) return w.Spec.Id;
        return -1;
    }

    // ================================================================== the settlement of an instant: the leader rule

    private void RequestSettle(long now, EventScheduler s)
    {
        if (s.CurrentKey.TimeMs == now && s.CurrentKey.Class < (byte)EventClass.Resolve)
        {
            if (_settleAt != now)
            {
                s.Schedule(now, EventClass.Resolve, 0, 0, OutcomeKinds.ControlSettle);
                _settleAt = now;
            }
        }
        else Settle(now, null);   // a change made by an order (or another late-class event): settled when it completes
    }

    private void Settle(long now, EventScheduler? s)
    {
        var changed = new List<int>();
        // 1. DIR5 §4.1: lost all its cities at this instant (had ≥ 1 at the last settled instant, has 0 now).
        foreach (var p in _p)
        {
            if (p.CitiesSettled < 1 || CityCount(p.Spec.Id) != 0) continue;
            if (p.Fate != LeaderFate.Executed)
            {
                ExecuteLeader(p.Spec.Id, now, FateCause.LastCityLost, p.LastLossBy, p.LastLossSettlement, p.LastLossWar);
                changed.Add(p.Spec.Id);
            }
            else Eliminate(p.Spec.Id, now);   // fix r1 (W-R1-1): its leader is already dead (no second fate), but it has lost all its cities
        }
        // 2. A leader whose seat was taken while its polity still holds a city is the taker's captive.
        foreach (var p in _p)
        {
            if (p.Fate != LeaderFate.Reigning || p.Seat < 0) continue;
            var seat = _x[p.Seat];
            if (seat.Controller == p.Spec.Id && !seat.Razed) continue;
            // fix r2 (W-R2-3): a seat its own polity has razed (it took the city at this instant and razed it; only the taker may) was
            // not taken by anyone: no captor, no fate change; the leader moves to the polity's lowest-id city, as a released one does
            if (seat.Razed && seat.TakenBy == p.Spec.Id) { p.Seat = LowestCity(p.Spec.Id); continue; }
            int captor = seat.TakenBy;
            int war = seat.TakenWar;   // fix r1 (W-R1-5): the war of the capture that took the seat (the battle row's war)
            p.Fate = LeaderFate.Captive; p.HeldBy = captor; p.Seat = -1;
            _fates.Add(new LeaderFateRow(_fates.Count, p.Spec.Id, p.Spec.LeaderId, (byte)LeaderFate.Reigning, (byte)LeaderFate.Captive, (byte)FateCause.SeatTaken,
                                         now, TurnAt(now), war, PulseOf(war, now), captor, -1, seat.Spec.Id));
            changed.Add(p.Spec.Id);
        }
        // 3. AI victors dispose of what they took at this instant, through the same checks as a player's order.
        foreach (var x in _x)
        {
            if (x.TakenAtMs != now || x.Disposed || x.Razed || _ai[x.TakenBy] < 0) continue;
            if (Dispose(x, (Disposition)_ai[x.TakenBy], now, x.TakenBy) != 0) Dispose(x, Disposition.Occupy, now, x.TakenBy);
        }
        CheckWarEnds(now);
        foreach (var p in _p) p.CitiesSettled = CityCount(p.Spec.Id);
        if (s is not null)
        {
            foreach (var x in _x) if (x.TakenAtMs == now) s.Schedule(now, EventClass.Significant, 0, x.Spec.Id, OutcomeKinds.SettlementTaken);
            foreach (int q in changed) s.Schedule(now, EventClass.Significant, 1, q, OutcomeKinds.LeaderFateChanged);
        }
    }

    private void ExecuteLeader(int polity, long now, FateCause cause, int by, int settlement, int war)
    {
        var p = _p[polity];
        var from = p.Fate;
        int held = p.HeldBy;
        p.Fate = LeaderFate.Executed; p.HeldBy = -1; p.Seat = -1;
        if (_ledger.Balance(p.Leader) > 0) _ledger.Transfer(p.Leader, _executed, 1);   // the leader is a person: death is a Ledger flow
        _fates.Add(new LeaderFateRow(_fates.Count, polity, p.Spec.LeaderId, (byte)from, (byte)LeaderFate.Executed, (byte)cause, now, TurnAt(now), war,
                                     PulseOf(war, now), by, held, settlement));
        if (cause == FateCause.LastCityLost) Eliminate(polity, now);
    }

    /// <summary>The polity lost all its cities at now: it leaves every war it still takes part in as Eliminated.</summary>
    private void Eliminate(int polity, long now)
    {
        foreach (var e in _parts)
            if (e.Polity == polity && e.LeftMs < 0) { e.LeftMs = now; e.Exit = ParticipantExit.Eliminated; }
    }

    private void CheckWarEnds(long now)
    {
        foreach (var w in _w)
        {
            if (w.Outcome != WarOutcome.Ongoing) continue;
            bool gone0 = true, gone1 = true, any0 = false, any1 = false;
            foreach (var e in _parts)
            {
                if (e.WarId != w.Spec.Id) continue;
                if (e.Side == 0) { any0 = true; gone0 &= e.LeftMs >= 0; } else { any1 = true; gone1 &= e.LeftMs >= 0; }
            }
            gone0 &= any0; gone1 &= any1;
            if (gone0 && gone1) { w.Outcome = WarOutcome.MutualCollapse; w.EndMs = now; }
            else if (gone0) { w.Outcome = WarOutcome.Conquest; w.Winner = 1; w.EndMs = now; }
            else if (gone1) { w.Outcome = WarOutcome.Conquest; w.Winner = 0; w.EndMs = now; }
        }
    }

    // ================================================================== settlement dispositions (the post-battle choice)

    /// <summary>Apply the victor's choice for settlement x at now. Returns 0 or an <see cref="OutcomeRefusal"/> code (nothing changed).</summary>
    private int Dispose(Set x, Disposition d, long now, int issuer)
    {
        if (x.TakenAtMs < 0 || x.TakenBy != issuer || x.Razed || x.Controller != issuer) return OutcomeRefusal.NotEligible;
        if (x.TakenAtMs != now) return OutcomeRefusal.WindowClosed;
        if (x.Disposed) return OutcomeRefusal.AlreadyDisposed;
        // fix r1 (W-R1-6, PROPOSAL L13): no sack or raze of one's own population (a re-capture is a liberation; lane S has no unrest
        // under one's own polity, so the harm would cost the sacker nothing)
        if (d != Disposition.Occupy && issuer == PopulationOf(x)) return OutcomeRefusal.OwnPopulation;
        if (d == Disposition.Raze && x.Spec.IsCity && CityCount(issuer) <= 1) return OutcomeRefusal.LastCity;
        x.Disposed = true;
        if (d == Disposition.Occupy) return 0;
        Plunder(x, issuer, d == Disposition.Raze, now);
        return 0;
    }

    /// <summary>
    /// Sack (raze = false) or raze. Over the customary SackDays at an intensity q = min(sackers, civilians) / civilians (1 for an
    /// empty town with sackers present): movable wealth and grain are taken at lane E's forced plunder rate × q (StockDraw), civilians
    /// die or flee at the sack rates × q as lane E's competing head-loss flows (HeadcountLoss), buildings burn at the burn rate × q.
    /// A raze then takes the rest of the money, burns the rest of the grain and every building, expels every survivor, and the
    /// settlement is no longer a city. Whether the raze also kills (the sack's kill rate) is the Director-level RazePopulationRule. The occupation grievance after a sack: base + HarmGrievance × (killed + fled) / civilians.
    /// </summary>
    private void Plunder(Set x, int issuer, bool raze, long now)
    {
        long dt = checked(_k.SackDays * Day);
        long civ0 = _ledger.Balance(x.Civ);
        long qNum, qDen;
        if (civ0 > 0) { qNum = Math.Min(x.Sackers, civ0); qDen = civ0; }
        else { qNum = x.Sackers > 0 ? 1 : 0; qDen = 1; }
        long Scaled(long rate) => (long)((Int128)rate * qNum / qDen);
        // fix r1 (W-R1-8): under RazePopulationRule.Displace a raze kills no one (the flight flow, then everyone left is expelled)
        long kill = raze && _razeRule == RazePopulationRule.Displace ? 0 : Scaled(_k.SackKillPermilleYear);
        long flee = Scaled(_k.SackFleePermilleYear), total = checked(kill + flee);
        long killed = 0, fled = 0;
        if (civ0 > 0 && total > 0)
        {
            var survival = HeadcountLoss.Survival(total, dt);
            killed = HeadcountLoss.LossMilli(civ0, kill, total, survival) / 1000;
            fled = HeadcountLoss.LossMilli(civ0, flee, total, survival) / 1000;
        }
        long lootRate = Scaled(_r.ForageForcedPermilleYear);
        long lootMoney = StockDraw.Share(_ledger.Balance(x.Money), lootRate, dt);
        long lootGrain = StockDraw.Share(_ledger.Balance(x.Grain), lootRate, dt);
        long burnt = StockDraw.Share(_ledger.Balance(x.Buildings), Scaled(_k.SackBurnPermilleYear), dt);
        int pop = PopulationOf(x);
        var v = _p[issuer];
        _ledger.Transfer(x.Civ, _civKilled, killed);
        _ledger.Transfer(x.Civ, _p[pop].Refugees, fled);
        _ledger.Transfer(x.Money, v.Treasury, lootMoney);
        _ledger.Transfer(x.Grain, v.Granary, lootGrain);
        _ledger.Transfer(x.Buildings, _ruins, burnt);
        long grainBurnt = 0;
        if (raze)
        {
            long m = _ledger.Balance(x.Money), g = _ledger.Balance(x.Grain), b = _ledger.Balance(x.Buildings), c = _ledger.Balance(x.Civ);
            _ledger.Transfer(x.Money, v.Treasury, m); lootMoney += m;
            _ledger.Transfer(x.Grain, _grainBurnt, g); grainBurnt = g;
            _ledger.Transfer(x.Buildings, _ruins, b); burnt += b;
            _ledger.Transfer(x.Civ, _p[pop].Refugees, c); fled += c;
            x.Razed = true;
            if (x.Spec.Installation < 0) x.Controller = -1;
            x.Grievance = 0;
        }
        else
        {
            x.Grievance = issuer == pop ? 0
                : civ0 <= 0 ? x.Spec.BaseGrievancePermilleYear
                : checked(x.Spec.BaseGrievancePermilleYear + (long)((Int128)_k.HarmGrievancePermilleYear * (killed + fled) / civ0));
        }
        AddCasualty(x.TakenBattle, 2, pop, CasualtyCause.CiviliansKilled, killed);
        AddCasualty(x.TakenBattle, 2, pop, CasualtyCause.CiviliansFled, fled);
        var row = _outcomes[x.OutcomeRow];
        _outcomes[x.OutcomeRow] = row with
        {
            Disposition = (byte)(raze ? Disposition.Raze : Disposition.Sack), LootMoney = lootMoney, LootGrain = lootGrain, Killed = killed, Fled = fled,
            Burnt = burnt, GrainBurnt = grainBurnt, GrievancePermilleYear = x.Grievance,
        };
    }

    // ================================================================== field battles: the collapse

    private void OnBattleEnded(int index, long now, EventScheduler s)
    {
        if (_fieldBattleDone[index]) throw new InvalidOperationException("a battle collapses once");
        _fieldBattleDone[index] = true;
        var fb = _fieldBattles[index];
        // fix r2 (W-R2-5): recorded in its war only if, at its end, that war is ongoing and both polities still take part on opposite
        // sides (the capture rule, W-R1-5); a battle that outlasts its war (a side eliminated meanwhile) is recorded outside any war
        int war = fb.WarId >= 0 && Opposed(fb.WarId, fb.Side0Polity, fb.Side1Polity, activeOnly: true) ? fb.WarId : -1;
        int b = AddBattle(war, BattleKind.Field, fb.Mechanism, fb.EndCode, fb.StartMs, now, -1, fb.HoldsField);
        var forms = new int[2];
        var start = new long[2];
        foreach (var f in fb.Formations) { forms[f.Side]++; start[f.Side] = checked(start[f.Side] + _ledger.Balance(_f[f.CampaignFormation].Account)); }
        _sides.Add(new BattleSideRow(b, 0, fb.Side0Polity, forms[0], start[0]));
        _sides.Add(new BattleSideRow(b, 1, fb.Side1Polity, forms[1], start[1]));
        var killed = new long[2]; var wounded = new long[2]; var captured = new long[2]; var deserted = new long[2];
        foreach (var input in fb.Formations)
        {
            var f = _f[input.CampaignFormation];
            int owner = f.Spec.Owner, side = input.Side;
            int captor = side == 0 ? fb.Side1Polity : fb.Side0Polity;
            var split = BattleCollapse.Split(_ledger.Balance(f.Account), input, fb.HoldsField == side);
            if (!split.Conserved) throw new InvalidOperationException("collapse split does not conserve people");
            _ledger.Transfer(f.Account, _kia, split.Dead);
            _ledger.Transfer(f.Account, f.Wounded, split.Wounded);
            _ledger.Transfer(f.Account, _deserted, split.Deserted);
            if (split.Captured > 0)
            {
                var cu = CustodyState(captor, owner);
                SettleCustody(cu, now);
                _ledger.Transfer(f.Account, cu.Account, split.Captured);
            }
            killed[side] += split.Dead; wounded[side] += split.Wounded; captured[side] += split.Captured; deserted[side] += split.Deserted;
        }
        for (int side = 0; side < 2; side++)
        {
            int pol = side == 0 ? fb.Side0Polity : fb.Side1Polity;
            AddCasualty(b, side, pol, CasualtyCause.Killed, killed[side]);
            AddCasualty(b, side, pol, CasualtyCause.Wounded, wounded[side]);
            AddCasualty(b, side, pol, CasualtyCause.Captured, captured[side]);
            AddCasualty(b, side, pol, CasualtyCause.Deserted, deserted[side]);
        }
        for (int side = 0; side < 2; side++)
            if (captured[side] > 0)
                _prisonerRows.Add(new PrisonerRow(_prisonerRows.Count, now, side == 0 ? fb.Side1Polity : fb.Side0Polity, side == 0 ? fb.Side0Polity : fb.Side1Polity,
                                                  (byte)PrisonerAction.Intake, captured[side], 0, b));
        s.Schedule(now, EventClass.Significant, 2, b, OutcomeKinds.BattleResolved);
    }

    // ================================================================== prisoners in custody

    private Cust CustodyState(int captor, int origin)
    {
        if (captor < 0 || captor >= _p.Length || origin < 0 || origin >= _p.Length || captor == origin) throw new ArgumentOutOfRangeException(nameof(captor));
        return _cust[captor * _p.Length + origin]!;
    }

    /// <summary>Prisoners eat lane E's base ration from their captor's granary (linear, exact carry; settled before every change of
    /// their number and at every campaign-turn start). What the granary cannot pay is recorded as unpaid; their mortality is attrition
    /// (M7.14) and is not modelled here.</summary>
    private void SettleCustody(Cust c, long upTo)
    {
        long dt = upTo - c.SettledMs;
        if (dt <= 0) return;
        long heads = _ledger.Balance(c.Account);
        long due = RateIntegrator.IntegrateExact(checked(heads * _r.RationBase), dt, SimInstant.MsPerYear, ref c.Carry);
        int granary = _p[c.Captor].Granary;
        long paid = Math.Min(due, _ledger.Balance(granary));
        _ledger.Transfer(granary, _prisonerFood, paid);
        c.Arrears = checked(c.Arrears + due - paid);
        c.SettledMs = upTo;
    }

    private void Move(Cust c, int to, long count, long now)
    {
        SettleCustody(c, now);
        _ledger.Transfer(c.Account, to, count);
    }

    private bool Has(int p, OutcomeInstitution i) => (_p[p].Spec.Institutions & i) == i;

    // ================================================================== orders

    private void ExecuteOrder(in ScheduledEvent ev, long now, EventScheduler s)
    {
        int log = (int)ev.Key.SourceId;
        if (ev.A < 0) { Result(log, -1, ev.Kind, now, OutcomeRefusal.BadTarget); return; }
        long issuerL = ev.A / WarKinds.PolityStride;
        int target = (int)(ev.A % WarKinds.PolityStride);
        if (issuerL >= _p.Length) { Result(log, -1, ev.Kind, now, OutcomeRefusal.BadTarget); return; }
        int issuer = (int)issuerL;
        int r = ev.Kind switch
        {
            OutcomeKinds.OrderDispose => OrderDispose(issuer, target, ev.B, now, s),
            OutcomeKinds.OrderPrisoners => OrderPrisoners(issuer, target, ev.B, now),
            OutcomeKinds.OrderRansomTerms => OrderTerms(issuer, target, ev.B),
            OutcomeKinds.OrderRansom => OrderRansom(issuer, target, ev.B, now),
            OutcomeKinds.OrderExchange => OrderExchange(issuer, target, ev.B, now),
            OutcomeKinds.OrderLeader => OrderLeader(issuer, target, ev.B, now),
            _ => OutcomeRefusal.UnknownKind,
        };
        Result(log, issuer, ev.Kind, now, r);
    }

    private void Result(int log, int issuer, int kind, long now, int reason) => _orders.Add(new OutcomeOrderResult(log, issuer, kind, now, reason));

    private int OrderDispose(int issuer, int settlement, long value, long now, EventScheduler s)
    {
        if (settlement < 0 || settlement >= _x.Length) return OutcomeRefusal.BadTarget;
        if (value < 0 || value > (long)Disposition.Raze) return OutcomeRefusal.BadValue;
        int r = Dispose(_x[settlement], (Disposition)value, now, issuer);
        if (r == 0) RequestSettle(now, s);   // a raze changes the razer's city count (never to zero: refused above)
        return r;
    }

    private int OrderPrisoners(int captor, int origin, long value, long now)
    {
        if (origin < 0 || origin >= _p.Length || origin == captor) return OutcomeRefusal.BadTarget;
        long action = value / OutcomeKinds.ValueStride, count = value % OutcomeKinds.ValueStride;
        if (count <= 0 || action < (long)PrisonerAction.Release || action > (long)PrisonerAction.Enslave) return OutcomeRefusal.BadValue;
        var a = (PrisonerAction)action;
        if (a == PrisonerAction.Enslave && (!Has(captor, OutcomeInstitution.Slavery) || Has(captor, OutcomeInstitution.PrisonerConvention))) return OutcomeRefusal.Institution;
        if (a == PrisonerAction.Conscript && Has(captor, OutcomeInstitution.PrisonerConvention)) return OutcomeRefusal.Institution;
        var c = CustodyState(captor, origin);
        if (_ledger.Balance(c.Account) < count) return OutcomeRefusal.NotHeld;
        int to = a switch { PrisonerAction.Release => _p[origin].Paroled, PrisonerAction.Conscript => _p[captor].Impressed, _ => _p[captor].Enslaved };
        Move(c, to, count, now);
        _prisonerRows.Add(new PrisonerRow(_prisonerRows.Count, now, captor, origin, (byte)a, count, 0, -1));
        return 0;
    }

    private int OrderTerms(int issuer, int subject, long price)
    {
        if (price < 0) return OutcomeRefusal.BadValue;
        if (subject == 0) _p[issuer].MenTerms = price;
        else if (subject == 1) _p[issuer].LeaderTerms = price;
        else return OutcomeRefusal.BadTarget;
        return 0;
    }

    /// <summary>Ransom (two parties): the origin's standing terms must meet the price; as many prisoners return as the origin can pay for.</summary>
    private int OrderRansom(int captor, int origin, long price, long now)
    {
        if (origin < 0 || origin >= _p.Length || origin == captor) return OutcomeRefusal.BadTarget;
        if (price <= 0) return OutcomeRefusal.BadValue;
        if (!Has(captor, OutcomeInstitution.Coinage) || !Has(origin, OutcomeInstitution.Coinage)) return OutcomeRefusal.Institution;
        var o = _p[origin];
        if (o.MenTerms <= 0 || price > o.MenTerms) return OutcomeRefusal.NoTerms;
        var c = CustodyState(captor, origin);
        long held = _ledger.Balance(c.Account);
        if (held == 0) return OutcomeRefusal.NotHeld;
        long n = Math.Min(held, _ledger.Balance(o.Treasury) / price);
        if (n == 0) return OutcomeRefusal.CannotPay;
        long money = checked(n * price);
        _ledger.Transfer(o.Treasury, _p[captor].Treasury, money);
        Move(c, o.Troops, n, now);
        _prisonerRows.Add(new PrisonerRow(_prisonerRows.Count, now, captor, origin, (byte)PrisonerAction.Ransom, n, money, -1));
        return 0;
    }

    /// <summary>Exchange (two parties): a standing offer; when both have offered, min(offers, holdings) return each way, one for one.</summary>
    private int OrderExchange(int issuer, int other, long offer, long now)
    {
        if (other < 0 || other >= _p.Length || other == issuer) return OutcomeRefusal.BadTarget;
        if (offer < 0) return OutcomeRefusal.BadValue;
        var mine = CustodyState(issuer, other);
        var theirs = CustodyState(other, issuer);
        mine.Offer = offer;
        long k = Math.Min(Math.Min(mine.Offer, theirs.Offer), Math.Min(_ledger.Balance(mine.Account), _ledger.Balance(theirs.Account)));
        if (k <= 0) return 0;   // the offer stands
        Move(mine, _p[other].Troops, k, now);
        Move(theirs, _p[issuer].Troops, k, now);
        mine.Offer -= k; theirs.Offer -= k;
        _prisonerRows.Add(new PrisonerRow(_prisonerRows.Count, now, issuer, other, (byte)PrisonerAction.Exchange, k, 0, -1));
        _prisonerRows.Add(new PrisonerRow(_prisonerRows.Count, now, other, issuer, (byte)PrisonerAction.Exchange, k, 0, -1));
        return 0;
    }

    private int OrderLeader(int captor, int polity, long value, long now)
    {
        if (polity < 0 || polity >= _p.Length || polity == captor) return OutcomeRefusal.BadTarget;
        var q = _p[polity];
        if (q.Fate != LeaderFate.Captive) return OutcomeRefusal.NotEligible;
        if (q.HeldBy != captor) return OutcomeRefusal.NotCaptor;
        long decision = value / OutcomeKinds.ValueStride, price = value % OutcomeKinds.ValueStride;
        // fix r2 (W-R2-9): the decision ends (or ends with) the captivity: its row names the captivity's war (the war of the capture
        // that took the seat, read from the captive fate row), if that war is still ongoing with both of them in it; otherwise none
        int war = CaptivityWar(polity);
        if (war >= 0 && !Opposed(war, captor, polity, activeOnly: true)) war = -1;
        switch ((LeaderDecision)decision)
        {
            case LeaderDecision.Release:
                Free(q, now, captor, war, FateCause.Released);
                return 0;
            case LeaderDecision.Ransom:
                if (price <= 0) return OutcomeRefusal.BadValue;
                if (!Has(captor, OutcomeInstitution.Coinage) || !Has(polity, OutcomeInstitution.Coinage)) return OutcomeRefusal.Institution;
                if (q.LeaderTerms <= 0 || price > q.LeaderTerms) return OutcomeRefusal.NoTerms;
                if (_ledger.Balance(q.Treasury) < price) return OutcomeRefusal.CannotPay;
                _ledger.Transfer(q.Treasury, _p[captor].Treasury, price);
                Free(q, now, captor, war, FateCause.Ransomed);
                return 0;
            case LeaderDecision.Execute:
                if (_captiveRule != CaptiveExecutionRule.Permitted) return OutcomeRefusal.NotPermitted;
                ExecuteLeader(polity, now, FateCause.ExecutedAsCaptive, captor, -1, war);
                return 0;
            default: return OutcomeRefusal.BadValue;
        }
    }

    /// <summary>The war of the polity's current captivity: the war of its latest Reigning → Captive fate row (-1: none).</summary>
    private int CaptivityWar(int polity)
    {
        for (int i = _fates.Count - 1; i >= 0; i--)
            if (_fates[i].Polity == polity && _fates[i].To == (byte)LeaderFate.Captive) return _fates[i].WarId;
        return -1;
    }

    /// <summary>The polity's lowest-id city (-1: none).</summary>
    private int LowestCity(int polity)
    {
        foreach (var x in _x) if (x.Spec.IsCity && !x.Razed && x.Controller == polity) return x.Spec.Id;
        return -1;
    }

    /// <summary>A freed leader returns to its polity's lowest-id city (a captive's polity still holds one: else the rule executed it).</summary>
    private void Free(Pol q, long now, int captor, int war, FateCause cause)
    {
        int seat = LowestCity(q.Spec.Id);
        q.Fate = LeaderFate.Reigning; q.HeldBy = -1; q.Seat = seat;
        _fates.Add(new LeaderFateRow(_fates.Count, q.Spec.Id, q.Spec.LeaderId, (byte)LeaderFate.Captive, (byte)LeaderFate.Reigning, (byte)cause, now, TurnAt(now), war,
                                     PulseOf(war, now), captor, captor, seat));
    }

    // ================================================================== tables and hashing

    public List<WarRow> WarRows()
    {
        var list = new List<WarRow>();
        foreach (var w in _w) list.Add(new WarRow(w.Spec.Id, w.Spec.StartMs, w.EndMs, (byte)w.Outcome, w.Winner));
        return list;
    }

    public List<WarParticipantRow> ParticipantRows()
    {
        var list = new List<WarParticipantRow>();
        foreach (var e in _parts) list.Add(new WarParticipantRow(e.WarId, e.Polity, (byte)e.Side, e.JoinedMs, e.LeftMs, (byte)e.Exit));
        return list;
    }

    public List<SettlementStateRow> SettlementRows()
    {
        var list = new List<SettlementStateRow>();
        foreach (var x in _x)
            list.Add(new SettlementStateRow(x.Spec.Id, x.Spec.IsCity, x.Controller, x.Razed, PopulationOf(x), x.Spec.Installation, x.Civ, x.Grain, x.Money,
                                            x.Buildings, x.Grievance, x.TakenAtMs, x.TakenBy, x.TakenFrom, x.TakenBattle, x.TakenWar, x.Disposed, x.Sackers));
        return list;
    }

    public List<PolityStateRow> PolityRows()
    {
        var list = new List<PolityStateRow>();
        foreach (var p in _p)
            list.Add(new PolityStateRow(p.Spec.Id, p.Spec.LeaderId, (byte)p.Fate, p.Seat, p.HeldBy, p.CitiesSettled, p.LastLossBy, p.LastLossSettlement,
                                        p.LastLossWar, (uint)p.Spec.Institutions, p.MenTerms, p.LeaderTerms, p.Accounts));
        return list;
    }

    public List<CustodyRow> CustodyRows()
    {
        var list = new List<CustodyRow>();
        foreach (var c in _cust)
            if (c is not null) list.Add(new CustodyRow(c.Captor, c.Origin, c.Account, c.SettledMs, c.Carry.Num, c.Carry.Den, c.Arrears, c.Offer));
        return list;
    }

    /// <summary>Every lane W table, canonically encoded, in a fixed order (the record export and the state hash's source).</summary>
    public List<byte[]> EncodedTables() => new()
    {
        OutcomeCodec.EncodeTable(WarRows(), OutcomeCodec.WarRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(ParticipantRows(), OutcomeCodec.ParticipantRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(_battles, OutcomeCodec.BattleRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(_sides, OutcomeCodec.BattleSideRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(_casualties, OutcomeCodec.CasualtyRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(_fates, OutcomeCodec.LeaderFateRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(_outcomes, OutcomeCodec.SettlementOutcomeRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(_prisonerRows, OutcomeCodec.PrisonerRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(SettlementRows(), OutcomeCodec.SettlementStateRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(PolityRows(), OutcomeCodec.PolityStateRowBytes, OutcomeCodec.Encode),
        OutcomeCodec.EncodeTable(CustodyRows(), OutcomeCodec.CustodyRowBytes, OutcomeCodec.Encode),
    };

    /// <summary>The military record alone (wars, participants, battles, sides, casualties, leader fates, settlement outcomes, prisoners).</summary>
    public string RecordHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var t = EncodedTables();
        for (int i = 0; i < 8; i++) h.AppendData(t[i]);
        return Convert.ToHexString(h.GetHashAndReset());
    }

    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        if (_siege is not null) h.AppendData(System.Text.Encoding.ASCII.GetBytes(_siege.StateHashHex()));   // includes the shared ledger
        else _ledger.AppendTo(h);
        foreach (var t in EncodedTables()) h.AppendData(t);
        L(_orders.Count);
        foreach (var o in _orders) { L(o.LogIndex); L(o.Issuer); L(o.Kind); L(o.AtMs); L(o.Reason); }
        L(_snapshots.Count);
        foreach (var x in _snapshots) { L(x.Turn); L(x.AtMs); foreach (var v in x.Balances) L(v); }
        L(_fieldBattleDone.Count);
        foreach (var d in _fieldBattleDone) L(d ? 1 : 0);
        // fix r2 (W-R2-10): a scheduled battle's inputs (its end instant and collapse inputs) decide its future casualties: world state
        for (int i = 0; i < _fieldBattles.Count; i++)
        {
            var fb = _fieldBattles[i];
            L(_fieldBattleEnd[i]); L(fb.WarId); L(fb.StartMs); L(fb.Side0Polity); L(fb.Side1Polity); L((long)fb.Mechanism); L(fb.EndCode); L(fb.HoldsField);
            L(fb.Formations.Length);
            foreach (var f in fb.Formations)
            {
                L(f.CampaignFormation); L(f.Side); L(f.MoraleEnd); L(f.MoraleMax); L(f.EntityCounts.Length);
                foreach (var n in f.EntityCounts) L(n);
            }
        }
        L(_settleAt); L(_siegeSeen); L(_siegeEndSeen);
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
