using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// The played session as a TESTABLE seam (T1.9 adversarial hardening): order
/// stamping, executor construction, End Turn, and session-log persistence all
/// live HERE — SimUiGame and Program.cs only call it. The adversarial pass
/// showed the previous shape let order-timing drift (Clock.Turn+1 stamping),
/// pipeline drift (a UI-only preset), and stamped-filename drift all ship with
/// every test green, because the real call sites were private on a MonoGame
/// Game class no test instantiates. The UiSessionReplay test now drives THIS
/// code end-to-end and replays its log hash-for-hash.
/// </summary>
public sealed class UiSession
{
    public WorldState World { get; private set; }
    public OrderLog Orders { get; }
    private readonly TurnExecutor _executor;

    // T2.9 chronicle-lite: names + event detection + annal prose. UI-side
    // history in the D-028 sense — replay through the same UiSession rebuilds
    // it; it never touches WorldState.
    private readonly Sim.Core.Chronicle.ChronicleConfig _chronicleCfg;
    private readonly Sim.Core.Chronicle.ChronicleCollector _chronicle;
    private readonly List<string> _annals = [];
    private int _renderedEvents;
    // Item 3: the annal events the Sim.Core chronicle does not detect, derived UI-side from (prev, next).
    private readonly ViewModel.StateChronicle _events = new();

    /// <summary>Settlement id → name (deterministic from world seed; ADR-001
    /// registry, never sim rows).</summary>
    public Sim.Core.Chronicle.NameRegistry Names { get; private set; }

    /// <summary>The annals, oldest first (the panel renders newest LAST).</summary>
    public IReadOnlyList<string> AnnalLines => _annals;

    // THE SESSION RECORD (T4.17). The seed and overrides are kept because a
    // session log without them is UNREPLAYABLE — the world cannot be rebuilt,
    // so nothing downstream of it can run. They are written to the manifest.
    private readonly ulong _seed;
    private readonly int? _sizePx;
    private readonly int? _settlements;
    // ADR-033 D5: the AI-empire override the world was founded with (null = worldgen.json's
    // aiEmpires, whose default is 0). Recorded in the manifest and the forensic record because a
    // session founded with AI Empires replays only into the same world.
    private readonly int? _aiEmpires;
    private readonly int _grainGoodId;
    private readonly List<string> _trace = [SessionTrace.Header];

    // T4.19 — THE GLASS BOX. One TurnRecord + SettlementRecord[] per End Turn,
    // built from (prev, next, cfg, ordersApplied) after the step. The log holds
    // records only — never a world — and nothing in the executor or any system
    // can reach it, so it cannot change a hash; that is also MEASURED, not
    // assumed (TelemetryTests: hash equality with and without a log).
    private readonly SimConfig _simCfg;

    // M5 S1c: the worldgen config the production pipeline's ColonizationSystem is built from (ProductionPipeline),
    // which the AI's Found Settlement orders are judged with — loaded once, read by AppendAiOrders and (P8) Sites only.
    private readonly Sim.Core.Worldgen.WorldgenConfig _worldgen = ProductionWorldgen();

    // M5 P8: the terrain half of a site assessment for the map this session plays on.
    private SiteAssessment.Prepared? _sites;

    /// <summary>
    /// M5 map workstream P8 — the terrain half of the map-cell card's <see cref="SiteAssessment"/> (the siting score
    /// raster and its two factor rasters, the frontier lattice): a pure function of the immutable terrain and the configs,
    /// built the first time a place's card is asked for and kept while the world's terrain is the same one (every clone
    /// of a world shares it). Built with the worldgen config this session's ColonizationSystem holds, so the card's
    /// deposit preview is the founding path's own values. UI-side only — the TerrainBaker precedent; nothing in the
    /// simulation reads it. Null for a world without terrain.
    /// </summary>
    public SiteAssessment.Prepared? Sites
    {
        get
        {
            if (World.Terrain is null) return null;
            if (_sites is null || !_sites.IsFor(World.Terrain)) _sites = SiteAssessment.Prepare(World, _simCfg, _worldgen);
            return _sites;
        }
    }

    // M5 P9: the resource overlay's bake and the turn's status, kept while their inputs are the same.
    private Sim.Ui.World.ResourceOverlay? _overlay;
    private IReadOnlyList<Sim.Ui.World.OverlayLayer>? _overlayLayers;
    private (WorldState World, PlayerKnowledge Knowledge, Sim.Ui.World.ResourceOverlay Bake, Sim.Ui.World.OverlayStatus Status)? _overlayStatus;

    /// <summary>
    /// M5 map workstream P9 — the resource overlay's layers for this session's content (the deposit channels, then the
    /// siting score and its two factors): what the overlay controls list. Cheap — no terrain pass.
    /// </summary>
    public IReadOnlyList<Sim.Ui.World.OverlayLayer> OverlayLayers =>
        _overlayLayers ??= World.Terrain is { } t ? Sim.Ui.World.ResourceOverlay.LayersOf(_simCfg, _worldgen, t.KmPerPx) : [];

    /// <summary>
    /// M5 map workstream P9 — THE RESOURCE OVERLAY's bake (<see cref="Sim.Ui.World.ResourceOverlay"/>) for what the player
    /// knows: a pure function of the terrain, the configs and the knowledge masks, baked the first time an overlay layer is
    /// shown and kept while they are the same (in production — all-known — once per session). UI-side only, the terrain
    /// bake's precedent; nothing in the simulation reads it. Null for a world without terrain.
    /// </summary>
    public Sim.Ui.World.ResourceOverlay? Overlay
    {
        get
        {
            if (Sites is not { } site) return null;
            if (_overlay is null || !_overlay.IsFor(site, Knowledge.Core)) _overlay = Sim.Ui.World.ResourceOverlay.Bake(_simCfg, site, Knowledge.Core);
            return _overlay;
        }
    }

    /// <summary>M5 P9: the overlay key's per-turn status (access, extraction, the realized ore rolls) for the world and
    /// knowledge of now; null without terrain. Kept while the world, the knowledge and the bake are the same.</summary>
    public Sim.Ui.World.OverlayStatus? OverlayStatus
    {
        get
        {
            if (Overlay is not { } bake) return null;
            if (_overlayStatus is { } c && ReferenceEquals(c.World, World) && ReferenceEquals(c.Knowledge, Knowledge) && ReferenceEquals(c.Bake, bake))
                return c.Status;
            Sim.Ui.World.OverlayStatus status = bake.Status(World, _simCfg, Knowledge);
            _overlayStatus = (World, Knowledge, bake, status);
            return status;
        }
    }

    /// <summary>The observation history the UI graphs and panels read through
    /// (docs/observability-architecture.md §7: one read seam).</summary>
    public IObservationHistory Observations => _observations;
    private readonly ObservationLog _observations = new();

    /// <summary>T4.19 lane B: the world the last End Turn STEPPED FROM, or null
    /// before the first. The explain queries are pure functions of (prev, next,
    /// cfg) — GrievanceExplanation, CausalChain, MigrationExplanation — and the
    /// observation log deliberately retains no world (§7), so the session holds
    /// the one previous world the UI needs. Safe to hold: TurnExecutor.Step
    /// never mutates prev and returns a fresh clone, so this reference is the
    /// same immutable pair the observer read. Nothing in the executor or any
    /// system can reach it.</summary>
    public WorldState? PreviousWorld { get; private set; }

    /// <summary>The loaded SimConfig — the same object the observer and the
    /// executor recipe load from the data files, exposed so the explain
    /// queries the UI runs read the registry the simulation ran with.</summary>
    public SimConfig Config => _simCfg;

    /// <summary>The live turn trace, one line per observed turn beneath the
    /// header — what the world looked like as it was actually played.</summary>
    public IReadOnlyList<string> TraceLines => _trace;

    /// <summary>The last turn this session reached — what a replay of its log
    /// must be run for.</summary>
    public long TurnsPlayed => World.Clock.Turn;

    /// <summary>T2.10, D-028: the graphs' ring buffer — UI-side history,
    /// captured per observed turn alongside the chronicle.</summary>
    public ViewModel.HistoryBuffer History { get; } = new();

    /// <summary>
    /// M5 map workstream P7 — WHAT THE PLAYER KNOWS of <see cref="World"/>, rebuilt after every step (and at the start) from
    /// the Sim.Core knowledge seam: <see cref="PlayerKnowledge.Of(IReadOnlyWorldState)"/>, all-known until S2a stores an
    /// extent. Every player-facing reader asks it; the annals are filtered by it HERE, before a line is appended (an event
    /// is kept only when the player knows every settlement and polity it names), and the history buffer's world series
    /// sum only what it knows. Never a second source of truth: a pure function of the world (and, for a test or the
    /// headless gate's hidden-rival rig only, of the stub <see cref="StartFrom(WorldState, ulong, int?, int?, Func{IReadOnlyWorldState, PlayerKnowledge}?)"/>
    /// was given). The developer export (chronicle.txt) writes the same filtered annals.
    /// </summary>
    public PlayerKnowledge Knowledge { get; private set; }

    // P7: how Knowledge is built from a world — the production view, or a test/gate stub (never anything else).
    private readonly Func<IReadOnlyWorldState, PlayerKnowledge> _knowledgeOf;

    /// <summary>
    /// M5 map workstream P10 — THE 'NOW USABLE' HISTORY: every shortcut a step fired for the player (a content-declared
    /// recipe or activity that the player's research made knowledge-eligible, and whose effect the simulation reads —
    /// <see cref="DiscoveryShortcuts.Transitions"/>), oldest first. Fed by <see cref="Step"/>, which Resume's replay drives
    /// too, so a resumed session rebuilds it identically. UI-side only: it never touches the world and no hash moves.
    /// </summary>
    public NowUsableHistory NowUsableHistory { get; } = new(DiscoveryShortcuts.Content);

    private UiSession(
        WorldState world, TurnExecutor executor, OrderLog orders,
        ulong seed, int? sizePx, int? settlements, SimConfig simCfg, int? aiEmpires = null,
        Func<IReadOnlyWorldState, PlayerKnowledge>? knowledgeOf = null)
    {
        World = world;
        _knowledgeOf = knowledgeOf ?? (w => PlayerKnowledge.Of(w));
        Knowledge = _knowledgeOf(world);
        _executor = executor;
        Orders = orders;
        _seed = seed;
        _sizePx = sizePx;
        _settlements = settlements;
        _aiEmpires = aiEmpires;
        _simCfg = simCfg;
        _grainGoodId = simCfg.Goods?.GrainId ?? 0;
        using (var stream = Sim.Data.DataFiles.OpenChronicle())
        {
            _chronicleCfg = Sim.Core.Chronicle.ChronicleConfigLoader.Load(stream);
        }
        Names = Sim.Core.Chronicle.NameRegistry.Build(_chronicleCfg, world.Seed, world);
        // T4.21-5: the chronicle's famine event is the simulation's own
        // FoodState classification, so the collector needs the sim config.
        _chronicle = new Sim.Core.Chronicle.ChronicleCollector(_chronicleCfg, _simCfg);
        ObserveChronicle(); // founding events fire on first sight
        History.Capture(World, Knowledge); // the founding sample (turn 0)
        CaptureTrace();         // turn 0 — the world before any order lands
    }

    private void ObserveChronicle()
    {
        _chronicle.Observe(World);
        for (; _renderedEvents < _chronicle.Events.Count; _renderedEvents++)
        {
            // P7: the annals are filtered by SUBJECT — an event at a settlement the player does not know is never told.
            // The collector itself still observes every settlement (its tracking state is the same with or without fog).
            if (!AdmitsAnnal(Knowledge, _chronicle.Events[_renderedEvents])) continue;
            _annals.Add(Sim.Core.Chronicle.ChronicleProse.Render(
                _chronicle.Events[_renderedEvents], _chronicleCfg, Names));
        }
    }

    /// <summary>P7: whether a core chronicle event reaches the player's annals — its subject settlement is not one the
    /// player does not know (all-known: every event, as before).</summary>
    public static bool AdmitsAnnal(PlayerKnowledge knowledge, Sim.Core.Chronicle.ChronicleEvent e)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        return !knowledge.IsHiddenSettlement(e.SettlementId);
    }

    /// <summary>Founds the world and builds the PRODUCTION executor + a fresh log.
    /// <paramref name="aiEmpiresOverride"/> (ADR-033 D5) founds that many AI-commanded Empires instead of
    /// worldgen.json's count (default 0); their orders come from <see cref="AiOrders"/> at every End Turn.
    /// <paramref name="wrapXOverride"/> (batch 3 WS3) is <see cref="UiFounding.Found"/>'s test and tool seam for the
    /// world's east-west topology — never set by the launcher, not recorded in the manifest.</summary>
    public static UiSession Start(
        ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null, int? aiEmpiresOverride = null,
        bool? wrapXOverride = null)
    {
        var orders = new OrderLog();
        SimConfig simCfg = UiFounding.ProductionConfig();
        return new UiSession(
            UiFounding.Found(seed, sizeOverridePx, settlementsOverride, aiEmpiresOverride, wrapXOverride),
            BuildProductionExecutor(orders), orders,
            seed, sizeOverridePx, settlementsOverride, simCfg, aiEmpiresOverride);
    }

    /// <summary>The AI-empire override this session was founded with, or null for worldgen.json's count.</summary>
    public int? AiEmpiresOverride => _aiEmpires;

    /// <summary>
    /// A session over an ALREADY-FOUNDED world with the production executor and config — for tests
    /// and previews that need a constructed situation (e.g. an AI polity that is eligible for its next
    /// Age). The order pathway, AI policy and End Turn are exactly <see cref="Start"/>'s. Such a session
    /// replays only from the same starting world, so it is never written as a playable session log.
    /// </summary>
    public static UiSession StartFrom(WorldState founded, ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null)
        => StartFrom(founded, seed, sizeOverridePx, settlementsOverride, knowledgeOf: null);

    /// <summary>
    /// As <see cref="StartFrom(WorldState, ulong, int?, int?)"/>, with the session's player knowledge built by
    /// <paramref name="knowledgeOf"/> — P7's TEST SEAM: a knowledge stub (<see cref="PlayerKnowledge.From"/> over
    /// <see cref="PolityKnowledge.Restricted"/>) that hides part of the world, for the metamorphic leak tests and the
    /// headless gate's hidden-rival rig. Null = the production view. It changes what the UI SHOWS, never the world, the
    /// orders or a hash.
    /// </summary>
    public static UiSession StartFrom(WorldState founded, ulong seed, int? sizeOverridePx, int? settlementsOverride,
        Func<IReadOnlyWorldState, PlayerKnowledge>? knowledgeOf)
    {
        var orders = new OrderLog();
        return new UiSession(founded, BuildProductionExecutor(orders), orders,
            seed, sizeOverridePx, settlementsOverride, UiFounding.ProductionConfig(), knowledgeOf: knowledgeOf);
    }

    /// <summary>
    /// The UI's executor recipe — canonical era + PRODUCTION pipeline preset +
    /// full system catalog. Public so the replay-equivalence test pins it; any
    /// UI-only preset/era drift breaks that test, not a played session.
    /// </summary>
    public static TurnExecutor BuildProductionExecutor(OrderLog orders)
        => new(ProductionEra(), ProductionPipeline(), orders);

    /// <summary>The era table the production executor is built from. Split out
    /// of BuildProductionExecutor so the forensic record can name the dt
    /// schedule the run actually used without a second loading recipe: the
    /// executor and the record read the SAME function.</summary>
    public static EraTable ProductionEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    /// <summary>The production pipeline, IN ORDER. The system execution order is
    /// data loaded at startup and is persisted in no existing artifact, although
    /// every value in every other artifact depends on it — so the forensic run
    /// record reads it from here, the same array the executor is constructed
    /// with.</summary>
    public static SystemRegistration[] ProductionPipeline()
    {
        SimConfig simCfg = UiFounding.ProductionConfig();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        using var wgStream = Sim.Data.DataFiles.OpenWorldgen();
        return PipelineLoader.Load(pipe, SystemCatalog.All(
            simCfg, Sim.Core.Worldgen.WorldgenConfigLoader.Load(wgStream)));
    }

    /// <summary>The worldgen configuration the production world is founded from
    /// — read by the forensic record for the AI-empire count, which is reported
    /// explicitly including when it is zero.</summary>
    public static Sim.Core.Worldgen.WorldgenConfig ProductionWorldgen()
    {
        using var wgStream = Sim.Data.DataFiles.OpenWorldgen();
        return Sim.Core.Worldgen.WorldgenConfigLoader.Load(wgStream);
    }

    /// <summary>
    /// T3.9b / ADR-033 D1: the labour control's submit handler — a BATCH of five SectorAllocation orders
    /// (D-032, through <see cref="SectorOrderFactory"/>), all stamped with the CURRENT turn, for ONE
    /// settlement. Returns true when the batch was appended, so the caller can leave the widget alone on
    /// refusal rather than pretending the order landed.
    ///
    /// GUARDED BY THE SIMULATION'S OWN PREDICATE (ADR-033 D2, "one predicate, two callers"): the batch is
    /// appended only when <see cref="LabourActivities.CanAllocate"/> holds for the player's Empire — the
    /// settlement exists AND the Empire controls it, the rule PathBuildSystem applies when it consumes the
    /// order and order validation applies at replay. Without it a click on an AI-run, stateless or revolted
    /// settlement was logged, ignored live, and made replay / `sim inspect` validation throw. A refused
    /// batch appends NOTHING (a ghost id, an uncontrolled settlement, an all-zero or out-of-range split).
    /// </summary>
    public bool EmitSectorOrders(ReadOnlySpan<int> weights, int settlementId)
    {
        if (!SectorOrderFactory.CanSubmit(weights)) return false;
        var settlement = new SettlementId(settlementId);
        if (!LabourActivities.CanAllocate(World, UiPlayer.Empire, settlement)) return false;
        IReadOnlyList<OrderRecord> batch = SectorOrderFactory.Create(World.Clock.Turn, UiPlayer.Empire, settlement, weights);
        for (int b = 0; b < batch.Count; b++) Orders.Append(batch[b]);
        return true;
    }

    /// <summary>
    /// ADR-033 D3: the build button's handler — ONE EnqueueConstruction order for the player's Empire,
    /// stamped with the CURRENT turn (the next End Turn queues it), built by
    /// <see cref="ConstructionOrderFactory"/> (= <see cref="ConstructionQuery.EnqueueOrder"/>) and appended
    /// only when <see cref="ConstructionQuery.IsProjectAvailable"/> holds — the predicate ConstructionSystem
    /// applies. A project short of materials is legal (it waits at the head of the queue); a settlement the
    /// Empire does not control, an unknown project or a locked one appends NOTHING.
    /// </summary>
    public bool EmitConstructionOrder(int settlementId, int projectId)
    {
        // WS1 (the batch-2 one-predicate gap closed): judged against the queue the intake will see — the world's queue with
        // this turn's pending cancels and enqueues applied (ConstructionQuery.QueueWithPending), so an order the intake would
        // refuse (a second workshop on ten tools) is never logged.
        if (ConstructionOrderFactory.Create(World, Config, UiPlayer.Empire, new SettlementId(settlementId), projectId, QueuedOrders()) is not { } order)
            return false;
        Orders.Append(order);
        return true;
    }

    /// <summary>
    /// WS1 (D-054): the queue's Cancel handler — ONE cancel order for the player's Empire (D-056: kind 13, carrying the
    /// entry's identity), stamped with
    /// the CURRENT turn (the next End Turn removes the entry before its building work), built by
    /// <see cref="ConstructionQuery.CancelOrder"/> (the constructor the AI's structural-stall cancel uses) and appended only
    /// when THE predicate ConstructionSystem applies at delivery holds (<see cref="ConstructionQuery.CancelOrderValid(IReadOnlyWorldState, OrderRecord)"/>:
    /// a roster Empire, control, a well-formed slot, the slot queued) for a COMMITTED entry whose cancel is not already
    /// pending (<see cref="ConstructionQuery.PendingCancels"/>) — an entry ordered this turn has no cancel ("from next turn").
    /// No refund: what has gone in is lost. Appends NOTHING (false) otherwise.
    /// </summary>
    public bool EmitCancelConstruction(int settlementId, int slot)
    {
        var settlement = new SettlementId(settlementId);
        OrderRecord order = ConstructionQuery.CancelOrder(World, UiPlayer.Empire, settlement, slot);
        if (!ConstructionQuery.CancelOrderValid(World, order)) return false;
        int i = ConstructionQuery.IndexOfSlot(World.ConstructionQueue, settlement, slot);
        var entry = new QueueEntryRef(settlement, slot, World.ConstructionQueue[i].ProjectId);
        if (Array.IndexOf(ConstructionQuery.PendingCancels(World, QueuedOrders()), entry) >= 0) return false;
        Orders.Append(order);
        return true;
    }

    /// <summary>
    /// ADR-032 / ADR-033 D2: the road-development action's handler — ONE DevelopRoads order for the player's
    /// Empire ("modernize <paramref name="percent"/>% of eligible demand"), stamped with the CURRENT turn,
    /// built by <see cref="RoadOrderFactory"/> (= <see cref="RoadDevelopmentQuery.DevelopOrder"/>). Appends
    /// NOTHING when the percentage is outside the order log's (0, 100], when the selection RoadDevelopmentSystem
    /// applies is empty (no eligible route for a class the Empire knows), or when the Empire already ordered a
    /// development this turn — the system applies only the FIRST DevelopRoads per Empire per turn, so a second
    /// would sit in the log and change nothing.
    /// </summary>
    public bool EmitRoadOrder(double percent)
    {
        PolityId me = UiPlayer.Empire;
        if (QueuedRoadOrder() is not null) return false;
        if (RoadOrderFactory.Create(World, Config, me, percent) is not { } order) return false;
        Orders.Append(order);
        return true;
    }

    /// <summary>The player's DevelopRoads order queued this turn (the one the next End Turn applies), or null.</summary>
    public OrderRecord? QueuedRoadOrder()
    {
        IReadOnlyList<OrderRecord> queued = QueuedOrders();
        for (int i = 0; i < queued.Count; i++)
            if (queued[i].Kind == OrderKind.DevelopRoads && queued[i].ActorId == UiPlayer.ActorId) return queued[i];
        return null;
    }

    /// <summary>
    /// ADR-029 / D-044 R17: the research-target control's submit handler. It
    /// appends a SetResearchTarget order for the player's Empire, stamped with the
    /// CURRENT turn — the same order pathway as every other directive; the UI never
    /// writes research state. Returns false and appends NOTHING when the node is not
    /// available to the Empire now, or when the session carries no research content,
    /// so the caller can leave the control alone rather than pretend the order landed.
    /// </summary>
    public bool EmitResearchOrder(ResearchNodeId node)
    {
        if (Config.Research is not { } content) return false;
        if (ResearchOrderFactory.SetTarget(World, content, UiPlayer.Empire, node) is not { } order)
            return false;
        Orders.Append(order);
        return true;
    }

    /// <summary>
    /// ADR-033 D4: the tax edict's submit handler — ONE SetTaxRate order, stamped with the CURRENT
    /// turn, legislated by the player's Empire for itself (<see cref="TaxOrderFactory.Create(long, PolityId, int)"/>,
    /// i.e. <see cref="Governance.TaxOrder"/>). Returns false and appends NOTHING for a rate outside
    /// 0..100, or when the Empire cannot levy a tax yet (<see cref="Governance.CanLevyTax"/> — the
    /// tax gate, the Taxation knowledge AND the minimum Age (H2), that GovernanceSystem applies on
    /// the step; the UI never logs an edict the
    /// simulation would ignore), so the caller can leave the control alone rather than pretend.
    /// </summary>
    public bool EmitTaxOrder(int percent)
    {
        if (!TaxOrderFactory.CanSubmit(percent)) return false;
        PolityId me = UiPlayer.Empire;
        if (!Governance.CanLevyTax(World, Config, me)) return false;
        Orders.Append(TaxOrderFactory.Create(World.Clock.Turn, me, percent));
        return true;
    }

    /// <summary>
    /// WS2 (D-054): the Materials block's "Set" handler — ONE SetResourceAllocation order (kind 12) for the player's Empire,
    /// stamped with the CURRENT turn, legislated by the Empire for itself (<see cref="ResourceAllocation.Order"/>: TargetId =
    /// the Empire, Amount = building work's shares in 100). It governs THIS turn's work: the next End Turn applies it before
    /// any building work (ConstructionSystem's step 2). Appended only when THE predicate ConstructionSystem applies at
    /// delivery holds (<see cref="ConstructionQuery.AllocationOrderValid"/>: the split switched on, a roster Empire setting its
    /// own split, a whole share in [0, 100]) and the share differs from the split in force for this turn (the world's row
    /// with this turn's earlier Sets applied — a Set that changes nothing is not logged; the button shows only while the
    /// draft differs). A later Set in the same turn replaces an earlier one (the last valid order wins). Appends NOTHING
    /// (false) otherwise.
    /// </summary>
    public bool EmitAllocationOrder(int percent)
    {
        if (percent is < 0 or > 100) return false;
        PolityId me = UiPlayer.Empire;
        OrderRecord order = ResourceAllocation.Order(World, me, percent);
        if (!ConstructionQuery.AllocationOrderValid(World, Config, order)) return false;
        IReadOnlyList<OrderRecord> queued = QueuedOrders();
        int inForce = ResourceAllocation.EffectivePercent(ConstructionQuery.AllocationsWith(World, Config, World.ResourceAllocations, queued), me);
        if (inForce == percent) return false;
        Orders.Append(order);
        return true;
    }

    /// <summary>
    /// M5 S1d (D-051 §5, ADR-036): the placement mode's CONFIRM handler — ONE FoundSettlement order for the player's
    /// Empire, from settlement <paramref name="sourceId"/> to terrain cell <paramref name="siteCell"/>, stamped with the
    /// CURRENT turn (the next End Turn founds it, in the state of the turn after). Built by
    /// <see cref="FoundSettlementOrderFactory"/> (= <see cref="FoundingQuery.FoundOrder"/>, the constructor the AI's founding
    /// policy uses) and appended only when THE predicate ColonizationSystem applies (<see cref="FoundingQuery.Check(IReadOnlyWorldState, SimConfig, Sim.Core.Worldgen.SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>)
    /// answers Legal on this world with the batch the queued orders already make, on a cell the player knows (P7). Appends
    /// NOTHING (and returns false) otherwise — a cell the player does not know, an illegal site, a source the Empire does
    /// not hold or that cannot spare a party and its provisions, a source whose founding is already queued this turn, or a
    /// world without terrain — so the caller shows why rather than pretend the order landed.
    /// </summary>
    public bool EmitFoundSettlement(int sourceId, int siteCell)
    {
        if (Sites is not { } site) return false;
        if (FoundSettlementOrderFactory.Create(World, Config, site.Frontier, Knowledge.Core, UiPlayer.Empire,
                new SettlementId(sourceId), siteCell, QueuedOrders()) is not { } order) return false;
        Orders.Append(order);
        return true;
    }

    /// <summary>The not-yet-stepped orders: the log rows stamped with the current turn, in log order
    /// (exactly the batch the next End Turn delivers).</summary>
    public IReadOnlyList<OrderRecord> QueuedOrders()
    {
        var queued = new List<OrderRecord>();
        for (int i = 0; i < Orders.Count; i++)
            if (Orders[i].Turn == World.Clock.Turn) queued.Add(Orders[i]);
        return queued;
    }

    /// <summary>The player's AdvanceAge decision queued this turn, or null.</summary>
    public PendingAgeAdvance? PendingPlayerAdvance =>
        Config.Ages is { } ages ? AgeQuery.PendingAdvance(World, ages, QueuedOrders(), UiPlayer.Empire) : null;

    /// <summary>
    /// ADR-031 / D-047 ruling 13: the ADVANCE AGE confirm handler. Appends exactly
    /// <see cref="AgeQuery.AdvanceOrder"/> for the player's Empire (stamped with the CURRENT turn, so
    /// the next turn begins in the new Age) — the same order pathway as every other directive; the UI
    /// never writes Age state. Returns false and appends NOTHING when the session has no Age content,
    /// when <see cref="AgeQuery.CheckAdvance"/> would reject it, or when an advance is already pending
    /// this turn (a second order would change nothing but would still sit in the log).
    /// </summary>
    public bool EmitAdvanceAge(int toAge, int surgeKey)
    {
        if (Config.Ages is not { } ages) return false;
        PolityId me = UiPlayer.Empire;
        if (AgeQuery.CheckAdvance(World, ages, me, toAge, surgeKey) != AdvanceRejection.None) return false;
        if (AgeQuery.PendingAdvance(World, ages, QueuedOrders(), me) is not null) return false;
        Orders.Append(AgeQuery.AdvanceOrder(World, me, toAge, surgeKey));
        return true;
    }

    /// <summary>
    /// ADR-033 D5 (replacing the Age-only call of ADR-031 §4): before each step every AI polity's orders —
    /// research target, Age advance, road development, construction (<see cref="AiOrders"/>, the one
    /// deterministic producer, which reaches them through the player's own order constructors and the same
    /// domain predicates) — are appended to the order log like the player's clicks, so an AI run replays from
    /// the log. Player-commanded polities are never touched: the player is never auto-advanced.
    /// </summary>
    private void AppendAiOrders() => AiOrders.Append(Orders, World, Config, _worldgen);

    /// <summary>
    /// ADR-029: clears the player's research target (progress is kept, D-044 R9) — ONE SetResearchTarget
    /// order with target −1 (<see cref="ResearchQuery.ClearTargetOrder"/>), stamped with the CURRENT turn.
    /// Appends NOTHING (and returns false) when there is nothing to clear: no research content; a clear
    /// already queued this turn; or no target chosen earlier this turn and none in the world (the condition
    /// under which the action surface lists "research.clear-target"). ResearchSystem applies a turn's target
    /// orders in log order, so a clear after a choice made the same turn is a real decision.
    /// </summary>
    public bool ClearResearchTarget()
    {
        if (Config.Research is null) return false;
        int? queued = QueuedResearchChoice();
        if (queued == -1) return false;
        if (queued is null && !ResearchQuery.TryGetTarget(World, UiPlayer.Empire, out _)) return false;
        Orders.Append(ResearchOrderFactory.ClearTarget(World, UiPlayer.Empire));
        return true;
    }

    /// <summary>The research directive the player queued this turn, as ResearchSystem will apply it — the
    /// LAST of the player's SetResearchTarget orders in log order: a node key, −1 for a clear, or null when
    /// none is queued.</summary>
    public int? QueuedResearchChoice()
    {
        int? choice = null;
        IReadOnlyList<OrderRecord> queued = QueuedOrders();
        for (int i = 0; i < queued.Count; i++)
            if (queued[i].Kind == OrderKind.SetResearchTarget && queued[i].ActorId == UiPlayer.ActorId) choice = queued[i].TargetId;
        return choice;
    }

    /// <summary>End Turn: the executor steps synchronously (m1 spec §3);
    /// the chronicle observes the new state (detection is read-only).
    ///
    /// T4.19: the observation log is fed the (prev, next) pair AFTER the step.
    /// It runs before the chronicle here, and the order is immaterial: both are
    /// read-only over the same two worlds and neither reads the other, so
    /// swapping them changes no record and no hash. The orders handed to it
    /// are exactly the batch the executor delivered — the log rows whose Turn
    /// == prev.Clock.Turn (TurnExecutor.Step's BatchFor) — with their log
    /// indices, so a policy change carries its order number.</summary>
    public void EndTurn()
    {
        AppendAiOrders();
        Step();
    }

    /// <summary>The step and every UI-side observer, shared by live play and <see cref="Resume"/>'s replay.</summary>
    private void Step()
    {
        WorldState prev = World;
        World = _executor.Step(prev);
        PreviousWorld = prev;
        // Audit E23: a colony founded this step gets its name now. The registry assigns names in ascending
        // id order with collision re-draws against names already taken, so rebuilding over a world that
        // only GAINED settlements leaves every existing name unchanged (pinned by a test).
        if (World.Settlements.Count != prev.Settlements.Count)
            Names = Sim.Core.Chronicle.NameRegistry.Build(_chronicleCfg, World.Seed, World);
        _observations.Observe(prev, World, _simCfg, OrderApplied.For(Orders, prev.Clock.Turn));
        Knowledge = _knowledgeOf(World);   // P7: what the player knows of the world the step produced
        ObserveChronicle();
        _events.Observe(prev, World, _simCfg, Names.Name, _annals, Knowledge);
        NowUsableHistory.Observe(prev, World, _simCfg, UiPlayer.Empire);   // P10: the 'now usable' shortcuts this step fired
        History.Capture(World, Knowledge);
        CaptureTrace();
    }

    /// <summary>One trace line for the world as it now stands. Called AFTER the
    /// step, like every other observer here.</summary>
    private void CaptureTrace() => _trace.Add(SessionTrace.Line(World, _grainGoodId));

    /// <summary>The chronicle.txt path twinned with a session log path:
    /// same stamp, `chronicle-` prefix, `.txt`.</summary>
    public static string ChroniclePath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "chronicle-") + ".txt");

    /// <summary>The trace path twinned with a session log path: same stamp,
    /// `trace-` prefix, `.csv`.</summary>
    public static string TracePath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "trace-") + ".csv");

    /// <summary>T4.19: the telemetry path twinned with a session log path: same
    /// stamp, `telemetry-` prefix, `.jsonl`.</summary>
    public static string TelemetryPath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "telemetry-") + ".jsonl");

    /// <summary>m4-forensic P1: the forensic record path twinned with a session
    /// log path: same stamp, `forensic-` prefix, `.jsonl`.</summary>
    public static string ForensicPath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "forensic-") + ".jsonl");

    /// <summary>The manifest path twinned with a session log path: same stamp,
    /// `session-` prefix, `.json`.</summary>
    public static string ManifestPath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "session-") + ".json");

    /// <summary>
    /// The reproduction contract for this session: the seed and overrides the
    /// world was built from, and the identity of the build that ran it. Written
    /// ONCE at launch — before a turn is played — because its whole job is to
    /// survive a session that ends in a crash.
    ///
    /// ADR-022 (CR-013 ruling): the manifest also records the PLATFORM that
    /// played the session — the .NET runtime identifier, read HERE because
    /// Sim.Ui may interrogate the runtime (ADR-009) and Sim.Core may not. A
    /// session played off the reference platform (Linux x64) is expected to
    /// diverge from a reference replay at the hash level from turn 2 (CR-013
    /// §8), and `sim inspect` can only say so if the trace's origin is on
    /// record; without it the director's real Windows session read as
    /// "REPRODUCTION FAILED", a determinism finding it was not.
    /// </summary>
    public SessionManifest Manifest(string startedAt, string sessionLogPath, SessionDisplay? display = null) =>
        new(Seed: _seed,
            SizePx: _sizePx,
            Settlements: _settlements,
            SchemaVersion: CanonicalSchema.Version,
            BuildSha: BuildInfo.Sha,
            BuildDate: BuildInfo.Date,
            StartedAt: startedAt,
            OrdersFile: Path.GetFileName(sessionLogPath),
            ChronicleFile: Path.GetFileName(ChroniclePath(sessionLogPath)),
            TraceFile: Path.GetFileName(TracePath(sessionLogPath)),
            TelemetryFile: Path.GetFileName(TelemetryPath(sessionLogPath)),
            Platform: System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            ForensicFile: Path.GetFileName(ForensicPath(sessionLogPath)),
            AiEmpires: _aiEmpires,
            Display: display);

    /// <summary>Writes the manifest beside the order log (with the display the game opens on, when the host knows it).</summary>
    public void ExportManifest(string startedAt, string sessionLogPath, SessionDisplay? display = null)
    {
        string path = ManifestPath(sessionLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream file = File.Create(path);
        Manifest(startedAt, sessionLogPath, display).Write(file);
    }

    /// <summary>Exports the turn trace — header plus one line per observed
    /// turn, fixed \n like the chronicle.</summary>
    public void ExportTrace(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", _trace) + "\n");
    }

    // --- P0: THE TELEMETRY WRITE PATH (m4-forensic, phase P0) ---------------
    // WHAT THIS REPLACED, and why it was a defect. ExportTelemetry used to be
    // File.Create + TelemetryWriter.WriteAll — it truncated the file and
    // re-serialized EVERY accumulated turn on EVERY End Turn. The bytes written
    // over an N-turn session were therefore O(N^2) in the record size, and the
    // last End Turn of a 650-turn session synchronously re-serialized the whole
    // artifact. It also made the window in which the file is INVALID as long as
    // the whole file: File.Create truncates first, so a process death mid-save
    // left a file missing every turn already played, not just the last one.
    //
    // THE FIX IS APPEND, and the shape of telemetry/v2 makes it exact: the file
    // is JSONL with NO header line (the schema tag rides on every line —
    // TelemetryWriter.Schema is written inside WriteTurn), so an append is a
    // legal telemetry file by construction and nothing has to be written once.
    // Each record is written in full and FLUSHED before the next begins, so a
    // completed prior record is always intact on disk; a death mid-record can
    // truncate only the record being written, and every line before it still
    // parses (asserted in Sim.Ui.Tests: TelemetryAppendTests).
    //
    // NO SIMULATION STATE IS TOUCHED. This method reads _observations and
    // writes a file. The two counters below are OBSERVER accounting — the
    // write-amplification instrument the packet was asked to measure.
    private string? _telemetryPath;
    private int _telemetryRecordsWritten;
    private long _telemetryBytesWritten;

    /// <summary>TOTAL BYTES this session has written to the telemetry file over
    /// its whole life — the write-amplification instrument. Under the append
    /// path this converges on the file's own size; under the old rewrite path
    /// it was the sum of every intermediate file size.</summary>
    public long TelemetryBytesWritten => _telemetryBytesWritten;

    /// <summary>How many observation records are already on disk.</summary>
    public int TelemetryRecordsWritten => _telemetryRecordsWritten;

    /// <summary>
    /// T4.19/P0: exports the telemetry INCREMENTALLY — on each call only the
    /// records observed since the last call are appended, in observation order,
    /// each written whole and flushed before the next. The file on disk stays a
    /// complete prefix of the session at all times.
    ///
    /// A path this session has not written before (or a file that has since
    /// disappeared) is written from the start, so the method is still a
    /// complete export for any caller that asks for one.
    /// </summary>
    public void ExportTelemetry(string path) => ExportTelemetry(path, flushToDisk: false);

    /// <summary>
    /// The explicit FINALIZE on exit: appends whatever remains and forces the
    /// bytes past the OS buffers, so the artifact is durable when the process
    /// ends deliberately rather than merely handed to the OS.
    /// </summary>
    public void FinalizeTelemetry(string path) => ExportTelemetry(path, flushToDisk: true);

    private void ExportTelemetry(string path, bool flushToDisk)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Resume only onto the file this session has been appending to. Any
        // other path — or a file that vanished under us — restarts from record
        // zero, because appending a suffix onto a file we did not write would
        // produce an artifact that is not this session.
        bool resume = _telemetryPath is not null
            && string.Equals(_telemetryPath, path, StringComparison.Ordinal)
            && File.Exists(path);
        if (!resume)
        {
            _telemetryPath = path;
            _telemetryRecordsWritten = 0;
        }

        IReadOnlyList<TurnObservation> all = _observations.Observations;
        if (resume && _telemetryRecordsWritten >= all.Count && !flushToDisk) return;

        using var file = new FileStream(
            path,
            resume ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.Read);
        for (int i = _telemetryRecordsWritten; i < all.Count; i++)
        {
            long before = file.Position;
            TelemetryWriter.WriteTurn(file, all[i]);
            file.Flush();                       // the record is whole on disk
            _telemetryRecordsWritten = i + 1;   // ...and only then counted
            _telemetryBytesWritten += file.Position - before;
        }
        if (flushToDisk) file.Flush(flushToDisk: true);
    }

    // --- P1: IDENTITY AND PROVENANCE ---------------------------------------
    // The sixth file. Written by the SAME assembler the headless CLI uses
    // (ForensicSession), so a played run and a headless one cannot disagree
    // about the same world. Everything in it is a READ of what is already in
    // memory at launch, plus content digests; no simulation state is touched,
    // and the run id is derived from content, never from the wall clock.

    /// <summary>The run id of this session's forensic record, once written.</summary>
    public string? ForensicRunId { get; private set; }

    /// <summary>
    /// Builds the run record for this session. Wall clock is the CALLER'S, as on
    /// the manifest: Sim.Ui may read one (ADR-009) and hands it over.
    /// </summary>
    public Sim.Core.Observability.Forensic.ForensicRunRecord ForensicRun(string startedAt) =>
        Sim.Core.Observability.Forensic.ForensicSession.BuildRun(
            seed: _seed,
            sizePx: _sizePx,
            settlements: _settlements,
            founded: true,
            contentAssembly: typeof(Sim.Data.DataFiles).Assembly,
            orders: Orders,
            era: ProductionEra(),
            pipeline: ProductionPipeline(),
            aiEmpiresConfigured: _aiEmpires ?? ProductionWorldgen().AiEmpires,
            terrainContentHash: World.Terrain?.ContentHash,
            buildSha: BuildInfo.Sha,
            buildDate: BuildInfo.Date,
            platform: System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            startedAt: startedAt);

    /// <summary>Writes the forensic run record beside the order log, ONCE at
    /// launch — for the manifest's reason: a session that ends in a crash is
    /// still identified, because the identity is written before a turn is
    /// played.</summary>
    public void ExportForensicRun(string startedAt, string sessionLogPath)
    {
        ForensicRunId = Sim.Core.Observability.Forensic.ForensicSession.WriteRun(
            ForensicPath(sessionLogPath), ForensicRun(startedAt));
    }

    /// <summary>
    /// APPENDS the close record: how far the session got, the hash it itself
    /// computed for its final world, and the CONTENT hash of every companion
    /// artifact — so the set is bound by content and not by a shared filename
    /// stamp. A swapped same-named companion becomes a named finding instead of
    /// a reproduction failure that is not one.
    /// </summary>
    public void ExportForensicClose(string sessionLogPath)
    {
        if (ForensicRunId is not { } runId) return;   // no run record: nothing to close
        string dir = Path.GetDirectoryName(sessionLogPath) ?? "";
        // The final world hash is the one the TRACE already carries for this
        // turn — read from the line this session wrote, never recomputed here,
        // so the record cannot disagree with the trace about the same world.
        string[] last = _trace[^1].Split(',');
        Sim.Core.Observability.Forensic.ForensicSession.WriteClose(
            ForensicPath(sessionLogPath),
            new Sim.Core.Observability.Forensic.ForensicCloseRecord(
                RunId: runId,
                TurnsReached: World.Clock.Turn,
                FinalWorldHash: last[^1],
                FinalWorldHashState: "recorded — READ from this session's own trace line, not recomputed",
                Artifacts: Sim.Core.Observability.Forensic.ForensicSession.Companions(
                    dir,
                    ("manifest", Path.GetFileName(ManifestPath(sessionLogPath))),
                    ("orders", Path.GetFileName(sessionLogPath)),
                    ("trace", Path.GetFileName(TracePath(sessionLogPath))),
                    ("chronicle", Path.GetFileName(ChroniclePath(sessionLogPath))),
                    ("telemetry", Path.GetFileName(TelemetryPath(sessionLogPath)))),
                GatesState: "not-recorded — no gate-verdict producer exists in this build"));
    }

    /// <summary>Exports the annals — EXACTLY the panel's lines, one per line,
    /// fixed \n (byte-identical across identical runs).</summary>
    public void ExportChronicle(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", _annals) + "\n");
    }

    /// <summary>
    /// Stamped session-log path (director UX ruling, T1.9): flat filename,
    /// lexicographic = chronological. A non-canonical size is recorded IN the
    /// name (…-s256.bin) so a preview session is never mistaken for a
    /// canonical-world log at replay time (`sim replay --founded --size PX`).
    /// </summary>
    public static string SessionLogPath(
        DateTime now, int? sizeOverridePx = null, int? settlementsOverride = null, int? aiEmpiresOverride = null) =>
        Path.Combine("runs",
            "orders-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + (sizeOverridePx is { } sz
                ? "-s" + sz.ToString(CultureInfo.InvariantCulture)
                : "")
            + (settlementsOverride is { } n
                ? "-n" + n.ToString(CultureInfo.InvariantCulture)
                : "")
            // ADR-033 D5: an AI-empire override is recorded IN the name too (…-a1.bin), like the size and
            // settlement overrides, so such a log is never mistaken for a solo canonical-world log.
            + (aiEmpiresOverride is { } a
                ? "-a" + a.ToString(CultureInfo.InvariantCulture)
                : "")
            + ".bin");

    /// <summary>
    /// Audit E32 — RESUME by replay (D-008: replay is the recovery path). Re-founds the world from the session
    /// manifest (seed, size, settlements, AI empires), then replays the saved order log through the SAME
    /// executor and the SAME per-step observers live play uses — so the world AND the UI-side records (annals,
    /// observations, history, trace, names, policy history) are rebuilt, not approximated. The log already
    /// carries every AI order (they were appended to it live), so the AI producer is NOT re-run while
    /// replaying. Played turns: the trace's last turn when the trace exists (each replayed turn's hash is then
    /// checked against it, and a mismatch throws), else the highest order stamp. Orders stamped with the
    /// final turn were issued but not yet played; they are re-queued.
    ///
    /// S0 (2026-10-10) — A SESSION RECORDED UNDER ANOTHER SCHEMA. The trace's hashes cover the whole canonical
    /// stream (WorldHash), so after any schema bump no saved trace line can equal a replayed one, and the check
    /// above threw "resume diverged at turn 1" for every older session while the manifest's own
    /// <see cref="SessionManifest.SchemaVersion"/> went unread. It is read now: when it is not this build's
    /// <see cref="CanonicalSchema.Version"/>, the session is resumed by the D-008 recovery path — re-founded from
    /// the seed and replayed from the order log WITHOUT the trace-hash check (the trace still gives the turn to
    /// replay to) — and <see cref="ResumeNotice"/> says so explicitly. The ADR-034 order checks still apply.
    ///
    /// D-054 (fix round 1, 2026-10-15) — A SESSION RECORDED BEFORE THE CYLINDER. Every world recorded under a schema before
    /// v33 was flat; such a session is re-founded flat (<see cref="TopologyFlags.RecordedFlat"/>), so its replay runs on the
    /// map it was played on, and the notice says so. A session this build recorded is re-founded with the shipped worldgen,
    /// as it was founded.
    ///
    /// M5 playtest blocker (D-052) — A SESSION ANOTHER BUILD RECORDED UNDER THIS SCHEMA. The simulation can change
    /// between builds without a schema bump (playtest build 2dc6af2 and f0427d2 are both v32 and differ from turn 1),
    /// and then the trace check refuses the session — rightly: its world cannot be rebuilt here. That refusal used to
    /// end the launch as an unhandled exception. It is now a <see cref="SessionIncompatibleException"/> carrying the
    /// recorded and current build identities and the divergence turn, with the check's
    /// <see cref="ResumeDivergedException"/> as its inner exception; <see cref="ResumeLaunch"/> reports it to the
    /// player. The check itself, the schema-mismatch path above and every hash are unchanged, and nothing is written:
    /// a refused resume has only read the session's files.
    /// </summary>
    public static UiSession Resume(string manifestPath, out string sessionLogPath)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? "";
        SessionManifest manifest;
        using (FileStream m = File.OpenRead(manifestPath)) manifest = SessionManifest.Read(m, manifestPath);
        sessionLogPath = Path.Combine(dir, manifest.OrdersFile);
        OrderLog saved;
        using (FileStream o = File.OpenRead(sessionLogPath)) saved = OrderLog.Load(o);

        IReadOnlyList<SessionTrace.Row>? trace = null;
        string tracePath = Path.Combine(dir, manifest.TraceFile);
        if (manifest.TraceFile.Length > 0 && File.Exists(tracePath))
            trace = SessionTrace.Parse(File.ReadAllLines(tracePath), tracePath);
        long target = 0;
        if (trace is { Count: > 0 }) target = trace[^1].Turn;
        else for (int i = 0; i < saved.Count; i++) target = Math.Max(target, saved[i].Turn);

        bool sameSchema = manifest.SchemaVersion == CanonicalSchema.Version;
        string recordedBy = "build " + manifest.BuildSha + ", " + manifest.BuildDate + ", schema v"
            + manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture);
        // Fix round 1 (D-054, verifier F1, 2026-10-15): a session recorded before v33 was played on a FLAT map (no world
        // wrapped before the cylinder), so it is re-founded flat — TopologyFlags.RecordedFlat, the rule the CLI's save
        // loader and `sim inspect` apply too. Before, it was re-founded on the shipped cylinder: a different map, and a
        // log whose FoundSettlement sites are legal only on the flat one was rejected (exit 2).
        UiSession session = Start(manifest.Seed, manifest.SizePx, manifest.Settlements, manifest.AiEmpires,
            wrapXOverride: TopologyFlags.RecordedFlat(manifest.SchemaVersion) ? false : null);
        try
        {
            // D-056: the trace goes in whatever the schema — ReplayGuard checks every batch carrying a legacy kind-11
            // cancel against it in any readable form; the every-turn check stays this schema's only.
            session.ReplayTo(saved, target, trace, checkEveryTurn: sameSchema,
                describedAs: "saved session " + Path.GetFileName(manifestPath), recordedBy: recordedBy);
        }
        catch (ResumeDivergedException diverged)   // only with the trace checked, i.e. under this build's schema
        {
            throw new SessionIncompatibleException(
                Path.GetFullPath(manifestPath),
                manifest.BuildSha, manifest.BuildDate, manifest.Platform,
                BuildInfo.Sha, BuildInfo.Date, System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                diverged);
        }
        if (!sameSchema) session.ResumeNotice = SchemaMismatchNotice(manifest.SchemaVersion, CanonicalSchema.Version);
        else if (trace is null) session.ResumeNotice = MissingTraceNotice;
        // D-056: a replay the trace did not check turn by turn may not be the recorded world, and the first save
        // rewrites the log and the trace — so the recorded files are copied aside before it (KeepRecordedFiles).
        if (!sameSchema || trace is null)
            session._recordedFiles = [sessionLogPath, tracePath, Path.Combine(dir, manifest.ChronicleFile)];
        return session;
    }

    /// <summary>D-056: the files a resumed session was recorded with, copied aside (<see cref="RecordedCopyPath"/>) by the
    /// first <see cref="Save"/> before anything overwrites them — set only when the replay was not checked turn by turn
    /// (another schema, or no trace), so the recording survives as evidence; null otherwise or once copied.</summary>
    private string[]? _recordedFiles;

    /// <summary>D-056: where <see cref="Save"/> keeps a recorded file before the first overwrite after an unchecked
    /// resume — beside it, with ".as-recorded" before the extension (orders-X.bin → orders-X.as-recorded.bin).</summary>
    public static string RecordedCopyPath(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? "",
            Path.GetFileNameWithoutExtension(path) + ".as-recorded" + Path.GetExtension(path));

    /// <summary>D-056: the copies, once — an existing copy (an earlier resume's) is never replaced, so it stays the
    /// oldest recording. Missing files (no trace, no chronicle) are skipped.</summary>
    private void KeepRecordedFiles()
    {
        if (_recordedFiles is null) return;
        foreach (string file in _recordedFiles)
        {
            if (file.Length == 0 || !File.Exists(file)) continue;
            string copy = RecordedCopyPath(file);
            if (!File.Exists(copy)) File.Copy(file, copy);
        }
        _recordedFiles = null;
    }

    /// <summary>D-056: the notice for a session of this schema resumed without its trace file — nothing could check the
    /// replay.</summary>
    public static readonly IReadOnlyList<string> MissingTraceNotice =
    [
        "The saved trace file is missing.",
        "Resumed by replaying its seed and orders (D-008).",
        "The replay was not checked against the original game.",
        "The world may differ from the one originally played.",
    ];

    /// <summary>What <see cref="Resume"/> has to tell the player about how this session was rebuilt: empty for a
    /// session started here or resumed with its trace checked, the <see cref="SchemaMismatchNotice"/> lines when it
    /// was recorded under another schema. UI-side, like the annals — never in the world, never in a hash.</summary>
    public IReadOnlyList<string> ResumeNotice { get; private set; } = [];

    /// <summary>The kicker the game's notice toast shows <see cref="ResumeNotice"/> under.</summary>
    public const string ResumeNoticeKicker = "RESUMED SESSION";

    /// <summary>The explicit notice for a session whose manifest records schema <paramref name="recorded"/> while
    /// this build writes <paramref name="current"/> (D-008: saves may break between milestones; seed + order-log
    /// replay is the recovery path). Four short lines, so the notice toast shows each of them whole. Fix round 1
    /// (D-054, verifier F1): for a schema before v33 (<see cref="TopologyFlags.RecordedFlat"/>) the second line names the
    /// map it was rebuilt on — flat, as it was played.</summary>
    public static IReadOnlyList<string> SchemaMismatchNotice(int recorded, int current) =>
    [
        "Recorded under save schema v" + recorded.ToString(CultureInfo.InvariantCulture)
            + "; this build is v" + current.ToString(CultureInfo.InvariantCulture) + ".",
        TopologyFlags.RecordedFlat(recorded)
            ? "Replayed from its seed and orders on the flat map v" + recorded.ToString(CultureInfo.InvariantCulture) + " used (D-008)."
            : "Resumed by replaying its seed and orders (D-008).",
        "The saved trace was not checked against this replay.",
        "The world may differ from the one originally played.",
    ];

    /// <summary>The replay loop of <see cref="Resume"/>, public for the resume tests: appends the saved
    /// orders of each turn in log order and steps, until <paramref name="target"/>; then re-queues the
    /// orders stamped <paramref name="target"/>.
    ///
    /// ADR-034 §4 adopted (S0, 2026-10-10): the replay runs the SAME order checks every CLI replay runner runs —
    /// <see cref="OrderValidation.ValidateAgainstWorld"/> once against the founded world, then
    /// <see cref="OrderValidation.ValidateAtDelivery"/> for each turn's batch against the world it is delivered
    /// to, before that step — so a forged or stale order for a deferred id (an actor no revolt founded, a
    /// settlement no colony took) rejects the resume with the diagnostic `sim replay` prints instead of being
    /// silently ignored by its consumer. The re-queued batch is checked too, against the world the next End Turn
    /// delivers it to. Observer-only: the steps are unchanged and no hash moves. Valid only from the founded world
    /// with an empty log — the turn-0 world the up-front pass needs — which is how <see cref="Resume"/> calls it.
    ///
    /// D-056: before each batch — the re-queued one too — <see cref="ReplayGuard.CheckBeforeDelivery"/> refuses one that
    /// carries a legacy kind-11 cancel unless <paramref name="trace"/> proves the world it is delivered to is the
    /// recorded one (in any readable schema form); with no trace, such a batch is refused. The check runs before the
    /// batch is appended, so a refusal leaves the world as the previous step made it. <paramref name="checkEveryTurn"/>
    /// keeps the every-turn hash check (this schema's sessions only — <see cref="Resume"/> passes false for another
    /// schema's, whose trace hashes another stream).
    /// </summary>
    public void ReplayTo(OrderLog saved, long target, IReadOnlyList<SessionTrace.Row>? trace = null,
        bool checkEveryTurn = true, string describedAs = "this saved session", string? recordedBy = null)
    {
        ArgumentNullException.ThrowIfNull(saved);
        if (World.Clock.Turn != 0 || Orders.Count != 0)
            throw new InvalidOperationException(
                "ReplayTo replays a saved log from the founded world, but this session is at turn "
                + World.Clock.Turn.ToString(CultureInfo.InvariantCulture) + " with "
                + Orders.Count.ToString(CultureInfo.InvariantCulture) + " order(s) already logged.");
        WorldState turnZero = World;
        OrderValidation.ValidateAgainstWorld(saved, turnZero);
        int next = 0;
        while (World.Clock.Turn < target)
        {
            long turn = World.Clock.Turn;
            ReplayGuard.CheckBeforeDelivery(saved.BatchFor(turn), World, trace, describedAs, recordedBy); // D-056
            for (; next < saved.Count && saved[next].Turn <= turn; next++)
                if (saved[next].Turn == turn) Orders.Append(saved[next]);
            OrderValidation.ValidateAtDelivery(Orders.BatchFor(turn), turnZero, World); // ADR-034
            Step();
            if (checkEveryTurn && trace is not null)
                for (int r = 0; r < trace.Count; r++)
                    if (trace[r].Turn == World.Clock.Turn)
                    {
                        string hash = _trace[^1].Split(',')[^1];
                        // M5 playtest blocker (D-052): a typed divergence, same message — Resume turns it into
                        // the player-facing SessionIncompatibleException.
                        if (!string.Equals(hash, trace[r].Hash, StringComparison.Ordinal))
                            throw new ResumeDivergedException(World.Clock.Turn, hash, trace[r].Hash);
                        break;
                    }
        }
        ReplayGuard.CheckBeforeDelivery(saved.BatchFor(World.Clock.Turn), World, trace, describedAs, recordedBy); // D-056
        for (; next < saved.Count; next++)
            if (saved[next].Turn == World.Clock.Turn) Orders.Append(saved[next]);
        // The re-queued batch is delivered to exactly this world by the next End Turn (orders the player adds in
        // the meantime do not change it), so it is checked here, while the log is still the saved one.
        OrderValidation.ValidateAtDelivery(Orders.BatchFor(World.Clock.Turn), turnZero, World); // ADR-034
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        KeepRecordedFiles();   // D-056: before the first overwrite after an unchecked resume
        using var stream = File.Create(path);
        Orders.Save(stream);
    }
}
