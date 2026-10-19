using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>Why a queue head's set-aside is RELEASED (D-055): the reservation is INVALID — its holder can never progress in
/// any future state — so it holds nothing and blocks no one. Evaluated directly from PREV and the content, before and
/// independently of <see cref="ConstructionQuery.ResolveHead"/>'s check order (<see cref="ConstructionQuery.ReleaseOf"/>).</summary>
public enum HoldRelease
{
    /// <summary>Not released: the head holds its remaining need (a valid, possibly long, wait).</summary>
    None = 0,
    /// <summary>(a) goods.json no longer defines the entry's project.</summary>
    UnknownProject = 1,
    /// <summary>(b) the settlement has no labour adults (InstitutionStaffing.LabourAdults ≤ 0). Sim.Core never removes a
    /// settlement, so a destroyed or abandoned settlement is a depopulated one.</summary>
    NoLabourAdults = 2,
    /// <summary>(c) the project founds an institution and the host's founding viability fails for want of an institutions
    /// configuration (ViabilityFailure.NoConfig), which can never become true.</summary>
    NoInstitutionsConfig = 3,
}

/// <summary>
/// One queue head of <see cref="ConstructionQuery.HoldsWalk"/>, in THE BUILDING ORDER: its purse key (<see cref="Purse"/>:
/// a polity id ≥ 0 for an Empire purse, −1 − settlementId for a single-settlement purse — <see cref="Reservations"/>), its
/// release (<see cref="Release"/>), its resolution against the budget it was given (<see cref="Resolution"/>: p', the draws
/// in project-input order, the limit), the units it draws (<see cref="Draws"/>) and SETS ASIDE (<see cref="Holds"/>) of each
/// good, indexed by good id; the budget it was resolved against (<see cref="ResolvedAgainst"/>, by good id); and, for a
/// materials-limited head, why it lacks its binding good (<see cref="Cause"/>), the earlier heads of its purse that drew that
/// good (<see cref="TakenBy"/>), the earliest earlier head that sets it aside (<see cref="Holder"/>, for
/// <see cref="MaterialCause.HeldForEarlier"/>) and how many units of it earlier heads of its purse set aside
/// (<see cref="SetAsideAhead"/>).
/// </summary>
public sealed record HoldsWalkHead(
    ConstructionQueueRow Entry, int Purse, HoldRelease Release, HeadResolution Resolution, long[] Draws, long[] Holds,
    long[] ResolvedAgainst, MaterialCause Cause, QueueEntryRef[] TakenBy, QueueEntryRef? Holder, long SetAsideAhead)
{
    /// <summary>The head's set-aside is released (invalid): it holds nothing.</summary>
    public bool Released => Release != HoldRelease.None;

    /// <summary>The units of good <paramref name="good"/> the head sets aside (0 for an unknown id).</summary>
    public long HoldOf(int good) => good >= 0 && good < Holds.Length ? Holds[good] : 0;

    /// <summary>The units of good <paramref name="good"/> the head draws (0 for an unknown id).</summary>
    public long DrawOf(int good) => good >= 0 && good < Draws.Length ? Draws[good] : 0;
}

/// <summary>
/// THE RESULT OF <see cref="ConstructionQuery.HoldsWalk"/>: every head walked, in the building order (<see cref="Heads"/>);
/// the purse keys met, ascending (<see cref="Purses"/>), with each purse's holder set (<see cref="HolderSets"/>), its
/// starting budget (<see cref="Start"/>: min(the holding S, the building grant G_b) of each good, by good id), and what its
/// heads drew (<see cref="Drawn"/>) and set aside (<see cref="Held"/>) in all. <see cref="Enabled"/>: the hold rule applied
/// (Reservations.Enabled) — false, every hold is 0 and the walk is batch 3's resolution exactly.
/// </summary>
public sealed record HoldsWalkResult(
    bool Enabled, HoldsWalkHead[] Heads, int[] Purses, SettlementId[][] HolderSets, long[][] Start, long[][] Drawn, long[][] Held)
{
    /// <summary>The walked head of <paramref name="settlement"/>, or null when it has none.</summary>
    public HoldsWalkHead? HeadOf(SettlementId settlement)
    {
        for (int i = 0; i < Heads.Length; i++) if (Heads[i].Entry.Settlement == settlement) return Heads[i];
        return null;
    }

    /// <summary>The index of <paramref name="purse"/> in <see cref="Purses"/>, or −1.</summary>
    public int IndexOfPurse(int purse) => Array.BinarySearch(Purses, purse) is var i && i >= 0 ? i : -1;

    /// <summary>The units of <paramref name="good"/> set aside in <paramref name="purse"/> (0 when none).</summary>
    public long HeldOf(int purse, int good)
    {
        int k = IndexOfPurse(purse);
        return k < 0 || good < 0 || good >= Held[k].Length ? 0 : Held[k][good];
    }

    /// <summary>THE PUBLISHED TABLE (ConstructionHolds, schema v34 section 61): one row per (purse, good) with units set
    /// aside, ascending (Purse, Good) — the purses are ascending and the goods are walked in ascending id. Empty when the
    /// rule is off.</summary>
    public ConstructionHoldRow[] Rows()
    {
        var rows = new List<ConstructionHoldRow>();
        if (!Enabled) return [];
        for (int k = 0; k < Purses.Length; k++)
            for (int g = 0; g < Held[k].Length; g++)
                if (Held[k][g] > 0) rows.Add(new ConstructionHoldRow(Purses[k], g, Held[k][g]));
        return [.. rows];
    }
}

public static partial class ConstructionQuery
{
    /// <summary>
    /// THE PURSE KEY of <paramref name="settlement"/>'s head (D-055; <see cref="Reservations"/>): the identity of the holder
    /// set it draws from (<see cref="Holders"/> on <paramref name="prev"/>) — its controller's polity id when
    /// construction.empireStockpile is on and an Empire controls it (<see cref="DrawsOnEmpire"/>), otherwise −1 −
    /// settlementId (its own stock: purse off, a stateless settlement, a world with no Controls).
    /// </summary>
    public static int PurseKey(IReadOnlyWorldState prev, SimConfig cfg, SettlementId settlement) =>
        cfg.Construction is { EmpireStockpile: true } && EmpireQuery.TryGetController(prev, settlement, out PolityId controller)
            ? Reservations.PolityPurse(controller)
            : Reservations.SettlementPurse(settlement);

    /// <summary>
    /// THE HOLDER SET OF <paramref name="settlement"/>'S PURSE under the hold rule (D-055; fix round 2026-10-16, finding
    /// T1a-L1-T1a-V1-F1): <see cref="Holders"/> on <paramref name="prev"/>, in its draw order, less every holder whose OWN
    /// purse key (<see cref="PurseKey"/>) is not the head's — so the purses' holder sets are DISJOINT, each exactly the
    /// places keyed to that purse, and no unit is ever counted, drawn or set aside by two purses. It differs from Holders
    /// only in a world that carries two control rows for one place (D-037's "exactly one" is documented but unenforced —
    /// EmpireQuery.TryGetController — and Snapshot.Load accepts such a world): there the place's stock belongs to the purse of
    /// its LOWEST controller, the one TryGetController and the purse key already name, and its other controllers' heads
    /// neither count nor draw it. HoldsWalk resolves, draws, sets aside and checks invariant (i) against it, and
    /// ConstructionSystem draws through it. With the rule off (<see cref="Reservations.Enabled"/> false —
    /// TestConfigs.PreReservations) it is Holders exactly: batch 3's draws, double-control worlds included.
    /// </summary>
    public static SettlementId[] PurseHolders(IReadOnlyWorldState prev, SimConfig cfg, SettlementId settlement)
    {
        SettlementId[] holders = Holders(prev, cfg, settlement);
        if (holders.Length < 2 || !Reservations.Enabled(cfg)) return holders;
        int key = PurseKey(prev, cfg, settlement);
        var mine = new bool[holders.Length];
        int n = 0;
        for (int i = 0; i < holders.Length; i++)
        {
            mine[i] = PurseKey(prev, cfg, holders[i]) == key;
            if (mine[i]) n++;
        }
        if (n == holders.Length) return holders;
        var result = new SettlementId[n];
        for (int i = 0, j = 0; i < holders.Length; i++)
            if (mine[i]) result[j++] = holders[i];
        return result;
    }

    /// <summary>
    /// THE SAME DISJOINT-HOLDER RULE FOR ROAD WORKS (D-055; T1a round 2, 2026-10-16, the fix round's open item 1):
    /// <paramref name="places"/> — Empire <paramref name="polity"/>'s road payers and donors
    /// (RoadDevelopmentQuery.PayingSettlements: every place the polity has a control row for, in whatever order the caller
    /// passes) — less every place whose OWN purse key (<see cref="PurseKey"/>) is ANOTHER Empire's purse. This is
    /// <see cref="PurseHolders"/>' rule applied where road development reads a purse: a place with two control rows belongs to
    /// its LOWEST controller's purse alone, so a higher controller's road works neither count nor draw its stock, and can never
    /// spend what that purse set aside there. A place keyed to a single-settlement purse (construction.empireStockpile off; a
    /// world with no Controls) is kept: its own set-aside already floors it as a donor. Only with the rule on
    /// (<see cref="Reservations.Enabled"/>); with it off this is <paramref name="places"/> itself — batch 3's road payers. It
    /// differs from <paramref name="places"/> only in a world with two control rows for one place, and returns the same array
    /// when nothing is dropped.
    /// </summary>
    public static SettlementId[] EmpirePursePlaces(IReadOnlyWorldState prev, SimConfig cfg, PolityId polity, SettlementId[] places)
    {
        if (places.Length == 0 || !Reservations.Enabled(cfg)) return places;
        int own = Reservations.PolityPurse(polity);
        var keep = new bool[places.Length];
        int n = 0;
        for (int i = 0; i < places.Length; i++)
        {
            int key = PurseKey(prev, cfg, places[i]);
            keep[i] = key < 0 || key == own;
            if (keep[i]) n++;
        }
        if (n == places.Length) return places;
        var result = new SettlementId[n];
        for (int i = 0, j = 0; i < places.Length; i++)
            if (keep[i]) result[j++] = places[i];
        return result;
    }

    /// <summary>
    /// THE RELEASE PREDICATE (D-055): whether <paramref name="entry"/>'s set-aside is INVALID — its holder can never
    /// progress in any future state — read directly from <paramref name="prev"/> and the content, before and independently
    /// of <see cref="ResolveHead"/> (which checks viability before labour, so it never reports "no labour adults" for an
    /// institution head): (a) its project is not in the content; (b) its settlement has no labour adults; (c) it founds an
    /// institution and the host's viability fails for want of an institutions configuration. Every other wait — materials
    /// short, no builders (labour adults present: the player's labour split), a market viability failure (the published
    /// food surplus and the adults change), an unobtainable crafted input (research can open a recipe) — is VALID and keeps
    /// holding.
    /// </summary>
    public static HoldRelease ReleaseOf(IReadOnlyWorldState prev, SimConfig cfg, ConstructionQueueRow entry)
    {
        if (cfg.Goods?.ProjectById(entry.ProjectId) is not { } project) return HoldRelease.UnknownProject;
        if (InstitutionStaffing.LabourAdults(prev, cfg, entry.Settlement) <= 0.0) return HoldRelease.NoLabourAdults;
        if (InstitutionContent.FoundsInstitution(project)
            && InstitutionViability.ToFound(prev, cfg, entry.Settlement).Failure == ViabilityFailure.NoConfig)
            return HoldRelease.NoInstitutionsConfig;
        return HoldRelease.None;
    }

    /// <summary>
    /// THE HOLDS WALK (D-055 — the Director's batch-4 instruction, item 1: projects must not consume resources reserved by
    /// other projects). ONE pure function — ConstructionSystem's step, <see cref="HeadStatus"/>, <see cref="AllocationFor"/>
    /// (D_b and the forecast) and the UI queries all call it, so the release predicate, the budget order and the labels can
    /// never differ between them. It reads and allocates only (a scratch copy of <paramref name="stocks"/> carries the draws).
    /// <list type="number">
    /// <item>The heads of <paramref name="queue"/> in THE BUILDING ORDER (<see cref="HeadsInBuildingOrder"/>: EnqueuedTurn —
    ///   the order's stamp turn — ascending, −1 for an entry with no progress row; SettlementId; Slot), each keyed by its
    ///   purse (<see cref="PurseKey"/> on <paramref name="prev"/>).</item>
    /// <item>Each purse's BUDGET of each good starts at its holding S in <paramref name="stocks"/> — the sum over its own
    ///   places, <see cref="PurseHolders"/>, which are disjoint across purses even in a world with two control rows for one
    ///   place (fix round, finding T1a-L1-T1a-V1-F1), so no unit is ever counted by two purses (ConstructionSystem: its
    ///   NEXT stocks after production, trade and housing and after this step's cancels and enqueues), or at min(S, G_b)
    ///   where the Empire's building/roads split grants building work G_b (<paramref name="grants"/>, aligned with
    ///   <paramref name="prev"/>.Polities — <see cref="GrantsLeft"/>; a single-settlement purse is never under a split).</item>
    /// <item>For each head in order: a RELEASED head (<see cref="ReleaseOf"/>) draws nothing and sets nothing aside. Any
    ///   other head is resolved by <see cref="ResolveHead"/> (against its purse's places) and its purse's budget — by
    ///   induction at most what the purse
    ///   physically holds, so a draw never exceeds the stock — giving p' and the draws d (the lockstep); the budget falls by
    ///   d; then the head SETS ASIDE hold_g = min(need_g, budget_g) of each good, need_g = Σ over its inputs of good g of
    ///   Remaining(c, p'), and the budget falls by the hold. The draw is subtracted BEFORE the hold, so Σ d + Σ hold never
    ///   exceeds the starting budget and no hold names a unit that is not there.</item>
    /// <item>LABELS (display only) for a materials-limited head, counterfactually: resolved again against A0_g = min(what
    ///   is physically left, the grant less the earlier draws) — i.e. ignoring earlier set-asides — a head that would get
    ///   further there is <see cref="MaterialCause.HeldForEarlier"/>, naming the earliest earlier head of its purse that sets
    ///   the binding good aside; otherwise <see cref="MaterialCause.HeldForRoads"/> when the grant binds below what is
    ///   physically left; and <see cref="HeadLimitKind.Unobtainable"/> stands only when what is physically left, ignoring
    ///   set-asides and the grant, cannot move the head and the crafted input's recipe chain is unreachable.</item>
    /// </list>
    /// D-058 (the Director's ruling of 2026-10-19; <see cref="ContinuesWithinTurn"/>): a head that COMPLETES passes what is
    /// left of its settlement's capacity to the next entry of its queue, which joins the walk IN THE SAME STEP at its own
    /// place in the building order and is resolved, labelled and holds like any head (see the loop); the walk's heads are
    /// therefore the queue heads plus those continuations, still in the building order. Off (TestConfigs.PreContinueWithinTurn):
    /// one head per settlement, batch 4.
    /// With the hold rule off (<see cref="Reservations.Enabled"/> false — TestConfigs.PreReservations) nothing is set
    /// aside, each head resolves against its Empire's grant less the earlier draws exactly as batch 3 did, and the labels
    /// are batch 3's. The oldest unreleased head of every purse is resolved against the whole starting budget, so a hold
    /// never blocks it and no cycle of holds can form. Arrays indexed by purse and good id, checked long arithmetic, no
    /// dictionary, O(heads × goods). <paramref name="strict"/>: ResolveHead's strict mode (the system).
    /// </summary>
    public static HoldsWalkResult HoldsWalk(
        IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyTable<ConstructionQueueRow> queue,
        IReadOnlyTable<ConstructionProgressRow> progress, IReadOnlyTable<GoodStockRow> stocks, double dtYears,
        long[]?[] grants, bool strict = false) =>
        Walk(prev, cfg, queue, progress, stocks, dtYears, grants, only: null, onlyGrant: null, strict);

    /// <summary>The walk over the heads of ONE Empire's purse (<paramref name="polity"/>; the split's D_b and forecast):
    /// its heads only — no other purse shares its holders — within <paramref name="grant"/> (by good id; null: no
    /// split).</summary>
    private static HoldsWalkResult HoldsWalkOf(
        IReadOnlyWorldState prev, SimConfig cfg, PolityId polity, IReadOnlyTable<ConstructionQueueRow> queue,
        IReadOnlyTable<ConstructionProgressRow> progress, IReadOnlyTable<GoodStockRow> stocks, double dtYears, long[]? grant) =>
        Walk(prev, cfg, queue, progress, stocks, dtYears, [], only: Reservations.PolityPurse(polity), onlyGrant: grant, strict: false);

    private static HoldsWalkResult Walk(
        IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyTable<ConstructionQueueRow> queue,
        IReadOnlyTable<ConstructionProgressRow> progress, IReadOnlyTable<GoodStockRow> stocks, double dtYears,
        long[]?[] grants, int? only, long[]? onlyGrant, bool strict)
    {
        bool on = Reservations.Enabled(cfg);
        if (cfg.Goods is not { } goods) return new HoldsWalkResult(on, [], [], [], [], [], []);
        int width = MaxGoodId(goods) + 1;

        // The heads in the building order, each with its purse key (only the one purse when asked), its order key (the
        // building order's EnqueuedTurn) and the capacity it is resolved with (null: the settlement's whole capacity this
        // step — every head that starts the walk; D-058: a promoted entry carries what its queue's completed entries left).
        ConstructionQueueRow[] all = HeadsInBuildingOrder(prev, queue, progress);
        var heads = new List<ConstructionQueueRow>(all.Length);
        var keys = new List<int>(all.Length);
        var turns = new List<long>(all.Length);
        var capacities = new List<double?>(all.Length);
        for (int h = 0; h < all.Length; h++)
        {
            int key = PurseKey(prev, cfg, all[h].Settlement);
            if (only is { } k && key != k) continue;
            heads.Add(all[h]);
            keys.Add(key);
            turns.Add(EnqueuedTurnOf(progress, all[h]));
            capacities.Add(null);
        }
        bool chain = ContinuesWithinTurn(cfg);

        if (heads.Count == 0) return new HoldsWalkResult(on, [], [], [], [], [], []);   // no head: nothing to walk or copy

        // The purses met, ascending (a sorted int array: the publication order and the slot lookup — no dictionary).
        var sorted = new int[keys.Count];
        for (int i = 0; i < sorted.Length; i++) sorted[i] = keys[i];
        Array.Sort(sorted);
        int n = 0;
        for (int i = 0; i < sorted.Length; i++) if (n == 0 || sorted[n - 1] != sorted[i]) sorted[n++] = sorted[i];
        var purses = new int[n];
        Array.Copy(sorted, purses, n);

        var holderSets = new SettlementId[n][];
        var start = new long[n][];
        var budget = new long[n][];
        var grantLeft = new long[]?[n];
        var drawn = new long[n][];
        var held = new long[n][];
        for (int h = 0; h < heads.Count; h++)
        {
            int k = Array.BinarySearch(purses, keys[h]);
            if (start[k] is not null) continue;                     // the purse's first head sets it up
            SettlementId at = heads[h].Settlement;
            SettlementId[] holders = PurseHolders(prev, cfg, at);   // the purse's own places (disjoint across purses)
            long[]? grant = only is null ? GrantsFor(prev, grants, at) : onlyGrant;
            if (keys[h] < 0) grant = null;                          // a single-settlement purse is never under a split
            holderSets[k] = holders;
            start[k] = new long[width];
            for (int g = 0; g < width; g++)
            {
                long s = EmpireStockpile.Available(stocks, holders, new GoodId(g));
                start[k][g] = grant is null || g >= grant.Length || grant[g] == long.MaxValue ? s : Math.Min(s, Math.Max(0, grant[g]));
            }
            budget[k] = (long[])start[k].Clone();
            grantLeft[k] = grant is null ? null : (long[])grant.Clone();
            drawn[k] = new long[width];
            held[k] = new long[width];
        }

        var scratch = new Table<GoodStockRow>(stocks.Count);
        for (int i = 0; i < stocks.Count; i++) scratch.Add(stocks[i]);
        var ledger = new Ledger(new Table<LedgerFlowRow>());
        var result = new List<HoldsWalkHead>(heads.Count);
        var refs = new List<QueueEntryRef>(heads.Count);
        for (int h = 0; h < heads.Count; h++) refs.Add(new QueueEntryRef(heads[h].Settlement, heads[h].Slot, heads[h].ProjectId));

        // heads.Count grows while the walk runs (D-058: a promoted entry is inserted at its own order's place, always after h).
        for (int h = 0; h < heads.Count; h++)
        {
            SettlementId at = heads[h].Settlement;
            int k = Array.BinarySearch(purses, keys[h]);
            SettlementId[] holders = PurseHolders(prev, cfg, at);   // = Holders with the rule off: batch 3
            HoldRelease release = ReleaseOf(prev, cfg, heads[h]);
            ConstructionQueueRow entry = heads[h];
            double? capacity = capacities[h];
            // ON: the purse's budget (the grant where a split binds, less every earlier draw and set-aside). OFF: batch 3 —
            // the Empire's grant less the earlier draws (null: no cap; the scratch stocks are what is physically left).
            long[]? against = on ? (long[])budget[k].Clone() : (long[]?)grantLeft[k]?.Clone();
            HeadResolution r = ResolveEntryIn(prev, cfg, entry, holders, progress, scratch, dtYears, capacity, against, strict);
            var draws = new long[width];
            var holds = new long[width];
            if (r.Project is { } project)
                for (int i = 0; i < project.Inputs.Length; i++)
                {
                    int g = goods.IdOf(project.Inputs[i].Good);
                    if (g >= 0 && g < width) draws[g] = checked(draws[g] + r.Draws[i]);
                }
            if (on && release != HoldRelease.None)
                for (int g = 0; g < width; g++)
                    if (draws[g] != 0)
                        throw new InvalidOperationException(
                            $"construction: the released head of settlement {heads[h].Settlement.Value} ({release}) drew good {g}.");

            // Labels, before this head's own draws move the scratch stocks.
            MaterialCause cause = MaterialCause.None;
            QueueEntryRef? holder = null;
            long ahead = 0;
            var takenBy = new List<QueueEntryRef>();
            if (r.Limit is (HeadLimitKind.Materials or HeadLimitKind.Unobtainable) && r.LimitGood >= 0 && r.Project is not null)
            {
                bool heldForEarlier = false, heldForRoads = r.HeldForRoads;
                if (on)
                {
                    // §1.3 both ways (fix round, finding T1a-L1-T1a-V1-F3): a head that cannot move is Unobtainable EXACTLY
                    // when what is physically left in its purse — ignoring set-asides and the grant — cannot move it and the
                    // crafted input's recipe chain is unreachable (that resolution's binding good named); otherwise it is
                    // materials-limited, whichever good the budget made bind.
                    if (r.ProgressAfter == r.ProgressBefore)
                    {
                        HeadResolution physical = ResolveEntryIn(prev, cfg, entry, holders, progress, scratch, dtYears, capacity, null, strict: false);
                        if (physical.Limit == HeadLimitKind.Unobtainable)
                            r = r with { Limit = HeadLimitKind.Unobtainable, LimitGood = physical.LimitGood, Short = physical.Short, HeldForRoads = false };
                        else if (r.Limit == HeadLimitKind.Unobtainable)
                            r = r with { Limit = HeadLimitKind.Materials };   // what is physically left could move it: not structural
                    }
                    HeadResolution r0 = ResolveEntryIn(prev, cfg, entry, holders, progress, scratch, dtYears, capacity, grantLeft[k], strict: false);
                    heldForEarlier = r0.ProgressAfter > r.ProgressAfter;
                    heldForRoads = !heldForEarlier && r0.HeldForRoads;
                    r = r with { HeldForRoads = heldForRoads };
                    // LABEL ONLY (T1b fix round 2026-10-17, finding T1b-L1-F3; display: no draw, set-aside or table changes):
                    // a head the earlier set-asides do NOT hold back is labelled with the good the counterfactual A0
                    // resolution names — the one it is physically short of — not the one only a set-aside made bind first.
                    if (!heldForEarlier && r.Limit == HeadLimitKind.Materials && r0.Limit == HeadLimitKind.Materials && r0.LimitGood >= 0)
                        r = r with { LimitGood = r0.LimitGood, Short = r0.Short };
                    if (heldForEarlier)
                    {
                        ahead = held[k][r.LimitGood];
                        for (int e = 0; e < h && holder is null; e++)
                            if (keys[e] == keys[h] && result[e].HoldOf(r.LimitGood) > 0) holder = refs[e];
                    }
                }
                for (int e = 0; e < h; e++)
                    if (keys[e] == keys[h] && result[e].DrawOf(r.LimitGood) > 0 && !takenBy.Contains(refs[e])) takenBy.Add(refs[e]);
                long heldAtStart = EmpireStockpile.Available(stocks, holders, new GoodId(r.LimitGood));
                cause = heldAtStart <= 0 ? MaterialCause.EmpireHasNone
                    : heldForEarlier ? MaterialCause.HeldForEarlier
                    : heldForRoads ? MaterialCause.HeldForRoads
                    : takenBy.Count > 0 ? MaterialCause.TakenByEarlier : MaterialCause.Short;
            }

            // The draws: the scratch stocks (in input order, from the head's holders — exactly the system's draws).
            if (r.Project is { } p && r.Progresses)
            {
                for (int i = 0; i < p.Inputs.Length; i++)
                    if (r.Draws[i] > 0)
                        EmpireStockpile.Draw(ledger, scratch, holders, new GoodId(goods.IdOf(p.Inputs[i].Good)), r.Draws[i],
                            ReasonIds.ConstructionMaterials);
            }

            // The hold: the remaining need after this step's draw, capped by what the budget has left AFTER that draw —
            // the draw is subtracted first — then both spend the budget (SpendGrants: the grant where a split binds), and
            // the draws alone spend the grant the counterfactual A0 reads.
            if (r.Project is { } q)
            {
                if (on && release == HoldRelease.None)
                {
                    for (int i = 0; i < q.Inputs.Length; i++)
                    {
                        int g = goods.IdOf(q.Inputs[i].Good);
                        if (g >= 0 && g < width) holds[g] = checked(holds[g] + Remaining(q.Inputs[i].Qty, r.ProgressAfter));
                    }
                    for (int g = 0; g < width; g++) holds[g] = Math.Min(holds[g], Math.Max(0, checked(budget[k][g] - draws[g])));
                }
                SpendGrants(budget[k], goods, q, r.Draws, holds);
                SpendGrants(grantLeft[k], goods, q, r.Draws);
            }
            for (int g = 0; g < width; g++)
            {
                drawn[k][g] = checked(drawn[k][g] + draws[g]);
                held[k][g] = checked(held[k][g] + holds[g]);
            }
            result.Add(new HoldsWalkHead(heads[h], keys[h], release, r, draws, holds, against ?? Unbounded(width), cause,
                [.. takenBy], holder, ahead));

            // D-058 — ONE AT A TIME, BUT NOT ONE A TURN (the Director's ruling of 2026-10-19). A head that COMPLETES leaves
            // the rest of its settlement's capacity this step, C − L × (N − p) / N adult-years, to the NEXT entry of the same
            // queue (the lowest slot above it): it becomes a head IN THIS STEP and is inserted into the walk at its OWN
            // order's place — the building order's composite integer key (EnqueuedTurn, SettlementId, Slot), never progress
            // and never a double — so it is resolved against exactly what every older head of its purse drew and set aside,
            // may spend only what a head in that place may spend, and as a head sets aside its remaining need (it holds in
            // this step, as a cancel's promoted slot does). Its key is above the completed head's (within one queue, slots
            // are issued in order-age order), so it always lands after h and every head already walked stays where it was.
            // Not continued: a project that founds an institution (its host's viability is read on PREV, which cannot
            // count a founding completed this step — it is the head next step, as before), and an entry the leftover
            // capacity cannot move by one progress unit (it is not started: the head next step, as before). Switch off
            // (TestConfigs.PreContinueWithinTurn): the batch-4 walk exactly.
            if (chain && r.Completes
                && NextEntryAfter(queue, entry) is { } promoted
                && goods.ProjectById(promoted.ProjectId) is { } next && !InstitutionContent.FoundsInstitution(next))
            {
                double left = (capacity ?? CapacityAdultYears(prev, cfg, at, dtYears)) - r.LaborUsed;
                if (LabourBound(left, next.LaborRequired, ProgressOf(progress, promoted)) > 0)
                {
                    long turn = EnqueuedTurnOf(progress, promoted);
                    int pos = h + 1;
                    while (pos < heads.Count && CompareBuildingOrder(turns[pos], heads[pos], turn, promoted) < 0) pos++;
                    heads.Insert(pos, promoted);
                    keys.Insert(pos, keys[h]);
                    turns.Insert(pos, turn);
                    capacities.Insert(pos, left);
                    refs.Insert(pos, new QueueEntryRef(promoted.Settlement, promoted.Slot, promoted.ProjectId));
                }
            }
        }
        return new HoldsWalkResult(on, [.. result], purses, holderSets, start, drawn, held);
    }

    /// <summary>D-058: the entry of <paramref name="entry"/>'s settlement queued directly behind it — the lowest slot above
    /// <paramref name="entry"/>.Slot — or null when it is the last.</summary>
    private static ConstructionQueueRow? NextEntryAfter(IReadOnlyTable<ConstructionQueueRow> queue, ConstructionQueueRow entry)
    {
        int best = -1;
        for (int i = 0; i < queue.Count; i++)
        {
            ConstructionQueueRow row = queue[i];
            if (row.Settlement != entry.Settlement || row.Slot <= entry.Slot) continue;
            if (best < 0 || row.Slot < queue[best].Slot) best = i;
        }
        return best < 0 ? null : queue[best];
    }

    private static long[] Unbounded(int width)
    {
        var a = new long[width];
        Array.Fill(a, long.MaxValue);
        return a;
    }

    /// <summary>
    /// THE HOLD INVARIANTS (D-055), asserted by ConstructionSystem at the end of every step with the rule on, for every purse
    /// key and good of <paramref name="walk"/>: (i) Σ holds ≤ the purse's holding in <paramref name="stocksAfter"/> (after
    /// construction's draws) — no fictitious stock; (ii) Σ draws + Σ holds ≤ the starting budget, and the budget never went
    /// below 0 (every head was resolved against a budget ≥ 0) — no double counting; (iii) every head that sets anything
    /// aside is an unreleased head; (iv) no stock row is negative; (v) <paramref name="published"/> equals the per-purse sums
    /// row for row, and every good is in the goods registry; (vi) the first unreleased head of each purse was resolved
    /// against the whole starting budget, so no hold ever blocks the oldest head. Null when they all hold.
    /// </summary>
    public static string? HoldsInvariantViolation(
        HoldsWalkResult walk, GoodsConfig goods, IReadOnlyTable<GoodStockRow> stocksAfter, IReadOnlyTable<ConstructionHoldRow> published)
    {
        for (int k = 0; k < walk.Purses.Length; k++)
        {
            for (int g = 0; g < walk.Held[k].Length; g++)
            {
                long after = EmpireStockpile.Available(stocksAfter, walk.HolderSets[k], new GoodId(g));
                if (walk.Held[k][g] > after)
                    return Clause($"purse {walk.Purses[k]} sets aside {walk.Held[k][g]} of good {g} but holds {after} after construction's draws (no fictitious stock)");
                if (checked(walk.Drawn[k][g] + walk.Held[k][g]) > walk.Start[k][g])
                    return Clause($"purse {walk.Purses[k]} drew {walk.Drawn[k][g]} and set aside {walk.Held[k][g]} of good {g} from a budget of {walk.Start[k][g]} (no double counting)");
            }
            bool first = true;
            for (int h = 0; h < walk.Heads.Length; h++)
            {
                HoldsWalkHead head = walk.Heads[h];
                if (head.Purse != walk.Purses[k] || head.Released) continue;
                for (int g = 0; g < head.ResolvedAgainst.Length; g++)
                {
                    if (head.ResolvedAgainst[g] < 0)
                        return Clause($"the head of settlement {head.Entry.Settlement.Value} was resolved against a budget of {head.ResolvedAgainst[g]} of good {g}");
                    if (first && head.ResolvedAgainst[g] != walk.Start[k][g])
                        return Clause($"the oldest head of purse {walk.Purses[k]} (settlement {head.Entry.Settlement.Value}) was resolved against {head.ResolvedAgainst[g]} of good {g}, not the whole budget {walk.Start[k][g]}");
                }
                first = false;
            }
        }
        for (int h = 0; h < walk.Heads.Length; h++)
        {
            HoldsWalkHead head = walk.Heads[h];
            if (!head.Released) continue;
            for (int g = 0; g < head.Holds.Length; g++)
                if (head.Holds[g] != 0)
                    return Clause($"the released head of settlement {head.Entry.Settlement.Value} sets aside {head.Holds[g]} of good {g}");
        }
        for (int i = 0; i < stocksAfter.Count; i++)
            if (stocksAfter[i].Amount.Value < 0)
                return Clause($"settlement {stocksAfter[i].Settlement.Value} holds {stocksAfter[i].Amount.Value} of good {stocksAfter[i].Good.Value}");
        ConstructionHoldRow[] expected = walk.Rows();
        if (expected.Length != published.Count)
            return Clause($"the published table has {published.Count} rows, the walk {expected.Length}");
        for (int i = 0; i < expected.Length; i++)
        {
            if (published[i] != expected[i])
                return Clause($"published row {i} is (purse {published[i].Purse}, good {published[i].Good}, {published[i].Units}), the walk's (purse {expected[i].Purse}, good {expected[i].Good}, {expected[i].Units})");
            if (!InRegistry(goods, published[i].Good))
                return Clause($"published row {i} names good {published[i].Good}, which the goods registry does not define");
        }
        return null;
    }

    private static bool InRegistry(GoodsConfig goods, int id)
    {
        for (int i = 0; i < goods.Goods.Length; i++) if (goods.Goods[i].Id == id) return true;
        return false;
    }

    private static string Clause(FormattableString text) => FormattableString.Invariant(text);
}
