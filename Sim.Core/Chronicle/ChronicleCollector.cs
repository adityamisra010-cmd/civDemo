using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Chronicle;

public enum ChronicleEventType
{
    Founding = 0,
    FamineOnset = 1,
    FamineEnd = 2,
    Extinction = 3,
    FirstArtisans = 4,
    MigrationSurge = 5,
    // T4.21-5 (spec §3.11, CR-015). Appended: the five ids above are unchanged.
    Disaster = 6,
    FoodShortfallOnset = 7,
    FoodShortfallEnd = 8,
}

/// <summary>One detected event: WHEN (turn, sim-year), WHERE (settlement),
/// and the triggering magnitudes (meaning depends on the type — documented
/// on <see cref="ChronicleCollector"/>). Pure value data.</summary>
public readonly record struct ChronicleEvent(
    ChronicleEventType Type, long Turn, double Year, int SettlementId,
    double Magnitude1, double Magnitude2);

/// <summary>
/// T2.9 event detection: data-driven THRESHOLD FUNCTIONS over observable rows
/// only — deficits (ConsumptionDeficits), chronicle flows (MigrationFlows),
/// vitals (SettlementVitals), class state (ClassStates + Buckets), population
/// (Buckets). An OBSERVATIONAL reader in the AutoplayMetrics mold: it never
/// writes WorldState and is not a system; its own state (per-settlement
/// latches) is UI/CLI-side history, exactly like the D-028 ring buffer —
/// replay rebuilds it, a mid-game load starts it fresh.
///
/// T4.21-5 (CR-015 "FAMINE IS EXCEPTIONAL", spec §3.11) — THE FAMINE EVENT IS
/// RE-BASED. It used to latch when the raw deficit crossed a chronicle-owned
/// constant (famineOnsetDeficit = 0.15), which is exactly the conflation the
/// ruling forbids: an ordinary bad-weather decade clears 0.15 and was written
/// into the annals as a famine. The annals now say "famine" when and only when
/// the simulation's own classifier says FAMINE — <see cref="FoodState.Of"/>,
/// the same static the kernel consults — and they carry its REASON. An ordinary
/// shortfall, however deep, is a FOOD SHORTFALL: a different event, with its own
/// onset and end on the deficit crossing zero, and its own prose. The two
/// deficit constants are RETIRED from chronicle.json; the chronicle owns no
/// famine threshold any more, because famine is not a threshold.
///
/// NOTHING HERE DECIDES ANYTHING. The collector calls FoodState on the world it
/// was handed and records the answer; it is an observer in the AutoplayMetrics
/// mold, outside the determinism surface, consulted by no system.
///
/// Magnitude meanings per event type:
///   Founding           — M1 = founding population, M2 unused.
///   FamineOnset        — M1 = the nominal deficit ratio at onset,
///                        M2 = the <see cref="FamineReason"/> ORDINAL (1 Disaster,
///                        2 Abandonment, 3 Both) — a recorded magnitude, which is
///                        what lets the prose bind {reason} without inventing it.
///   FamineEnd          — M1 = famine duration (sim-years), M2 = deaths during
///                        the famine (vitals Deaths summed over famine turns —
///                        base + starvation; the per-settlement split is queued).
///   Disaster           — M1 = DisasterRow.Severity, M2 = the AppliedMultiplier
///                        production actually multiplied the two food rates by.
///   FoodShortfallOnset — M1 = the deficit ratio on the turn it crossed above 0.
///   FoodShortfallEnd   — M1 = shortfall duration (sim-years), M2 unused.
///   Extinction         — M1 = the population that perished this turn, M2 unused.
///   FirstArtisans      — M1 = artisan adult count, M2 unused.
///   MigrationSurge     — M1 = outflow this turn, M2 = outflow / start-of-turn
///                        population (the surge fraction the threshold tested).
///
/// Detection is deterministic: settlements scan in table order each turn;
/// events append in (turn, table-order) sequence, and within a settlement in the
/// fixed order below. The famine and shortfall latches are EDGE latches on a
/// classification and on a sign — no hysteresis band survives, because neither
/// is a threshold crossing any more.
/// </summary>
public sealed class ChronicleCollector
{
    private readonly ChronicleConfig cfg;
    private readonly SimConfig _sim;

    /// <summary>The chronicle needs the sim config because FAMINE is now the
    /// simulation's own classification (FoodState.Of reads the adaptation
    /// threshold from it), not a chronicle constant.</summary>
    public ChronicleCollector(ChronicleConfig cfg, SimConfig sim)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(sim);
        this.cfg = cfg;
        _sim = sim;
    }

    private readonly List<ChronicleEvent> _events = [];
    public IReadOnlyList<ChronicleEvent> Events => _events;

    private sealed class Track
    {
        public bool Founded;
        public bool InFamine;
        public double FamineStartYear;
        public long FamineDeaths;
        public bool InShortfall;
        public double ShortfallStartYear;
        public bool Struck;
        public bool SeenArtisans;
        public bool Extinct;
        public long PrevPopulation;
    }

    private readonly Dictionary<int, Track> _tracks = [];

    private Track TrackFor(int id)
    {
        if (!_tracks.TryGetValue(id, out Track? t)) { t = new Track(); _tracks[id] = t; }
        return t;
    }

    /// <summary>Observe the world AFTER a turn has run (or the freshly founded
    /// world before turn 1 — founding events fire on first sight).</summary>
    public void Observe(IReadOnlyWorldState world)
    {
        long turn = world.Clock.Turn;
        double year = world.Clock.WorldDateYears;

        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementRow settlement = world.Settlements[i];
            int id = settlement.Id.Value;
            Track track = TrackFor(id);

            long pop = 0;
            long artisanAdults = 0;
            for (int b = 0; b < world.Buckets.Count; b++)
            {
                BucketRow bucket = world.Buckets[b];
                if (bucket.Settlement != settlement.Id) continue;
                pop += bucket.Count.Value;
                if (bucket.Class.Value != 1 && !BandViews.IsChild(bucket.CohortIdx)
                    && !BandViews.IsElder(bucket.CohortIdx))
                    artisanAdults += bucket.Count.Value;
            }

            if (!track.Founded)
            {
                track.Founded = true;
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.Founding, turn, year, id, pop, 0.0));
                track.PrevPopulation = pop;
                continue; // founding turn: no other event can predate existence
            }

            // THE CLASSIFICATION, not a threshold: FoodState.Of on the world
            // handed in — the same static the kernel consults, on the same
            // state. The deficit below is READ for the magnitudes only.
            FoodStateKind state = FoodState.Of(world, settlement.Id, _sim, out FamineReason reason);
            double deficit = FoodState.DeficitRatio(world, settlement.Id);

            long vitalsDeaths = 0;
            for (int v = 0; v < world.SettlementVitals.Count; v++)
                if (world.SettlementVitals[v].Settlement == settlement.Id)
                { vitalsDeaths = world.SettlementVitals[v].Deaths; break; }

            // DISASTER — the rising edge of "a famine-class multiplier was
            // applied to the harvest that produced this world's food"
            // (FoodState.IsStruck: DisasterRow.AppliedMultiplier < 1). An EDGE,
            // so a five-year failure is ONE line in the annals and not one line
            // per turn; the turn it ends is silent by design (the disaster is
            // news, its expiry is not).
            bool struck = FoodState.IsStruck(world, settlement.Id);
            if (struck && !track.Struck)
            {
                double severity = 0.0, applied = 1.0;
                for (int d = 0; d < world.Disasters.Count; d++)
                {
                    if (world.Disasters[d].Settlement != settlement.Id) continue;
                    severity = world.Disasters[d].Severity;
                    applied = world.Disasters[d].AppliedMultiplier;
                    break;
                }
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.Disaster, turn, year, id, severity, applied));
            }
            track.Struck = struck;

            // FAMINE — CR-015. Fires on the classification and carries its
            // reason; an ordinary bad harvest, at ANY depth, is not one.
            if (!track.InFamine && state == FoodStateKind.Famine)
            {
                track.InFamine = true;
                track.FamineStartYear = year;
                track.FamineDeaths = vitalsDeaths;
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.FamineOnset, turn, year, id, deficit, (double)(int)reason));
            }
            else if (track.InFamine)
            {
                track.FamineDeaths += vitalsDeaths;
                if (state != FoodStateKind.Famine)
                {
                    track.InFamine = false;
                    _events.Add(new ChronicleEvent(
                        ChronicleEventType.FamineEnd, turn, year, id,
                        year - track.FamineStartYear, track.FamineDeaths));
                }
            }

            // FOOD SHORTFALL — the ordinary event the famine line used to
            // usurp: the deficit crossing zero, in either direction. This is the
            // same transition SessionInspector.MajorEvents calls FOOD
            // SHORTFALL/RECOVERY; the annals now carry it too.
            bool shortfall = deficit > 0.0;
            if (shortfall && !track.InShortfall)
            {
                track.InShortfall = true;
                track.ShortfallStartYear = year;
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.FoodShortfallOnset, turn, year, id, deficit, 0.0));
            }
            else if (!shortfall && track.InShortfall)
            {
                track.InShortfall = false;
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.FoodShortfallEnd, turn, year, id,
                    year - track.ShortfallStartYear, 0.0));
            }

            // First artisans: the first turn any non-peasant class has adults.
            if (!track.SeenArtisans && artisanAdults > 0)
            {
                track.SeenArtisans = true;
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.FirstArtisans, turn, year, id, artisanAdults, 0.0));
            }

            // Migration surge: this turn's outflow against the population that
            // started the turn (PrevPopulation — outflow is part of why the
            // present count shrank, so present-count normalization would
            // overstate the share).
            for (int f = 0; f < world.MigrationFlows.Count; f++)
            {
                if (world.MigrationFlows[f].Settlement != settlement.Id) continue;
                long outflow = world.MigrationFlows[f].Outflow;
                if (track.PrevPopulation > 0 && outflow > 0)
                {
                    double share = outflow / (double)track.PrevPopulation;
                    if (share >= cfg.Thresholds.MigrationSurgeFraction)
                        _events.Add(new ChronicleEvent(
                            ChronicleEventType.MigrationSurge, turn, year, id, outflow, share));
                }
                break;
            }

            // Extinction: latched forever (a dead settlement stays dead in the
            // annals even if migration later reseeds the site — that would be
            // a refounding mechanism no system has yet).
            if (!track.Extinct && pop == 0 && track.PrevPopulation > 0)
            {
                track.Extinct = true;
                if (track.InFamine)
                {
                    track.InFamine = false; // the famine "ends" by extinction;
                    // no FamineEnd event — the extinction line carries the news.
                }
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.Extinction, turn, year, id, track.PrevPopulation, 0.0));
            }

            track.PrevPopulation = pop;
        }
    }
}
