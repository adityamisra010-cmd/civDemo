using Sim.Core.State;

namespace Sim.Core.Chronicle;

public enum ChronicleEventType
{
    Founding = 0,
    FamineOnset = 1,
    FamineEnd = 2,
    Extinction = 3,
    FirstArtisans = 4,
    MigrationSurge = 5,
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
/// Magnitude meanings per event type:
///   Founding       — M1 = founding population, M2 unused.
///   FamineOnset    — M1 = deficit ratio at onset, M2 unused.
///   FamineEnd      — M1 = famine duration (sim-years), M2 = deaths during
///                    the famine (vitals Deaths summed over famine turns —
///                    base + starvation; the per-settlement split is queued).
///   Extinction     — M1 = the population that perished this turn, M2 unused.
///   FirstArtisans  — M1 = artisan adult count, M2 unused.
///   MigrationSurge — M1 = outflow this turn, M2 = outflow / start-of-turn
///                    population (the surge fraction the threshold tested).
///
/// Detection is deterministic: settlements scan in table order each turn;
/// events append in (turn, table-order) sequence. Latches are hysteretic
/// where the data says so (famine onset above famineOnsetDeficit, end at or
/// below famineEndDeficit).
/// </summary>
public sealed class ChronicleCollector(ChronicleConfig cfg)
{
    private readonly List<ChronicleEvent> _events = [];
    public IReadOnlyList<ChronicleEvent> Events => _events;

    private sealed class Track
    {
        public bool Founded;
        public bool InFamine;
        public double FamineStartYear;
        public long FamineDeaths;
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

            // Famine latch (hysteretic per data): deficit is THIS turn's
            // Consumption output — the observable row, no recomputation.
            double deficit = 0.0;
            for (int d = 0; d < world.ConsumptionDeficits.Count; d++)
                if (world.ConsumptionDeficits[d].Settlement == settlement.Id)
                { deficit = world.ConsumptionDeficits[d].DeficitRatio; break; }

            long vitalsDeaths = 0;
            for (int v = 0; v < world.SettlementVitals.Count; v++)
                if (world.SettlementVitals[v].Settlement == settlement.Id)
                { vitalsDeaths = world.SettlementVitals[v].Deaths; break; }

            if (!track.InFamine && deficit >= cfg.Thresholds.FamineOnsetDeficit)
            {
                track.InFamine = true;
                track.FamineStartYear = year;
                track.FamineDeaths = vitalsDeaths;
                _events.Add(new ChronicleEvent(
                    ChronicleEventType.FamineOnset, turn, year, id, deficit, 0.0));
            }
            else if (track.InFamine)
            {
                track.FamineDeaths += vitalsDeaths;
                if (deficit <= cfg.Thresholds.FamineEndDeficit)
                {
                    track.InFamine = false;
                    _events.Add(new ChronicleEvent(
                        ChronicleEventType.FamineEnd, turn, year, id,
                        year - track.FamineStartYear, track.FamineDeaths));
                }
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
