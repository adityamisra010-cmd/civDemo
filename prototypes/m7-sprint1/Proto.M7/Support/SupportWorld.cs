using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Morale;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.WarEcon;

namespace Proto.M7.Support;

/// <summary>
/// [D] Panel-managed support capabilities (DIR5 §2.12; D-079 DR29, §4 D8 support part), composed over lane E's
/// <see cref="WarEconomyWorld"/> (which composes T2's world): COMPOSITION, NOT A SECOND MODEL. Support never appears on a
/// battlefield (no battle entity, no deployment, nothing rendered): it is a per-army allocation from the owner's pool.
///
/// THE PANEL. A polity issues <see cref="SupportKinds.OrderAllocate"/> orders through the Runner (delivered at a decision-window
/// instant, class PlayerOrder, after every world event of that instant). An accepted order sets the army's TARGET for one kind
/// (medical staff, or support wagons); it is a standing target until changed.
///
/// ALLOCATION PER WAR PULSE (why: docs/m7-plan/support-capabilities-proposal.md §3). The target is applied only at the army's
/// next pulse start, inside lane E's plan (<see cref="ISupportOverlay.Latch"/>, called in lane E's (Priority, Id) army order):
/// the army's limit at its men at that instant caps it; an increase is granted from the pool as far as the pool allows (armies
/// latched earlier at the same instant are served first); a decrease returns assets to the pool. The latched support holds for
/// the whole pulse: every battle of the pulse sees it, and nothing moves it in between (one pool cannot serve two armies in one
/// pulse). An order issued AT a pulse-start instant is delivered after that pulse was planned, so it applies from the next one.
///
/// EFFECTS (coefficients inside existing equations; <see cref="SupportMath"/>):
///   supply train → lane E's train capacity, supply-line fleet throughput, march speed and fodder (lane E's own arithmetic);
///   field medical → the battle collapse (lane W's Dead → Wounded save) and the care of the wounded (return to duty, death of
///   wounds) at per-sim-year rates compounded per day; and, through the save, part of the casualty-driven morale loss.
/// COSTS run through lane E's per-pulse settlement: support staff eat the field ration and draw a foot soldier's pay, the
/// wounded eat the base ration, support animals eat rest feed, roughage and working feed (all from the army's own train).
/// MORALE. A battle lowers the army's campaign morale by its net loss on the TacticalResolver's scale (start − end, mapped to micro
/// units), less the restore of the saved, and never raises it ([D, verification round 2, D-R2-01]: the round-1 rule set it to the
/// end morale, which the in-battle recovery can lift above the start); afterwards lane E's pulse-end recovery and penalties apply as
/// before (hunger, arrears).
/// [MOR, D-082 DA1] Superseded for the winning side: DR29's decrease above stands for every participant, and an army whose side the
/// battle report names the winner also gains the DA1 rise (<see cref="CampaignMorale"/>; docs/m7-plan/morale-proposal.md §4), both in
/// one clamp; each settlement is recorded as a <see cref="MoraleSettlementRow"/>. The in-battle recovery's gain is still not carried.
/// CONSERVATION. Every person, animal and wagon moves by a ledger transfer between accounts of lane E's ledger (pools, army
/// attachments, the wounded, the dead, the captured sink, the civilians); totals are constant (tests: exact equality).
/// </summary>
public sealed class SupportWorld : IEventWorld, ISupportOverlay
{
    private sealed class PoolS { public int Medics, Wagons, Draft, Drivers; }

    private sealed class ArmyS
    {
        public ArmySetup S = null!;
        public int Medics, Wagons, Draft, Drivers, Wounded;
        public readonly long[] Target = new long[3];        // indexed by (int)SupportKind
        public long Coverage, CareFromMs, RecCarry, DieCarry;
        public bool Returned;
    }

    private readonly WarEconomyWorld _econ;
    private readonly WarLedger _l;
    private readonly SupportRates _r;
    private readonly PoolS[] _pool;
    private readonly ArmyS[] _a;
    private readonly int _captured;
    private readonly List<SupportBattle> _battles = new();
    private readonly List<SupportOrderRow> _orders = new();
    private readonly List<SupportAllocationRow> _allocs = new();
    private readonly List<SupportCareRow> _care = new();
    private readonly List<SupportBattleRow> _battleRows = new();
    private readonly MoraleRates _m;                                      // [MOR]
    private readonly List<MoraleSettlementRow> _moraleRows = new();       // [MOR]

    /// <summary>[MOR] <paramref name="morale"/>: the DA1 coefficients (null = <see cref="MoraleRates.Proposal"/>).</summary>
    public SupportWorld(WarTheaterWorld inner, CampaignCalendar turnCalendar, WarEconSetup setup, SupportPool[] pools, SupportRates rates,
                        MoraleRates? morale = null)
    {
        _r = rates;
        rates.Validate();
        _m = morale ?? MoraleRates.Proposal();                          // [MOR]
        _m.Validate();
        if (setup.Support is not null) throw new ArgumentException("the setup already carries a support overlay");
        _econ = new WarEconomyWorld(inner, turnCalendar, setup with { Support = this });
        _l = _econ.Ledger;
        _captured = _l.Open(Commodity.People, 0);
        _pool = new PoolS[setup.Polities.Length];
        foreach (var p in pools)
        {
            if (p.Polity < 0 || p.Polity >= _pool.Length || _pool[p.Polity] is not null) throw new ArgumentException("one pool per polity");
            if (p.Medics < 0 || p.Wagons < 0 || p.DraftAnimals < 0 || p.Drivers < 0) throw new ArgumentOutOfRangeException(nameof(pools));
        }
        for (int i = 0; i < _pool.Length; i++)
        {
            SupportPool? src = null;
            foreach (var p in pools) if (p.Polity == i) src = p;
            _pool[i] = new PoolS
            {
                Medics = _l.Open(Commodity.People, src?.Medics ?? 0),
                Wagons = _l.Open(Commodity.Vehicles, src?.Wagons ?? 0),
                Draft = _l.Open(Commodity.Animals, src?.DraftAnimals ?? 0),
                Drivers = _l.Open(Commodity.People, src?.Drivers ?? 0),
            };
        }
        _a = new ArmyS[setup.Armies.Length];
        foreach (var s in setup.Armies)
            _a[s.Id] = new ArmyS
            {
                S = s, Medics = _l.Open(Commodity.People, 0), Wagons = _l.Open(Commodity.Vehicles, 0), Draft = _l.Open(Commodity.Animals, 0),
                Drivers = _l.Open(Commodity.People, 0), Wounded = _l.Open(Commodity.People, 0),
            };
    }

    // ------------------------------------------------------------------ public surface
    public WarEconomyWorld Economy => _econ;
    public WarLedger Ledger => _l;
    public SupportRates Rates => _r;
    public IReadOnlyList<SupportOrderRow> OrderRows => _orders;
    public IReadOnlyList<SupportAllocationRow> AllocationRows => _allocs;
    public IReadOnlyList<SupportCareRow> CareRows => _care;
    public IReadOnlyList<SupportBattleRow> BattleRows => _battleRows;
    /// <summary>[MOR] Every campaign-morale settlement at a battle's end, in execution order.</summary>
    public IReadOnlyList<MoraleSettlementRow> MoraleRows => _moraleRows;
    public MoraleRates Morale => _m;
    public int CapturedAccount => _captured;
    public long MedicsOf(int army) => _l.Balance(_a[army].Medics);
    public long WagonsOf(int army) => _l.Balance(_a[army].Wagons);
    public long WoundedOf(int army) => _l.Balance(_a[army].Wounded);
    public long CoverageOf(int army) => _a[army].Coverage;
    public long TargetOf(int army, SupportKind kind) => _a[army].Target[(int)kind];
    public long PoolMedicsOf(int polity) => _l.Balance(_pool[polity].Medics);
    public long PoolWagonsOf(int polity) => _l.Balance(_pool[polity].Wagons);

    public void Begin(EventScheduler s)
    {
        _econ.Begin(s);
        foreach (var a in _a) a.CareFromMs = s.NowMs;
    }

    public bool IsLive(EntityRef entity) => _econ.IsLive(entity);

    /// <summary>Test device and stand-in for the M7.15 battle → campaign hand-off: the battle of <paramref name="b"/>'s army ends at
    /// b.AtMs; its entities collapse then (class CampaignProcess, after the instant's tactical and pulse bookkeeping).</summary>
    public void ScheduleBattle(EventScheduler s, SupportBattle b)
    {
        if (b.ArmyId < 0 || b.ArmyId >= _a.Length) throw new ArgumentOutOfRangeException(nameof(b));
        if (b.Collapse.EntityCounts.Length != BattleCollapse.CategoryCount) throw new ArgumentException("one count per collapse category");
        if (b.MoraleStart < 0 || b.MoraleStart > b.Collapse.MoraleMax) throw new ArgumentOutOfRangeException(nameof(b), "start morale");
        if (b.WinnerSide < -1 || b.WinnerSide > 1) throw new ArgumentOutOfRangeException(nameof(b), "winner side");                     // [MOR]
        if (b.BeatenPersons < 0 || b.WinnerSidePersons < 0) throw new ArgumentOutOfRangeException(nameof(b), "side strengths");       // [MOR]
        // [MOR, verification round 1, MOR-V1-11] a reported winner needs the strength of the side it beat: without it the DA1 rise would
        // silently be 0 (CampaignMorale.VictoryRisePpm: "a win over nobody"), so the hand-off refuses it instead of settling a rise-less win.
        if (b.WinnerSide >= 0 && b.BeatenPersons == 0) throw new ArgumentOutOfRangeException(nameof(b), "a reported winner needs the beaten side's persons");
        _battles.Add(b);
        s.Schedule(b.AtMs, EventClass.CampaignProcess, 0, b.ArmyId, SupportKinds.BattleEnded, _battles.Count - 1);
    }

    public void Execute(in ScheduledEvent ev, EventScheduler s)
    {
        long now = ev.Key.TimeMs;
        if (ev.Key.Class == (byte)EventClass.PlayerOrder && ev.Kind == SupportKinds.OrderAllocate) { OnAllocate(ev.A, ev.B, now); return; }
        if (ev.Kind == SupportKinds.BattleEnded && ev.Key.Class == (byte)EventClass.CampaignProcess) { OnBattle((int)ev.A, now); return; }
        bool pulseEnd = ev.Key.Class == (byte)EventClass.System && ev.Kind == WarKinds.PulseEnd;
        int war = pulseEnd ? (int)ev.Key.SourceId : -1;
        // The care of the war's wounded settles AT the pulse end, before lane E's pulse-end updates: men who returned to duty
        // count for its strength (disbandment) check and its row.
        if (pulseEnd)
            foreach (var a in _a)
                if (a.S.WarId == war && !a.Returned && _econ.ConditionOf(a.S.Id) != ArmyCondition.Disbanded) SettleCare(a, now);
        _econ.Execute(in ev, s);
        if (pulseEnd)
            foreach (var a in _a)
                if (a.S.WarId == war && !a.Returned && _econ.ConditionOf(a.S.Id) == ArmyCondition.Disbanded) ReturnAll(a, now);
    }

    // ------------------------------------------------------------------ the panel: allocation orders

    /// <summary>
    /// Executes one allocation order: A = Pack(polity, army), B = kind × 2^32 + target. [D, verification round 2, D-R2-04] TOTAL over
    /// every raw (A, B): an order log is replayed input, so a malformed order is refused and recorded, never thrown. Before round 2 a
    /// decoded kind outside 0..255 (any B ≤ −2^32, B ≥ 256 × 2^32) or an A whose polity did not fit an int threw an
    /// OverflowException out of the world, and NegativeTarget could not be produced. Now: an A outside 0..<see
    /// cref="SupportKinds.MaxPacked"/> is UnknownArmy (polity and army recorded as −1); a B &lt; 0 is NegativeTarget, checked before the
    /// kind (under the documented decoding a negative B carries a negative part; kind byte 0, the raw B recorded as the target);
    /// otherwise kind = ⌊B ÷ 2^32⌋, target = B mod 2^32, and a kind other than 1 or 2 is UnknownKind (its byte recorded when it is
    /// 0..255, else 0).
    /// </summary>
    private void OnAllocate(long packed, long value, long now)
    {
        var (polity, army) = packed >= 0 && packed <= SupportKinds.MaxPacked ? SupportKinds.Unpack(packed) : (-1, -1);
        bool negative = value < 0;
        long kindCode = negative ? 0 : value / SupportKinds.TargetStride, target = negative ? value : value % SupportKinds.TargetStride;
        SupportRefusal refusal = SupportRefusal.None;
        if (army < 0 || army >= _a.Length) refusal = SupportRefusal.UnknownArmy;
        else if (_a[army].S.Owner != polity) refusal = SupportRefusal.NotOwner;
        else if (negative) refusal = SupportRefusal.NegativeTarget;
        else if (kindCode != (long)SupportKind.FieldMedical && kindCode != (long)SupportKind.SupplyTrain) refusal = SupportRefusal.UnknownKind;
        else if (_a[army].S.Composition.Naval) refusal = SupportRefusal.NavalForce;
        else if (_econ.ConditionOf(army) == ArmyCondition.Disbanded) refusal = SupportRefusal.ArmyDisbanded;
        if (refusal == SupportRefusal.None) _a[army].Target[kindCode] = target;
        byte kindByte = kindCode <= byte.MaxValue ? (byte)kindCode : (byte)0;
        _orders.Add(new SupportOrderRow(_orders.Count, now, polity, army, kindByte, target, (byte)refusal));
    }

    // ------------------------------------------------------------------ the latch (called by lane E at the army's pulse start)

    public SupportComposition Latch(int armyId, int owner, int warId, long pulseIndex, long pulseStartMs, long menAtStart, bool naval)
    {
        var a = _a[armyId];
        if (naval || a.Returned) return SupportComposition.None;
        SettleCare(a, pulseStartMs);
        var p = _pool[owner];
        // Medical staff first, then the supply train (fixed kind order).
        long medLimit = SupportMath.Limit(SupportKind.FieldMedical, menAtStart, _r);
        long medWant = Math.Min(a.Target[(int)SupportKind.FieldMedical], medLimit);
        long medHave = _l.Balance(a.Medics);
        if (medWant > medHave) _l.Transfer(p.Medics, a.Medics, Math.Min(medWant - medHave, _l.Balance(p.Medics)));
        else if (medWant < medHave) _l.Transfer(a.Medics, p.Medics, medHave - medWant);
        _allocs.Add(new SupportAllocationRow(armyId, warId, pulseIndex, pulseStartMs, (byte)SupportKind.FieldMedical,
            a.Target[(int)SupportKind.FieldMedical], medLimit, _l.Balance(a.Medics), _l.Balance(p.Medics)));

        long wagLimit = SupportMath.Limit(SupportKind.SupplyTrain, menAtStart, _r);
        long wagWant = Math.Min(a.Target[(int)SupportKind.SupplyTrain], wagLimit);
        long wagHave = _l.Balance(a.Wagons);
        if (wagWant > wagHave) MoveWagons(p, a, Math.Min(wagWant - wagHave, PoolWagonUnits(p)), toArmy: true);
        else if (wagWant < wagHave) MoveWagons(p, a, wagHave - wagWant, toArmy: false);
        _allocs.Add(new SupportAllocationRow(armyId, warId, pulseIndex, pulseStartMs, (byte)SupportKind.SupplyTrain,
            a.Target[(int)SupportKind.SupplyTrain], wagLimit, _l.Balance(a.Wagons), _l.Balance(p.Wagons)));

        a.Coverage = SupportMath.CoveragePerThousand(_l.Balance(a.Medics), menAtStart);
        return new SupportComposition(checked(_l.Balance(a.Medics) + _l.Balance(a.Drivers)), _l.Balance(a.Wagons), _l.Balance(a.Draft),
                                      _l.Balance(a.Wounded));
    }

    /// <summary>Complete wagon units the pool can field: wagons, with their draft animals and drivers.</summary>
    private long PoolWagonUnits(PoolS p)
    {
        long n = _l.Balance(p.Wagons);
        if (_r.DraftPerWagon > 0) n = Math.Min(n, _l.Balance(p.Draft) / _r.DraftPerWagon);
        if (_r.DriversPerWagon > 0) n = Math.Min(n, _l.Balance(p.Drivers) / _r.DriversPerWagon);
        return n;
    }

    private void MoveWagons(PoolS p, ArmyS a, long n, bool toArmy)
    {
        if (n <= 0) return;
        long draft = checked(n * _r.DraftPerWagon), drivers = checked(n * _r.DriversPerWagon);
        if (toArmy)
        {
            _l.Transfer(p.Wagons, a.Wagons, n);
            _l.Transfer(p.Draft, a.Draft, draft);
            _l.Transfer(p.Drivers, a.Drivers, drivers);
        }
        else
        {
            _l.Transfer(a.Wagons, p.Wagons, n);
            _l.Transfer(a.Draft, p.Draft, draft);
            _l.Transfer(a.Drivers, p.Drivers, drivers);
        }
    }

    /// <summary>Everything attached to a disbanded army returns to its owner's pool; its wounded (cared for up to now, recovered
    /// ones going home too) go home to the civilians.</summary>
    private void ReturnAll(ArmyS a, long now)
    {
        SettleCare(a, now);
        var p = _pool[a.S.Owner];
        _l.Transfer(a.Medics, p.Medics, _l.Balance(a.Medics));
        _l.Transfer(a.Wagons, p.Wagons, _l.Balance(a.Wagons));
        _l.Transfer(a.Draft, p.Draft, _l.Balance(a.Draft));
        _l.Transfer(a.Drivers, p.Drivers, _l.Balance(a.Drivers));
        _l.Transfer(a.Wounded, _econ.CivilianAccountOf(a.S.Owner), _l.Balance(a.Wounded));
        a.Coverage = 0;
        a.Returned = true;
    }

    // ------------------------------------------------------------------ care of the wounded

    /// <summary>Settles the care of the army's wounded over [CareFromMs, now) at the coverage in force (the latched one):
    /// recovered men rejoin the ranks, the dead of wounds go to lane E's dead account. Thousandth-of-a-head carries persist.</summary>
    private void SettleCare(ArmyS a, long now)
    {
        long dt = now - a.CareFromMs;
        if (dt < 0) throw new InvalidOperationException("care settled in the past");
        if (dt == 0) return;
        long wounded = _l.Balance(a.Wounded);
        if (wounded > 0)
        {
            var (recM, dieM) = SupportMath.CareMilli(wounded, a.Coverage, dt, _r);
            long rec = checked(recM + a.RecCarry), die = checked(dieM + a.DieCarry);
            long recHeads = Math.Min(wounded, rec / 1000);
            long dieHeads = Math.Min(wounded - recHeads, die / 1000);
            a.RecCarry = rec % 1000;
            a.DieCarry = die % 1000;
            // Recovered men rejoin the ranks; those of a disbanded army go home (its men account is closed).
            int to = _econ.ConditionOf(a.S.Id) == ArmyCondition.Disbanded ? _econ.CivilianAccountOf(a.S.Owner) : _econ.MenAccountOf(a.S.Id);
            _l.Transfer(a.Wounded, to, recHeads);
            _l.Transfer(a.Wounded, _econ.DeadAccount, dieHeads);
            _care.Add(new SupportCareRow(a.S.Id, a.CareFromMs, now, a.Coverage, wounded, recHeads, dieHeads));
        }
        a.CareFromMs = now;
    }

    // ------------------------------------------------------------------ battles

    private void OnBattle(int index, long now)
    {
        var b = _battles[index];
        var a = _a[b.ArmyId];
        if (a.Returned || _econ.ConditionOf(b.ArmyId) == ArmyCondition.Disbanded) throw new InvalidOperationException("a disbanded army fights no battle");
        SettleCare(a, now);
        int men = _econ.MenAccountOf(b.ArmyId);
        long persons = _l.Balance(men);
        var split = BattleCollapse.Split(persons, b.Collapse, b.HeldField);
        long saved = SupportMath.SavedFromDead(split.Dead, a.Coverage, _r);
        var final = SupportMath.WithSave(split, saved);
        if (!final.Conserved) throw new InvalidOperationException("the medical save does not conserve people");
        _l.Transfer(men, _econ.DeadAccount, final.Dead);
        _l.Transfer(men, a.Wounded, final.Wounded);
        _l.Transfer(men, _captured, final.Captured);
        _l.Transfer(men, _econ.CivilianAccountOf(a.S.Owner), final.Deserted);
        long before = _econ.MoraleOf(b.ArmyId);
        long restore = SupportMath.MoraleRestore(b.Collapse, split.Dead, saved, b.MoraleLossFullStrength, b.Leadership, _r);
        // [D, verification round 2, D-R2-01] the battle's net loss (start − end, less the restore), never a rise: the round-1 rule set
        // the campaign morale to the battle's end morale, which the TacticalResolver's in-battle recovery can lift above its start.
        // [MOR, D-082 DA1] that decrease (DR29) stands for every participant; an army of the side the report names the winner also gains
        // the DA1 rise; one clamp over both terms (CampaignMorale.After).
        long decrease = CampaignMorale.CombatDecreasePpm(b.MoraleStart, b.Collapse.MoraleEnd, restore, b.Collapse.MoraleMax);
        bool won = b.WinnerSide >= 0 && b.WinnerSide == b.Collapse.Side;
        long winnerSide = won && b.WinnerSidePersons == 0 ? persons : b.WinnerSidePersons;   // 0 = this army alone (the one-army stand-in)
        long rise = CampaignMorale.VictoryRisePpm(won, b.BeatenPersons, winnerSide, _m);
        long after = CampaignMorale.After(before, decrease, rise);
        _econ.SetMoraleAfterBattle(b.ArmyId, after);
        _battleRows.Add(new SupportBattleRow(_battleRows.Count, b.ArmyId, now, persons, split.Dead, saved, final.Dead, final.Wounded,
            final.Captured, final.Deserted, a.Coverage, before, after));
        _moraleRows.Add(new MoraleSettlementRow(_moraleRows.Count, b.ArmyId, now, (byte)b.Collapse.Side, b.WinnerSide, b.MoraleStart,
            b.Collapse.MoraleEnd, restore, decrease, b.BeatenPersons, winnerSide, rise, before, after));
    }

    // ------------------------------------------------------------------ hashes

    /// <summary>State hash: lane E's world state (its ledger includes every support account) and the support rows' encodings.</summary>
    public string StateHashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        _l.AppendTo(h);
        foreach (var t in EncodedTables()) h.AppendData(t);
        var b = new byte[8];
        foreach (var a in _a)
        {
            foreach (long v in new[] { a.Target[1], a.Target[2], a.Coverage, a.CareFromMs, a.RecCarry, a.DieCarry, a.Returned ? 1L : 0L })
            {
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(b, v);
                h.AppendData(b);
            }
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }

    public List<byte[]> EncodedTables() => new()
    {
        SupportCodec.EncodeOrders(_orders), SupportCodec.EncodeAllocations(_allocs), SupportCodec.EncodeCare(_care), SupportCodec.EncodeBattles(_battleRows),
        MoraleCodec.Encode(_moraleRows),                                  // [MOR]
    };
}
