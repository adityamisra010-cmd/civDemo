using System.Collections.Immutable;
using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Ui.Ages;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Actions;

/// <summary>How the labour (and every other share-like) control is drawn and how fine its steps are.</summary>
public enum LabourControlKind
{
    /// <summary>A1: stones laid in heaps — ten of them, no numerals.</summary>
    Pebbles = 1,
    /// <summary>A2–A3: clay counters — ten of them, no numerals.</summary>
    Counters = 2,
    /// <summary>A4–A6: a notched rule of twenty, with numerals.</summary>
    Notches = 3,
    /// <summary>A7–A8: a slider in whole points, with numerals.</summary>
    Slider = 4,
    /// <summary>A9: a precise slider in whole points, with numerals and the applied share.</summary>
    PreciseSlider = 5,
}

/// <summary>
/// THE CONTROL SPEC of an era — derived ONLY from <see cref="ControlTokens.Granularity"/> (the era theme's
/// token, never the era number): coarse, numeral-free pebble and counter tallies while the civilization has
/// barely organised knowledge (A1–A3), a notched rule of twenty with numerals from the Standard eras, and
/// sliders in whole points with numerals at Fine and Precise granularity (A7–A9). <see cref="Units"/> is how
/// many units an allocation places (it always sums to exactly that); one unit is
/// <see cref="PercentPerUnit"/> percentage points of labour in the SectorAllocation order the control emits.
/// </summary>
public sealed record LabourControlSpec(LabourControlKind Kind, int Units, bool Numerals)
{
    public int PercentPerUnit => SectorAllocationModel.Total / Units;

    /// <summary>Whether the control is a continuous track (clicked or dragged) rather than a row of slots.</summary>
    public bool IsSlider => Kind is LabourControlKind.Slider or LabourControlKind.PreciseSlider;

    public static LabourControlSpec For(ControlGranularity granularity) => granularity switch
    {
        ControlGranularity.Coarse => new(LabourControlKind.Pebbles, 10, false),
        ControlGranularity.Simple => new(LabourControlKind.Counters, 10, false),
        ControlGranularity.Standard => new(LabourControlKind.Notches, 20, true),
        ControlGranularity.Fine => new(LabourControlKind.Slider, 100, true),
        _ => new(LabourControlKind.PreciseSlider, 100, true),
    };

    public static LabourControlSpec For(EraTheme theme) => For(theme.Controls.Granularity);
}

/// <summary>How the surface groups its entries — from <see cref="DensityTokens.Level"/>: a short flat list at
/// density 1 (A1), domain groups with headers at 2–3, denser detail at 4–5.</summary>
public enum SurfaceLayout { Flat = 1, Grouped = 2, Detailed = 3 }

/// <summary>One labour activity of the target settlement: its sector (the order's granularity), the label
/// of every identity it expresses now, what it produces, its share, its units in the era's control, and the
/// "new" marker on the turn research changed its identity.</summary>
public sealed record LabourEntry(
    int Sector, string Label, ImmutableArray<LabourIdentity> Identities, string Produces, double Share, int Units,
    bool Researched, string? NewMarker, string? LearnedFrom, ActionDescriptor Action);

/// <summary>The Empire-wide view of the labour the player commands: how many settlements, how many adults,
/// and each sector's adult-weighted share across them (sector order), with the sectors' current labels.</summary>
public sealed record LabourSummary(int Settlements, long Adults, ImmutableArray<double> Shares, ImmutableArray<string> Labels);

/// <summary>The labour control of ONE controlled settlement (the selection, or the capital when the selection
/// is not the player's to command).</summary>
public sealed record LabourBlock(
    SettlementId Settlement, string Name, string? Note, LabourControlSpec Control, ImmutableArray<LabourEntry> Entries,
    ImmutableArray<int> Current, ImmutableArray<int> Queued, LabourSummary Summary, ImmutableArray<SettlementId> Controlled)
{
    /// <summary>Whether a labour order for this settlement is queued this turn (it applies at End Turn).</summary>
    public bool HasQueued => !Queued.IsDefaultOrEmpty;

    /// <summary>What the control starts from: the queued split if one is waiting, else the running one.</summary>
    public ImmutableArray<int> Baseline => HasQueued ? Queued : Current;
}

/// <summary>A research node as the surface names it.</summary>
public sealed record ResearchItem(long Key, string Name, double Cost, double Progress, string Where);

/// <summary>
/// THE RESEARCH LINE: the active target with its progress, a target chosen this turn (it applies at End Turn),
/// the RP the pool earns per turn, retained partial progress, and whether the target can be cleared.
/// </summary>
public sealed record ResearchBlock(
    ResearchItem? Target, ResearchItem? Chosen, bool ClearQueued, double PointsPerTurn, int Available,
    ImmutableArray<ResearchItem> Retained, bool CanClear)
{
    /// <summary>The target the next End Turn will research: the one chosen this turn, else none if a clear is
    /// queued, else the current target.</summary>
    public ResearchItem? Effective => Chosen ?? (ClearQueued ? null : Target);

    /// <summary>Research points are idle: nothing will be researched at the next End Turn.</summary>
    public bool Idle => Effective is null;
}

/// <summary>The Age advance — listed only when the query lists it (eligible, not already ordered).</summary>
public sealed record AgeBlock(int Age, string Name, int NextAge, string NextName, ActionDescriptor Advance);

/// <summary>One construction project available in the target settlement.</summary>
public sealed record ProjectEntry(
    int ProjectId, string Name, string Materials, double LabourAdultYears, string? Blocker, long Built, int Queued,
    string? LearnedFrom, ActionDescriptor Action);

/// <summary>One queued project, head first.</summary>
public sealed record QueueEntry(int Slot, int ProjectId, string Name);

/// <summary>This turn's construction labour in adult-years: the construction share's whole pool, what housing
/// took (its published draw, a one-turn lag) and what is left for projects and path-making.</summary>
public sealed record CapacityLine(double Pool, double Housing, double Available, double DtYears);

/// <summary>The construction projects of the target settlement, its queue, the head's blocker and capacity.</summary>
public sealed record ConstructionBlock(
    SettlementId Settlement, string Name, ImmutableArray<ProjectEntry> Projects, ImmutableArray<QueueEntry> Queue,
    string? HeadBlocker, CapacityLine? Capacity, int OrderedThisTurn);

/// <summary>One developable inter-city route, ranked by use (the plan's order), with its present performance.</summary>
public sealed record RouteEntry(
    int A, int B, string Label, double LengthKm, long Usage, double ModernizedPercent, double CostFactor, long CapacityTonnesPerYear,
    string Cost);

/// <summary>Road development for the best class the Empire knows: the ranked routes, the first route's
/// blocker, whether a development is already ordered this turn.</summary>
public sealed record RoadsBlock(
    int TargetClass, string ClassName, ImmutableArray<RouteEntry> Routes, string? Blocker, string? LearnedFrom,
    double? OrderedPercent, ActionDescriptor Action);

/// <summary>What a road development of a given percentage would do now — an ESTIMATE on current stocks.</summary>
public sealed record RoadPlanPreview(
    double Percent, int Routes, string Cost, double Affordable, ImmutableArray<string> Payers, ImmutableArray<string> RouteLabels);

/// <summary>The tax edict: the declared levy, legitimacy, per-settlement collection, and a levy queued this turn.</summary>
public sealed record GovernanceBlock(
    int DeclaredPercent, bool HasPolicy, string Legitimacy, ImmutableArray<string> Collection, string? LearnedFrom,
    int? QueuedPercent, ActionDescriptor Action);

/// <summary>One formation on the roster card.</summary>
public sealed record FormationEntry(long Id, string Identity, string Family, int IdentityAge, string Where, string Line);

/// <summary>"Basic fighting": the roster card — information only, because no military order exists (ADR-033 D7).</summary>
public sealed record MilitaryBlock(
    ImmutableArray<FormationEntry> Formations, string? NextAgeName, ImmutableArray<string> Modernization, string Note, ActionDescriptor Capability);

/// <summary>What the people do on their own — standing capabilities, one compact list, no controls.</summary>
public sealed record StandingBlock(ImmutableArray<string> Items);

/// <summary>
/// THE ACTION SURFACE — "what can this civilization actually do now?", as one read-only model (ADR-033 D1/D2).
/// EVERY block is present iff <see cref="AvailableActionsQuery"/> returned that domain's descriptors (labour and
/// construction: for the target settlement); nothing here decides availability, so a locked future action is
/// never listed and no future control is ever shown disabled. The era decides only HOW it is shown
/// (<see cref="Control"/>, <see cref="Layout"/>).
/// </summary>
public sealed record ActionSurfaceModel(
    UiEra Era, SurfaceLayout Layout, LabourControlSpec Control, ImmutableArray<ActionDescriptor> Actions,
    LabourBlock? Labour, ResearchBlock? Research, AgeBlock? Age, ConstructionBlock? Construction, RoadsBlock? Roads,
    MilitaryBlock? Military, GovernanceBlock? Governance, StandingBlock? Standing, ImmutableArray<string> Notices)
{
    /// <summary>The domains the surface shows, in the query's domain order.</summary>
    public ImmutableArray<ActionDomain> Domains
    {
        get
        {
            var b = ImmutableArray.CreateBuilder<ActionDomain>();
            if (Labour is not null) b.Add(ActionDomain.Labour);
            if (Research is not null) b.Add(ActionDomain.Research);
            if (Age is not null) b.Add(ActionDomain.Age);
            if (Construction is not null) b.Add(ActionDomain.Construction);
            if (Roads is not null) b.Add(ActionDomain.Roads);
            if (Military is not null) b.Add(ActionDomain.Military);
            if (Governance is not null) b.Add(ActionDomain.Governance);
            if (Standing is not null) b.Add(ActionDomain.Standing);
            return b.ToImmutable();
        }
    }
}

/// <summary>Everything the surface is built from: the world on screen, the world the last End Turn stepped
/// from (for "new this turn"), the session's config and era table (the next step's dt), the player's Empire,
/// the selection, the not-yet-stepped orders, the name registry and the era theme.</summary>
public sealed record ActionSurfaceInput(
    IReadOnlyWorldState World, IReadOnlyWorldState? Previous, SimConfig Config, EraTable Era, PolityId Player,
    int Selected, IReadOnlyList<OrderRecord> Queued, Func<int, string> Name, EraTheme Theme);

/// <summary>
/// BUILDS the action surface (ADR-033 D1/D2). Pure and read-only: it calls
/// <see cref="AvailableActionsQuery.For"/> once (with the next step's context) and <see cref="LabourActivities.For"/>
/// once, reads the owning domains' public readers for what a descriptor names (a project's materials, a route's
/// performance, a formation's line, the legitimacy line), and never re-derives availability. Deterministic:
/// descriptor order, settlement-table order, sector order; no dictionary, no LINQ.
/// </summary>
public static class ActionSurface
{
    /// <summary>The model for <paramref name="input"/>.</summary>
    public static ActionSurfaceModel Build(ActionSurfaceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        IReadOnlyWorldState w = input.World;
        var context = ActionQueryContext.ForNextStep(w, input.Era, input.Queued);
        ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, input.Config, input.Player, context);
        LabourControlSpec control = LabourControlSpec.For(input.Theme);
        SurfaceLayout layout = input.Theme.Density.Level <= 1 ? SurfaceLayout.Flat
            : input.Theme.Density.Level <= 3 ? SurfaceLayout.Grouped : SurfaceLayout.Detailed;

        var notices = new List<string>();
        LabourBlock? labour = Labour(input, actions, control, notices);
        SettlementId? target = labour?.Settlement ?? TargetSettlement(input, actions, ActionDomain.Construction);
        return new ActionSurfaceModel(
            input.Theme.Era, layout, control, actions,
            labour,
            Research(input, actions),
            Age(input, actions),
            target is { } t ? Construction(input, actions, t, context.NextDtYears) : null,
            Roads(input, actions),
            Military(input, actions),
            Governance(input, actions),
            Standing(actions),
            Notices(input, actions, notices));
    }

    /// <summary>The surface for a live session: its world, previous world, config, era table and queued orders.</summary>
    public static ActionSurfaceModel ForSession(UiSession session, EraTable era, int selected, EraTheme theme) =>
        Build(new ActionSurfaceInput(session.World, session.PreviousWorld, session.Config, era, UiPlayer.Empire,
            selected, session.QueuedOrders(), id => session.Names.Name(id), theme));

    // ------------------------------------------------------------------ labour

    /// <summary>The settlements the query lists a domain's actions for, in descriptor (settlement-table) order.</summary>
    public static ImmutableArray<SettlementId> SettlementsListed(ImmutableArray<ActionDescriptor> actions, ActionDomain domain)
    {
        var ids = new List<int>();
        foreach (ActionDescriptor a in actions)
        {
            if (a.Domain != domain || a.Targets.IsDefaultOrEmpty || a.Targets[0].Kind != ActionTargetKind.Settlement) continue;
            int id = (int)a.Targets[0].Id;
            if (!ids.Contains(id)) ids.Add(id);
        }
        var b = ImmutableArray.CreateBuilder<SettlementId>(ids.Count);
        foreach (int id in ids) b.Add(new SettlementId(id));
        return b.MoveToImmutable();
    }

    /// <summary>The settlement a per-settlement domain is shown for: the selection when the query lists that
    /// domain for it, else the capital when listed, else the first settlement listed; null when none is.</summary>
    public static SettlementId? TargetSettlement(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions, ActionDomain domain)
    {
        ImmutableArray<SettlementId> listed = SettlementsListed(actions, domain);
        if (listed.IsEmpty) return null;
        foreach (SettlementId s in listed) if (s.Value == input.Selected) return s;
        if (EmpireQuery.TryGetCapital(input.World, input.Player, out SettlementId cap))
            foreach (SettlementId s in listed) if (s.Value == cap.Value) return s;
        return listed[0];
    }

    private static LabourBlock? Labour(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions, LabourControlSpec control, List<string> notices)
    {
        if (TargetSettlement(input, actions, ActionDomain.Labour) is not { } target) return null;
        IReadOnlyWorldState w = input.World;
        ImmutableArray<LabourActivity> activities = LabourActivities.For(w, input.Config, input.Player);
        ImmutableArray<LabourActivity> before = input.Previous is { } prev ? LabourActivities.For(prev, input.Config, input.Player) : [];

        var entries = ImmutableArray.CreateBuilder<LabourEntry>(Sectors.Count);
        foreach (ActionDescriptor a in actions)
        {
            if (a.Domain != ActionDomain.Labour || a.Targets[0].Id != target.Value) continue;
            int sector = (int)a.Targets[1].Id;
            LabourActivity activity = Find(activities, target, sector)
                ?? throw new InvalidOperationException("the query listed a labour action LabourActivities does not know");
            string? marker = NewMarker(activity, before, sector);
            if (marker is not null && !notices.Contains(marker)) notices.Add(marker);
            entries.Add(new LabourEntry(
                sector, a.Label, activity.Identities, Produces(activity), activity.Share, 0, activity.Researched, marker,
                a.Provenance.Researched ? string.Join(", ", a.Provenance.NodeNames) : null, a));
        }

        // The running split in the era's units; the queued split, when this turn already ordered one.
        SectorAllocationRow running = LabourActivities.AllocationOf(w, target);
        int[] current = new int[Sectors.Count];
        SectorAllocationModel.FromShares(running, current, control.Units);
        ImmutableArray<int> queued = [];
        if (QueuedAllocation(input.Queued, input.Player, target, running) is { } row)
        {
            int[] q = new int[Sectors.Count];
            SectorAllocationModel.FromShares(row, q, control.Units);
            queued = [.. q];
        }
        var withUnits = ImmutableArray.CreateBuilder<LabourEntry>(entries.Count);
        foreach (LabourEntry e in entries) withUnits.Add(e with { Units = current[e.Sector] });

        ImmutableArray<SettlementId> controlled = SettlementsListed(actions, ActionDomain.Labour);
        string? note = target.Value == input.Selected ? null
            : input.Selected >= 0 ? input.Name(input.Selected) + " is not ours to command; showing " + input.Name(target.Value) + "."
            : null;
        return new LabourBlock(target, input.Name(target.Value), note, control, withUnits.MoveToImmutable(), [.. current], queued,
            Summary(w, activities, controlled), controlled);
    }

    private static LabourActivity? Find(ImmutableArray<LabourActivity> activities, SettlementId settlement, int sector)
    {
        foreach (LabourActivity a in activities)
            if (a.Settlement.Value == settlement.Value && a.Sector == sector) return a;
        return null;
    }

    /// <summary>"new: Farming (Root and tuber cultivation)" when a researched identity the sector expresses now
    /// was not among the identities it expressed in the world the last End Turn stepped from.</summary>
    private static string? NewMarker(LabourActivity now, ImmutableArray<LabourActivity> before, int sector)
    {
        if (before.IsDefaultOrEmpty) return null;
        LabourActivity? was = null;
        foreach (LabourActivity a in before) if (a.Sector == sector) { was = a; break; }   // identities are Empire-wide
        if (was is null) return null;
        foreach (LabourIdentity id in now.Identities)
        {
            if (!id.Researched) continue;
            bool known = false;
            foreach (LabourIdentity old in was.Identities) if (string.Equals(old.Id, id.Id, StringComparison.Ordinal)) known = true;
            if (!known) return "new: " + id.Name + (id.NodeNames.IsDefaultOrEmpty ? "" : " (" + string.Join(", ", id.NodeNames) + ")");
        }
        return null;
    }

    /// <summary>What a sector's labour makes: its goods (LabourActivities, production's own classification), or
    /// for Construction — which makes no good — what its labour builds.</summary>
    private static string Produces(LabourActivity a)
    {
        if (a.Sector == Sectors.Construction) return "dwellings, paths and projects";
        return a.GoodNames.IsDefaultOrEmpty ? "nothing yet" : string.Join(", ", a.GoodNames);
    }

    /// <summary>The settlement's allocation row as the next End Turn will leave it, when the player has queued
    /// SectorAllocation orders for it this turn: PathBuildSystem applies them in log order, each setting one
    /// sector's raw weight (the same <see cref="Sectors.With"/> SectorOrderFactory.Preview uses). Null when none.</summary>
    public static SectorAllocationRow? QueuedAllocation(
        IReadOnlyList<OrderRecord> queued, PolityId player, SettlementId settlement, SectorAllocationRow running)
    {
        SectorAllocationRow row = running;
        bool any = false;
        for (int i = 0; i < queued.Count; i++)
        {
            OrderRecord o = queued[i];
            if (o.Kind != OrderKind.SectorAllocation || o.ActorId != player.Value || o.TargetId >> 3 != settlement.Value) continue;
            row = Sectors.With(row, o.TargetId & 7, o.Amount / 100.0);
            any = true;
        }
        return any ? row : null;
    }

    private static LabourSummary Summary(IReadOnlyWorldState w, ImmutableArray<LabourActivity> activities, ImmutableArray<SettlementId> controlled)
    {
        var weighted = new double[Sectors.Count];
        var labels = new string[Sectors.Count];
        long adults = 0;
        foreach (SettlementId s in controlled)
        {
            long a = BandViews.Adults(w.Buckets, s);
            adults += a;
            foreach (LabourActivity act in activities)
            {
                if (act.Settlement.Value != s.Value) continue;
                weighted[act.Sector] += act.Share * a;
                labels[act.Sector] = act.Label;
            }
        }
        var shares = ImmutableArray.CreateBuilder<double>(Sectors.Count);
        for (int s = 0; s < Sectors.Count; s++) shares.Add(adults > 0 ? weighted[s] / adults : 0.0);
        for (int s = 0; s < Sectors.Count; s++) labels[s] ??= "";
        return new LabourSummary(controlled.Length, adults, shares.MoveToImmutable(), [.. labels]);
    }

    /// <summary>The sectors in order of their summary share, largest first; ties by sector id (a composite
    /// (share, id) key — the shares are doubles).</summary>
    public static int[] RankedSectors(ImmutableArray<double> shares)
    {
        int[] order = new int[shares.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (x, y) => shares[x] != shares[y] ? shares[y].CompareTo(shares[x]) : x.CompareTo(y));
        return order;
    }

    // ------------------------------------------------------------------ research

    private static ResearchBlock? Research(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions)
    {
        if (input.Config.Research is not { } content) return null;
        ActionDescriptor? set = null, clear = null;
        foreach (ActionDescriptor a in actions)
        {
            if (a.Domain != ActionDomain.Research) continue;
            if (string.Equals(a.Key, "research.set-target", StringComparison.Ordinal)) set = a;
            if (string.Equals(a.Key, "research.clear-target", StringComparison.Ordinal)) clear = a;
        }
        if (set is null && clear is null) return null;

        ResearchItem? target = null;
        if (clear is not null) target = Item(clear.Targets[0]);
        var retained = ImmutableArray.CreateBuilder<ResearchItem>();
        if (set is not null)
            foreach (ActionTarget t in set.Targets)
            {
                if (t.Current) target ??= Item(t);
                else if (t.Progress is > 0.0) retained.Add(Item(t));
            }

        int? queued = QueuedResearchChoice(input.Queued, input.Player);
        ResearchItem? chosen = null;
        if (queued is > 0 && content.IndexOf(new ResearchNodeId(queued.Value)) is int idx and >= 0)
        {
            ResearchNode n = content.Nodes[idx];
            chosen = new ResearchItem(n.Key.Value, n.Name, ResearchQuery.EffectiveCost(input.World, content, input.Player, idx),
                ResearchQuery.Progress(input.World, input.Player, n.Key), "");
            // A retained node chosen this turn is the choice, not a leftover.
            for (int i = retained.Count - 1; i >= 0; i--) if (retained[i].Key == chosen.Key) retained.RemoveAt(i);
        }
        bool clearQueued = queued == -1;
        bool canClear = !clearQueued && (clear is not null || chosen is not null);
        return new ResearchBlock(target, chosen, clearQueued, ResearchQuery.ResearchPointPool(input.World, content, input.Player),
            set?.Targets.Length ?? 0, retained.ToImmutable(), canClear);
    }

    private static ResearchItem Item(ActionTarget t) => new(t.Id, t.Label, t.Cost ?? 0.0, t.Progress ?? 0.0, t.Detail ?? "");

    /// <summary>The player's research directive queued this turn as ResearchSystem will apply it: the LAST of its
    /// SetResearchTarget orders in log order (a key, −1 to clear), or null.</summary>
    public static int? QueuedResearchChoice(IReadOnlyList<OrderRecord> queued, PolityId player)
    {
        int? choice = null;
        for (int i = 0; i < queued.Count; i++)
            if (queued[i].Kind == OrderKind.SetResearchTarget && queued[i].ActorId == player.Value) choice = queued[i].TargetId;
        return choice;
    }

    // ------------------------------------------------------------------ Age

    private static AgeBlock? Age(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions)
    {
        if (input.Config.Ages is not { } ages) return null;
        foreach (ActionDescriptor a in actions)
        {
            if (a.Domain != ActionDomain.Age) continue;
            int current = AgeQuery.CurrentAge(input.World, ages, input.Player);
            return new AgeBlock(current, ages.Age(current).Name, (int)a.Id, ages.Age((int)a.Id).Name, a);
        }
        return null;
    }

    // ------------------------------------------------------------------ construction

    private static ConstructionBlock? Construction(
        ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions, SettlementId settlement, double? dtYears)
    {
        if (input.Config.Goods is not { } goods) return null;
        IReadOnlyWorldState w = input.World;
        var projects = ImmutableArray.CreateBuilder<ProjectEntry>();
        foreach (ActionDescriptor a in actions)
        {
            if (a.Domain != ActionDomain.Construction || a.Targets[0].Id != settlement.Value) continue;
            int projectId = (int)a.Targets[1].Id;
            ConstructionProjectEntry p = goods.ProjectById(projectId)!;
            int queuedHere = 0;
            foreach (ConstructionQueueRow r in ConstructionQuery.Queue(w, settlement)) if (r.ProjectId == projectId) queuedHere++;
            projects.Add(new ProjectEntry(projectId, a.Targets[1].Label, Materials(p), p.LaborRequired, a.Blocker,
                ConstructionQuery.Built(w, settlement, projectId), queuedHere,
                a.Provenance.Researched ? string.Join(", ", a.Provenance.NodeNames) : null, a));
        }
        if (projects.Count == 0) return null;

        var queue = ImmutableArray.CreateBuilder<QueueEntry>();
        string? headBlocker = null;
        ConstructionQueueRow[] rows = ConstructionQuery.Queue(w, settlement);
        for (int i = 0; i < rows.Length; i++)
        {
            ConstructionProjectEntry? p = goods.ProjectById(rows[i].ProjectId);
            queue.Add(new QueueEntry(rows[i].Slot, rows[i].ProjectId, p is null ? "project " + Inv(rows[i].ProjectId) : NameOf(projects, rows[i].ProjectId, p.Name)));
            if (i == 0 && p is not null) headBlocker = ConstructionQuery.Blocker(w, goods, settlement, p, dtYears);
        }

        CapacityLine? capacity = null;
        if (dtYears is { } dt && dt > 0.0)
        {
            double pool = Sectors.Share(LabourActivities.AllocationOf(w, settlement), Sectors.Construction) * BandViews.Adults(w.Buckets, settlement) * dt;
            double housing = 0.0;
            for (int i = 0; i < w.Housing.Count; i++)
                if (w.Housing[i].Settlement == settlement) { housing = w.Housing[i].LastLaborUsed; break; }
            capacity = new CapacityLine(pool, housing, ConstructionQuery.CapacityAdultYears(w, settlement, dt), dt);
        }

        int ordered = 0;
        for (int i = 0; i < input.Queued.Count; i++)
            if (input.Queued[i].Kind == OrderKind.EnqueueConstruction && input.Queued[i].ActorId == input.Player.Value
                && input.Queued[i].TargetId == settlement.Value) ordered++;
        return new ConstructionBlock(settlement, input.Name(settlement.Value), projects.ToImmutable(), queue.ToImmutable(),
            headBlocker, capacity, ordered);
    }

    private static string NameOf(ImmutableArray<ProjectEntry>.Builder projects, int id, string fallback)
    {
        foreach (ProjectEntry p in projects) if (p.ProjectId == id) return p.Name;
        return fallback;
    }

    private static string Materials(ConstructionProjectEntry p)
    {
        var parts = new List<string>(p.Inputs.Length);
        foreach (ProjectInput i in p.Inputs) parts.Add(Inv(i.Qty) + " " + i.Good);
        return string.Join(", ", parts);
    }

    // ------------------------------------------------------------------ roads (read only: transport is frozen)

    private static RoadsBlock? Roads(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions)
    {
        ActionDescriptor? road = null;
        foreach (ActionDescriptor a in actions) if (a.Domain == ActionDomain.Roads) { road = a; break; }
        if (road is null || input.Config.Roads is not { } roads || input.Config.Research is not { } research || input.Config.Goods is not { } goods)
            return null;
        RoadRouteView[] views = RoadDevelopmentQuery.Describe(input.World, research, roads, goods, input.Player);
        var routes = ImmutableArray.CreateBuilder<RouteEntry>(road.Targets.Length);
        foreach (ActionTarget t in road.Targets)
        {
            int a = (int)(t.Id >> 32), b = (int)(t.Id & 0xFFFFFFFF);
            RoadRouteView? v = null;
            foreach (RoadRouteView rv in views) if (rv.Route.A.Value == a && rv.Route.B.Value == b) { v = rv; break; }
            if (v is not { } view) continue;
            routes.Add(new RouteEntry(a, b, input.Name(a) + " - " + input.Name(b), view.Route.LengthKm, view.Route.Usage,
                view.ModernizationPercent, view.EffectiveCostFactor, view.CapacityTonnesPerYear, Costs(goods, view.EstimatedCost)));
        }
        double? ordered = null;
        for (int i = 0; i < input.Queued.Count; i++)
            if (input.Queued[i].Kind == OrderKind.DevelopRoads && input.Queued[i].ActorId == input.Player.Value) { ordered = input.Queued[i].Amount; break; }
        string className = road.Label.StartsWith("Develop roads (", StringComparison.Ordinal) && road.Label.EndsWith(')')
            ? road.Label["Develop roads (".Length..^1] : road.Label;
        return new RoadsBlock((int)road.Id, className, routes.MoveToImmutable(), road.Blocker,
            road.Provenance.Researched ? string.Join(", ", road.Provenance.NodeNames) : null, ordered, road);
    }

    /// <summary>What a DevelopRoads of <paramref name="percent"/>% would select now and cost — read through
    /// <see cref="RoadOrderFactory.Preview"/> (= RoadDevelopmentQuery.Plan, the selection the system applies),
    /// with the affordable fraction of the whole on the CURRENT stocks of the Empire's paying settlements. An
    /// ESTIMATE: production runs before road development in the step that applies the order.</summary>
    public static RoadPlanPreview RoadPlan(IReadOnlyWorldState world, SimConfig cfg, PolityId player, double percent, Func<int, string> name)
    {
        RoadDevelopmentStep[] plan = RoadOrderFactory.Preview(world, cfg, player, percent);
        if (plan.Length == 0 || cfg.Goods is not { } goods) return new RoadPlanPreview(percent, 0, "nothing", 1.0, [], []);
        var total = new List<RoadMaterialCost>();
        var labels = ImmutableArray.CreateBuilder<string>(plan.Length);
        foreach (RoadDevelopmentStep step in plan)
        {
            labels.Add(name(step.Route.A.Value) + " - " + name(step.Route.B.Value));
            foreach (RoadMaterialCost c in step.Cost)
            {
                int at = -1;
                for (int i = 0; i < total.Count; i++) if (total[i].Good.Value == c.Good.Value) at = i;
                if (at < 0) total.Add(c); else total[at] = new RoadMaterialCost(c.Good, total[at].Units + c.Units);
            }
        }
        RoadMaterialCost[] sum = [.. total];
        SettlementId[] payers = RoadDevelopmentQuery.PayingSettlements(world, player, plan[0].Route.A, plan[0].Route.B);
        double affordable = RoadDevelopmentQuery.AffordableFraction(world.GoodStocks, payers, sum);
        var payerNames = ImmutableArray.CreateBuilder<string>(payers.Length);
        foreach (SettlementId p in payers) payerNames.Add(name(p.Value));
        return new RoadPlanPreview(percent, plan.Length, Costs(goods, sum), affordable, payerNames.MoveToImmutable(), labels.MoveToImmutable());
    }

    private static string Costs(GoodsConfig goods, RoadMaterialCost[] cost)
    {
        if (cost.Length == 0) return "nothing";
        var parts = new List<string>(cost.Length);
        foreach (RoadMaterialCost c in cost) parts.Add(Inv(c.Units) + " " + goods.ById(c.Good.Value).Name);
        return string.Join(", ", parts);
    }

    // ------------------------------------------------------------------ military (information only)

    private static MilitaryBlock? Military(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions)
    {
        ActionDescriptor? fighting = null;
        foreach (ActionDescriptor a in actions) if (a.Domain == ActionDomain.Military) { fighting = a; break; }
        if (fighting is null) return null;
        UnitFamilyContent? families = input.Config.UnitFamilies;
        MilitaryUnitRow[] units = MilitaryQuery.Units(input.World, input.Player);
        var formations = ImmutableArray.CreateBuilder<FormationEntry>(fighting.Targets.Length);
        foreach (ActionTarget t in fighting.Targets)
        {
            MilitaryUnitRow? row = null;
            foreach (MilitaryUnitRow u in units) if (u.Id == t.Id) { row = u; break; }
            if (row is not { } unit) continue;
            UnitIdentity? identity = families?.IdentityByKey(unit.Identity);
            UnitFamily? family = families?.FamilyByKey(unit.Family);
            formations.Add(new FormationEntry(t.Id, t.Label, family?.Name ?? "family " + Inv(unit.Family), identity?.Age ?? 0,
                unit.Location.Value >= 0 ? input.Name(unit.Location.Value) : "in the field", Line(family, identity)));
        }
        string? next = null;
        var modern = ImmutableArray.CreateBuilder<string>();
        if (AdvanceFlowModel.Build(input.World, input.Config.Ages, families, input.Player) is { } flow)
        {
            next = flow.ToAgeName;
            foreach (ModernizationLine l in flow.Modernization)
                modern.Add(Inv(l.Count) + " x " + l.From + (l.Changes ? " -> " + l.To : " - " + AdvanceFlowModel.OutcomeText(l.Outcome)));
        }
        return new MilitaryBlock(formations.ToImmutable(), next, modern.ToImmutable(),
            "Recruitment, movement and battle are not yet simulated: there is no military order.", fighting);
    }

    /// <summary>The family's mainline from the formation's current identity: "Warband -> Axe warriors -> Bronze swordsmen".</summary>
    private static string Line(UnitFamily? family, UnitIdentity? current)
    {
        if (family is null || current is null) return "";
        var names = new List<string>();
        bool from = false;
        foreach (UnitIdentity i in family.Line)
        {
            if (i.Branch) continue;
            if (i.Key == current.Key) from = true;
            if (from) names.Add(i.Name);
            if (names.Count == 3) break;
        }
        return string.Join(" -> ", names);
    }

    // ------------------------------------------------------------------ governance

    private static GovernanceBlock? Governance(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions)
    {
        ActionDescriptor? edict = null;
        foreach (ActionDescriptor a in actions) if (a.Domain == ActionDomain.Governance) { edict = a; break; }
        if (edict is null) return null;
        IReadOnlyWorldState w = input.World;
        int? queued = null;
        for (int i = 0; i < input.Queued.Count; i++)
            if (input.Queued[i].Kind == OrderKind.SetTaxRate && input.Queued[i].ActorId == input.Player.Value) queued = (int)input.Queued[i].Amount;
        return new GovernanceBlock(
            TaxOrderFactory.DeclaredPercent(w, input.Player), Sim.Core.State.Governance.HasPolicy(w, input.Player),
            TaxOrderFactory.LegitimacyLine(w, input.Player, input.Config),
            [.. TaxOrderFactory.BurdenLines(w, input.Player, input.Config, s => input.Name(s.Value))],
            edict.Provenance.Researched ? string.Join(", ", edict.Provenance.NodeNames) : null, queued, edict);
    }

    // ------------------------------------------------------------------ standing

    private static StandingBlock? Standing(ImmutableArray<ActionDescriptor> actions)
    {
        var items = ImmutableArray.CreateBuilder<string>();
        foreach (ActionDescriptor a in actions)
            if (a.Domain == ActionDomain.Standing) items.Add(Short(a.Label));
        return items.Count == 0 ? null : new StandingBlock(items.ToImmutable());
    }

    /// <summary>A content name without its parenthetical gloss ("Basic fishing (shore, net, trap and spear)" → "Basic fishing").</summary>
    public static string Short(string name)
    {
        int at = name.IndexOf(" (", StringComparison.Ordinal);
        return at > 0 ? name[..at] : name;
    }

    // ------------------------------------------------------------------ notices

    /// <summary>What changed in the action space at the last End Turn: research completed, new activity
    /// identities (already gathered), and whole domains the query lists now but did not list before.</summary>
    private static ImmutableArray<string> Notices(ActionSurfaceInput input, ImmutableArray<ActionDescriptor> actions, List<string> identities)
    {
        var lines = new List<string>();
        if (input.Previous is { } prev && input.Config.Research is { } content)
        {
            foreach (ResearchNodeId k in ResearchQuery.CompletedBetween(prev, input.World, input.Player))
                if (content.IndexOf(k) is int i and >= 0) lines.Add("learned: " + content.Nodes[i].Name);
            lines.AddRange(identities);
            ImmutableArray<ActionDescriptor> before = AvailableActionsQuery.For(prev, input.Config, input.Player);
            foreach (ActionDomain d in new[] { ActionDomain.Construction, ActionDomain.Roads, ActionDomain.Governance, ActionDomain.Institutions, ActionDomain.Age })
            {
                bool now = false, was = false;
                foreach (ActionDescriptor a in actions) if (a.Domain == d) { now = true; break; }
                foreach (ActionDescriptor a in before) if (a.Domain == d) { was = true; break; }
                if (now && !was) lines.Add("new: " + DomainNoun(d));
            }
        }
        return [.. lines];
    }

    /// <summary>A domain as the notices and the grouped headers name it.</summary>
    public static string DomainNoun(ActionDomain d) => d switch
    {
        ActionDomain.Labour => "work",
        ActionDomain.Research => "learning",
        ActionDomain.Age => "advancing to the next Age",
        ActionDomain.Construction => "building projects",
        ActionDomain.Roads => "road development",
        ActionDomain.Military => "arms",
        ActionDomain.Governance => "the tax edict",
        ActionDomain.Institutions => "institutions",
        _ => "what the people do on their own",
    };

    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);
}
