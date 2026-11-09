using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Caps;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.War;
using Proto.M7.War.Outcomes;

namespace Proto.M7.Sandbox;

/// <summary>
/// [D] One War Sandbox session (DIR5 §2.13, §2.14, §2.15; D-079 DR30, DR31, DR33, §4 D15; docs/m7-plan/war-sandbox-proposal.md).
///
/// ISOLATION AND ACCOUNTING (D15, PROPOSAL). The session owns a sandbox-scoped <see cref="WarLedger"/>: forces are MINTED by a
/// transfer from the sandbox's own mint account, and a finished battle moves people, through lane W's collapse (the campaign's own
/// casualty mapping), into the sandbox's own sinks (dead, wounded, captured, deserted). Every person stays in exactly one account
/// of THIS ledger, so the sandbox totals conserve exactly, and no transfer can reach a campaign account: the session holds no
/// reference to any campaign world (a Campaign Encounter copies a value snapshot at load).
///
/// FORCES (DR30 "unrestricted experimental force creation"; DR33 any designed family regardless of campaign era). Any designed
/// family of <see cref="SandboxCatalogue"/> may be added in any number, whatever its Age; campaign availability by computed Age
/// (<see cref="CampaignAvailability"/>, READING R2) is never consulted here. CAPS (READING S1, alternative (a) applied; both
/// alternatives in the proposal §4): a force larger than one formation's DR24 cap is created as the fewest formations within the
/// cap (<see cref="FormationRoster.SplitSizes"/>), because the same battle engine the campaign uses refuses an over-cap formation.
///
/// TERRAIN. Editable cell by cell in every mode except a Campaign Encounter, which inherits the campaign terrain (TerrainLocked).
/// DEPLOYMENT (DR31, READING R5). Always through lane TR's <see cref="DeploymentPhase"/>: each army in its own third, the middle
/// third undeployable; the battle cannot start until the phase closes on a valid deployment (NotDeployed).
/// RESOLVER (DR30). An AI-vs-AI battle takes lane P's default policy (<see cref="ResolverPolicy.Default"/>): the AutoResolver
/// (lane P's model 2 as it stands; its acceptance is OPEN, D-079 Q4) whenever every formation is inside its model, headless; the
/// TacticalResolver otherwise (headless too; no rendering is needed). A battle with a player side uses the TacticalResolver.
/// [D, verification round 1, D-R1-12] The fallback is not in DR30 ("AI-vs-AI sandbox battles use AutoResolver"): it is READING A,
/// applied, with the alternative (refuse such battles) in docs/m7-plan/war-sandbox-proposal.md §6.
/// The resolvers are used exactly as the campaign uses them (DR30 "Campaign resolver rules remain unchanged").
/// REPLAY AND INSPECTION. Every edit is logged (refused ones too) and the battle keeps its order log; <see cref="Replay"/> rebuilds
/// the session from the logs and reproduces it bit for bit. The inspection surface hands out copies (terrain, formations, deployed
/// setup since verification round 1; ledger, deployment phase and result since round 2, D-R2-06), so only logged edits reach the
/// session; the logs are handed out as IReadOnlyList views of the session's own lists (no write member; not copied), and the
/// result's personnel report is shared (neither the session's state nor its hash uses it).
/// </summary>
public sealed class SandboxSession
{
    /// <summary>Opening balance of the sandbox mint (people). Sandbox scope only; never a campaign quantity.</summary>
    public const long MintOpening = 1_000_000_000_000;
    /// <summary>Lateral spacing (cells) between the formations one AddForce creates, so they do not start on top of each other.</summary>
    public const long SplitSpacingCells = 4;

    private sealed class Form
    {
        public FormationSpec Spec = null!;
        public DesignedFamily Family = null!;
        public long Persons;
        public int Account;
    }

    private readonly SandboxMode _mode;
    private readonly TerrainGrid _initial;
    private readonly TerrainGrid _grid;
    private readonly ulong _seed;
    private readonly TacticalRules _rules;
    private readonly CampaignEncounterSnapshot? _encounter;
    private readonly SideSpec[] _sides = { new(ExitEdge.West), new(ExitEdge.East) };
    private readonly TerrainCostTable _costs = TerrainCostTable.PlaceholderTestTable();
    private readonly ConcealmentTable _concealment = ConcealmentTable.PlaceholderTestTable();
    private readonly WarLedger _ledger = new();
    private readonly int _mint, _dead, _wounded, _captured, _deserted;
    private readonly List<Form> _forms = new();
    private readonly List<SandboxEditRow> _log = new();
    private readonly List<SandboxCasualtyRow> _casualties = new();
    private long _nextId = 1;
    private DeploymentPhase? _deploy;
    private BattleSetup? _deployBase;     // [D, verification round 2, D-R2-06] the setup the phase was opened on (for its copies)
    private int _deployClosedAt = -1;     // the phase's log length when the session closed it (-1: not closed)
    private BattleResult? _result;
    private BattleSetup? _deployed;
    private TacticalBattle? _tactical;
    private IReadOnlyList<OrderLogEntry> _battleOrders = Array.Empty<OrderLogEntry>();

    public SandboxSession(SandboxMode mode, TerrainGrid terrain, ulong seed, TacticalRules? rules = null)
        : this(mode, terrain, seed, rules, null)
    {
        if (mode == SandboxMode.CampaignEncounter) throw new ArgumentException("a campaign encounter is loaded from a snapshot (FromEncounter)");
    }

    private SandboxSession(SandboxMode mode, TerrainGrid terrain, ulong seed, TacticalRules? rules, CampaignEncounterSnapshot? encounter)
    {
        _mode = mode;
        _initial = Copy(terrain);
        _grid = Copy(terrain);
        _seed = seed;
        _rules = rules ?? new TacticalRules();
        _rules.Validate();
        _encounter = encounter;
        _mint = _ledger.Open(Commodity.People, MintOpening);
        _dead = _ledger.Open(Commodity.People, 0);
        _wounded = _ledger.Open(Commodity.People, 0);
        _captured = _ledger.Open(Commodity.People, 0);
        _deserted = _ledger.Open(Commodity.People, 0);
    }

    /// <summary>Loads a Campaign Encounter: the campaign terrain (inherited, locked) and the campaign formations, each fielded as one
    /// battle formation of min(persons, cap) entities standing for its persons (lane W's persons-and-entities rule). [D, verification
    /// round 1, D-R1-03] This is READING C1 (b), battle entities; the sandbox's own force creation applies C1 (a), persons
    /// (docs/m7-plan/caps-and-counted-objects-proposal.md §2.2).</summary>
    public static SandboxSession FromEncounter(CampaignEncounterSnapshot snapshot, ulong seed, TacticalRules? rules = null)
    {
        var s = new SandboxSession(SandboxMode.CampaignEncounter, snapshot.Terrain, seed, rules, snapshot);
        foreach (var f in snapshot.Forces) s.AddEncounterForce(f.Side, f.FamilyKey, f.IdentityKey, f.Persons, f.Anchor);
        return s;
    }

    // ------------------------------------------------------------------ inspection surface
    public SandboxMode Mode => _mode;
    /// <summary>A COPY of the session's current terrain. [D, verification round 1, D-R1-10] This returned the live grid, so a caller
    /// could write the "locked" terrain of a Campaign Encounter (or any session's terrain) without a log entry, and a replay then
    /// built a different field (verifier probe P3). The session's grid now changes only through <see cref="EditTerrain"/>.</summary>
    public TerrainGrid Terrain => Copy(_grid);
    /// <summary>A COPY of the session's ledger: the same accounts (index, commodity, balance) in a new ledger, so a transfer on it
    /// moves nobody in the session. [D, verification round 2, D-R2-06] This returned the live ledger, and a transfer through it
    /// (outside the session log) changed the forces that fought, which the replay could not reproduce.</summary>
    public WarLedger Ledger => CopyOf(_ledger);
    public IReadOnlyList<SandboxEditRow> Log => _log;
    public IReadOnlyList<SandboxCasualtyRow> Casualties => _casualties;
    public IReadOnlyList<OrderLogEntry> BattleOrders => _battleOrders;
    /// <summary>A COPY of the battle's result (its formation array copied; the formation results are immutable records).
    /// [D, verification round 2, D-R2-06] This returned the live result, whose array a caller could write, changing the session's
    /// hash outside the log.</summary>
    public BattleResult? Result => _result is null ? null : _result with { Formations = (FormationResult[])_result.Formations.Clone() };
    /// <summary>A COPY of the deployed battle setup, with its own grid and arrays. [D, verification round 1, D-R1-10] This handed
    /// out the setup the battle runs on, whose grid was the session's grid.</summary>
    public BattleSetup? DeployedSetup => _deployed is null ? null : Detached(_deployed);
    public ResolverKind? ResolverUsed { get; private set; }
    /// <summary>A COPY of the session's deployment phase, rebuilt edit for edit (the same base setup, detached; every logged proposal
    /// re-proposed in order, refused ones included; closed where the session closed it), so it shows the session's proposal, log and
    /// verdicts, and a proposal tried on it gets lane TR's real verdict without reaching the session. [D, verification round 2,
    /// D-R2-06] This returned the live phase: an unlogged Propose was accepted, the battle was fought from it, and the replay built a
    /// different battle (verifier probe P_D).</summary>
    public DeploymentPhase? Deployment => CopyOfDeployment();
    public int MintAccount => _mint;
    public long Persons(long formationId) { foreach (var f in _forms) if (f.Spec.Id == formationId) return _ledger.Balance(f.Account); return 0; }
    /// <summary>The formations (copies whose arrays are copies too: writing to them changes nothing in the session).</summary>
    public IReadOnlyList<FormationSpec> Formations { get { var a = new FormationSpec[_forms.Count]; for (int i = 0; i < a.Length; i++) a[i] = Detached(_forms[i].Spec); return a; } }
    public long DeadTotal => _ledger.Balance(_dead);
    public long WoundedTotal => _ledger.Balance(_wounded);
    public long CapturedTotal => _ledger.Balance(_captured);
    public long DesertedTotal => _ledger.Balance(_deserted);

    private SandboxRefusal Record(SandboxEditKind kind, int side, int family, int identity, long formation, long a, long b, long c, SandboxRefusal r)
    {
        _log.Add(new SandboxEditRow(_log.Count, (byte)kind, side, family, identity, formation, a, b, c, (byte)r));
        return r;
    }

    private bool Started => _result is not null;

    // ------------------------------------------------------------------ scenario editing

    /// <summary>Sets one cell's terrain class and height. Refused in a Campaign Encounter (inherited terrain), once the deployment
    /// phase is open, or after the battle.</summary>
    public SandboxRefusal EditTerrain(long cx, long cy, TerrainClass cls, long heightMm = 0)
    {
        SandboxRefusal r = SandboxRefusal.None;
        if (_mode == SandboxMode.CampaignEncounter) r = SandboxRefusal.TerrainLocked;
        else if (Started) r = SandboxRefusal.BattleStarted;
        else if (_deploy is not null) r = SandboxRefusal.DeploymentOpen;
        else if (!_grid.InBounds(cx, cy)) r = SandboxRefusal.OutOfGrid;
        if (r == SandboxRefusal.None) _grid.Set(cx, cy, cls, heightMm);
        return Record(SandboxEditKind.TerrainEdit, -1, (int)cls, 0, -1, cx, cy, heightMm, r);
    }

    /// <summary>Adds <paramref name="persons"/> of a designed family to a side at an anchor, as the fewest formations within the DR24
    /// cap (READING S1 (a)); the persons are minted from the sandbox mint. Returns the ids of the formations created.</summary>
    public SandboxRefusal AddForce(int side, int familyKey, int identityKey, long persons, Vec2L anchor, out long[] formationIds)
    {
        formationIds = Array.Empty<long>();
        var fam = SandboxCatalogue.Find(familyKey, identityKey);
        SandboxRefusal r = SandboxRefusal.None;
        if (Started) r = SandboxRefusal.BattleStarted;
        else if (_deploy is not null) r = SandboxRefusal.DeploymentOpen;
        else if (side != 0 && side != 1) r = SandboxRefusal.BadSide;
        else if (persons <= 0) r = SandboxRefusal.NonPositive;
        else if (fam is null) r = SandboxRefusal.UnknownFamily;
        else if (!anchor.InDocumentedRange || !_grid.InBounds(_grid.CellOf(anchor).X, _grid.CellOf(anchor).Y)) r = SandboxRefusal.OutOfGrid;
        if (r == SandboxRefusal.None)
        {
            long max = DirectorCaps.Values.MaxPersons(fam!.CapClass);
            long[] sizes = fam.CapClass == CapFamily.Uncapped ? new[] { persons } : FormationRoster.SplitSizes(persons, max);
            // [D, verification round 1, D-R1-14] every part's anchor is laid out and checked BEFORE anything is minted: the whole
            // force is refused when a part would leave the field (before, only the first anchor was checked, and a 1,000-man
            // force put 16 of its 20 formations off a 48 x 32 field; verifier probe P6).
            var at = SplitAnchors(side, anchor, sizes.Length);
            if (at is null) r = SandboxRefusal.OutOfGrid;
            else
            {
                formationIds = new long[sizes.Length];
                for (int k = 0; k < sizes.Length; k++) formationIds[k] = NewFormation(side, fam, sizes[k], sizes[k], at[k]);
            }
        }
        return Record(SandboxEditKind.AddForce, side, familyKey, identityKey, formationIds.Length > 0 ? formationIds[0] : -1, persons, anchor.X, anchor.Y, r);
    }

    /// <summary>
    /// [D, verification round 1, D-R1-14] The anchors of the parts of one <see cref="AddForce"/>, laid out column by column from the
    /// anchor, <see cref="SplitSpacingCells"/> cells apart. A column holds max(1, floor(grid height / spacing)) parts, at rows
    /// anchor.y + j * spacing wrapped inside the grid (modulo its height); each further column lies spacing cells toward the side's
    /// own exit edge (side 0 West, side 1 East: away from the middle third). Null when a part would leave the grid. The first
    /// column keeps the pre-round-1 positions wherever those were inside the grid. Pure integer arithmetic.
    /// </summary>
    private Vec2L[]? SplitAnchors(int side, Vec2L anchor, int parts)
    {
        long step = checked(SplitSpacingCells * _grid.CellSizeMm), height = checked((long)_grid.Height * _grid.CellSizeMm);
        long perColumn = Math.Max(1, _grid.Height / SplitSpacingCells);
        long dir = _sides[side].Exit == ExitEdge.West ? -1 : 1;
        var at = new Vec2L[parts];
        for (int k = 0; k < parts; k++)
        {
            long column = k / perColumn, row = k % perColumn;
            long y = checked(anchor.Y + row * step);
            if (y >= height) y -= height;
            var p = new Vec2L(checked(anchor.X + dir * column * step), y);
            if (!p.InDocumentedRange) return null;
            var c = _grid.CellOf(p);
            if (!_grid.InBounds(c.X, c.Y)) return null;
            at[k] = p;
        }
        return at;
    }

    private void AddEncounterForce(int side, int familyKey, int identityKey, long persons, Vec2L anchor)
    {
        var fam = SandboxCatalogue.Find(familyKey, identityKey) ?? throw new ArgumentException("the encounter names a family the sandbox cannot field");
        if (persons <= 0 || (side != 0 && side != 1)) throw new ArgumentOutOfRangeException(nameof(persons));
        long cap = DirectorCaps.CapOf(fam.CapClass);
        long entities = fam.CapClass == CapFamily.Uncapped ? persons : Math.Min(persons, cap);
        long id = NewFormation(side, fam, persons, entities, anchor);
        Record(SandboxEditKind.AddForce, side, familyKey, identityKey, id, persons, anchor.X, anchor.Y, SandboxRefusal.None);
    }

    private long NewFormation(int side, DesignedFamily fam, long persons, long entities, Vec2L anchor)
    {
        long id = _nextId++;
        int acc = _ledger.Open(Commodity.People, 0);
        _ledger.Transfer(_mint, acc, persons);
        // [D, verification round 1, D-R1-09] the family's domain weapons (a gun crew's gun), copied into the formation
        _forms.Add(new Form { Spec = new FormationSpec(id, side, fam.Profile, checked((int)entities), anchor) { Weapons = WeaponsOf(fam) }, Family = fam,
                              Persons = persons, Account = acc });
        return id;
    }

    private static WeaponSpec[]? WeaponsOf(DesignedFamily fam)
    {
        if (fam.Weapons is null) return null;
        var w = new WeaponSpec[fam.Weapons.Count];
        for (int i = 0; i < w.Length; i++) w[i] = fam.Weapons[i];
        return w;
    }

    /// <summary>A formation spec whose arrays are copies (records are immutable; their arrays are not).</summary>
    private static FormationSpec Detached(FormationSpec f)
        => f.Weapons is null && f.Entities is null ? f
            : f with { Weapons = (WeaponSpec[]?)f.Weapons?.Clone(), Entities = (EntitySpec[]?)f.Entities?.Clone() };

    /// <summary>[D, verification round 1, D-R1-10] A battle setup with its own grid, formations and sides (nothing shared).</summary>
    private static BattleSetup Detached(BattleSetup s)
    {
        var forms = new FormationSpec[s.Formations.Length];
        for (int i = 0; i < forms.Length; i++) forms[i] = Detached(s.Formations[i]);
        var sides = new SideSpec[s.Sides.Length];
        for (int i = 0; i < sides.Length; i++) sides[i] = s.Sides[i] with { Objectives = (Vec2L[])s.Sides[i].Objectives.Clone() };
        return s with { Grid = Copy(s.Grid), Formations = forms, Sides = sides };
    }

    // ------------------------------------------------------------------ deployment (lane TR's DeploymentPhase only)

    // [D, verification round 1, D-R1-10] the deployment phase and the battle get a COPY of the grid (the session's grid is handed out
    // nowhere; terrain is frozen once the phase is open, so the copy equals it from then on).
    private BattleSetup BaseSetup() => new(Copy(_grid), _costs, _concealment, _sides, Formations.ToArray(), _seed) { Rules = _rules };

    public SandboxRefusal OpenDeployment()
    {
        SandboxRefusal r = SandboxRefusal.None;
        bool s0 = false, s1 = false;
        foreach (var f in _forms) { if (f.Spec.Side == 0) s0 = true; else s1 = true; }
        if (Started) r = SandboxRefusal.BattleStarted;
        else if (_deploy is not null) r = SandboxRefusal.DeploymentOpen;
        else if (!s0 || !s1) r = SandboxRefusal.NoForces;
        if (r == SandboxRefusal.None)
        {
            _deployBase = BaseSetup();
            _deploy = new DeploymentPhase(_deployBase);
        }
        return Record(SandboxEditKind.OpenDeployment, -1, 0, 0, -1, 0, 0, 0, r);
    }

    /// <summary>[D, verification round 2, D-R2-06] A new phase equal to the session's: opened on a detached copy of the same base
    /// setup, every entry of the session phase's log re-proposed in order (lane TR's phase is deterministic, so each gets the same
    /// verdict), and closed at the log position where the session closed it (an edit after the close is refused PhaseClosed in the
    /// copy too).</summary>
    private DeploymentPhase? CopyOfDeployment()
    {
        if (_deploy is null) return null;
        var c = new DeploymentPhase(Detached(_deployBase!));
        var log = _deploy.Log;
        for (int i = 0; i < log.Count; i++)
        {
            if (i == _deployClosedAt) c.Close(out _);
            var e = log[i];
            c.Propose(e.Side, e.FormationId, e.Anchor, e.Entities is null ? null : (EntitySpec[])e.Entities.Clone());
        }
        if (_deploy.IsClosed && !c.IsClosed) c.Close(out _);
        return c;
    }

    /// <summary>[D, verification round 2, D-R2-06] A ledger with the same accounts (index, commodity, balance).</summary>
    private static WarLedger CopyOf(WarLedger l)
    {
        var c = new WarLedger();
        for (int i = 0; i < l.AccountCount; i++) c.Open(l.CommodityOf(i), l.Balance(i));
        return c;
    }

    /// <summary>Proposes a new anchor for one formation; lane TR's verdict decides (its code is logged in C).</summary>
    public SandboxRefusal Deploy(int side, long formationId, Vec2L anchor)
    {
        SandboxRefusal r = SandboxRefusal.None;
        long code = 0;
        if (Started) r = SandboxRefusal.BattleStarted;
        else if (_deploy is null) r = SandboxRefusal.NotDeployed;
        else
        {
            var v = _deploy.Propose(side, formationId, anchor);
            if (!v.Accepted) { r = SandboxRefusal.DeploymentRefused; code = (long)v.Refusal; }
        }
        return Record(SandboxEditKind.Deploy, side, 0, 0, formationId, code, anchor.X, anchor.Y, r);
    }

    /// <summary>Closes the deployment phase; refused unless the whole deployment is valid (lane TR's code logged in C).</summary>
    public SandboxRefusal CloseDeployment()
    {
        SandboxRefusal r = SandboxRefusal.None;
        long code = 0;
        if (Started) r = SandboxRefusal.BattleStarted;
        else if (_deploy is null) r = SandboxRefusal.NotDeployed;
        else
        {
            var v = _deploy.Close(out var setup);
            if (!v.Accepted) { r = SandboxRefusal.DeploymentRefused; code = (long)v.Refusal; }
            else
            {
                _deployed = setup! with { Sides = DefaultObjectives(setup) };
                _deployClosedAt = _deploy.Log.Count;
            }
        }
        return Record(SandboxEditKind.CloseDeployment, -1, 0, 0, -1, code, 0, 0, r);
    }

    /// <summary>The default battle objective of each side (PROPOSAL, as lane D's Meeting scenario): the integer mean anchor of the
    /// ENEMY's deployed formations, so both the knowledge-only AI and the AutoResolver doctrine advance to contact. Exits unchanged
    /// (the thirds were judged on them).</summary>
    private static SideSpec[] DefaultObjectives(BattleSetup setup)
    {
        var sum = new Int128[2, 2];
        var n = new long[2];
        foreach (var f in setup.Formations) { sum[f.Side, 0] += f.Anchor.X; sum[f.Side, 1] += f.Anchor.Y; n[f.Side]++; }
        var sides = new SideSpec[2];
        for (int s = 0; s < 2; s++)
        {
            int e = 1 - s;
            var obj = new Vec2L((long)(sum[e, 0] / n[e]), (long)(sum[e, 1] / n[e]));
            sides[s] = setup.Sides[s] with { Objectives = new[] { obj } };
        }
        return sides;
    }

    // ------------------------------------------------------------------ the battle

    /// <summary>
    /// Runs the battle on the deployed setup. Both sides AI: lane P's default policy (AutoResolver when every formation is in its
    /// model, else the TacticalResolver), headless. Otherwise the TacticalResolver, the player sides driven by
    /// <paramref name="player0"/> / <paramref name="player1"/> (e.g. a ScriptedController), the AI sides by the knowledge-only AI.
    /// Then the sandbox casualty accounting (lane W's collapse into the sandbox's own sinks).
    /// </summary>
    public SandboxRefusal StartBattle(SandboxCommand side0, SandboxCommand side1, IFormationController? player0 = null, IFormationController? player1 = null)
    {
        SandboxRefusal r = SandboxRefusal.None;
        if (Started) r = SandboxRefusal.BattleStarted;
        else if (_deployed is null) r = SandboxRefusal.NotDeployed;
        if (r != SandboxRefusal.None) return Record(SandboxEditKind.StartBattle, -1, 0, 0, -1, (long)side0, (long)side1, -1, r);
        var setup = _deployed!;
        var kind = side0 == SandboxCommand.Ai && side1 == SandboxCommand.Ai ? ResolverPolicy.Default(setup, Commander.Ai, Commander.Ai) : ResolverKind.Tactical;
        IFormationController c0 = side0 == SandboxCommand.Player ? player0 ?? PassiveController.Instance : new KnowledgeAi();
        IFormationController c1 = side1 == SandboxCommand.Player ? player1 ?? PassiveController.Instance : new KnowledgeAi();
        BattleResult result;
        CollapseInput[] inputs;
        var map = new (long, int)[_forms.Count];
        for (int i = 0; i < _forms.Count; i++) map[i] = (_forms[i].Spec.Id, i);
        if (kind == ResolverKind.Auto)
        {
            result = BattleResolution.Resolve(setup, c0, c1, ResolverKind.Auto, new AutoResolver());
            inputs = FromAuto(result, setup.Rules.MoraleMax);
        }
        else
        {
            _tactical = new TacticalBattle(setup, c0, c1);
            result = _tactical.Run();
            _battleOrders = _tactical.Orders;
            inputs = BattleCollapse.FromTactical(_tactical, setup, map);
        }
        _result = result;
        ResolverUsed = kind;
        Account(result, inputs);
        return Record(SandboxEditKind.StartBattle, -1, 0, 0, -1, (long)side0, (long)side1, (long)kind, SandboxRefusal.None);
    }

    /// <summary>Collapse inputs from an AutoResolver result (no per-entity hit points: no wounded category): fit = survivors less
    /// the routed who escaped, routed = those, dead = killed, surrendered. In ascending formation id, as FromTactical.</summary>
    private CollapseInput[] FromAuto(BattleResult result, long moraleMax)
    {
        var inputs = new CollapseInput[_forms.Count];
        for (int i = 0; i < _forms.Count; i++)
        {
            var spec = _forms[i].Spec;
            FormationResult? fr = null;
            foreach (var x in result.Formations) if (x.FormationId == spec.Id) fr = x;
            if (fr is null) throw new InvalidOperationException("a formation without a result");
            inputs[i] = new CollapseInput(i, spec.Side, new[] { fr.Survivors - fr.RoutedEscaped, 0, fr.RoutedEscaped, fr.Killed, fr.Surrendered },
                                          fr.MoraleEnd, moraleMax);
        }
        return inputs;
    }

    /// <summary>The sandbox casualty accounting: lane W's split of each formation's persons over its battle end categories, moved to
    /// the sandbox's own sinks by ledger transfers (the remaining stay in the formation).</summary>
    private void Account(BattleResult result, CollapseInput[] inputs)
    {
        foreach (var input in inputs)
        {
            var f = _forms[input.CampaignFormation];
            long persons = _ledger.Balance(f.Account);
            var split = BattleCollapse.Split(persons, input, result.HoldsField == f.Spec.Side);
            if (!split.Conserved) throw new InvalidOperationException("collapse does not conserve");
            _ledger.Transfer(f.Account, _dead, split.Dead);
            _ledger.Transfer(f.Account, _wounded, split.Wounded);
            _ledger.Transfer(f.Account, _captured, split.Captured);
            _ledger.Transfer(f.Account, _deserted, split.Deserted);
            _casualties.Add(new SandboxCasualtyRow(f.Spec.Id, f.Spec.Side, persons, split.Fit, split.Wounded, split.RoutedReturned, split.Deserted,
                                                   split.Dead, split.Captured));
        }
    }

    // ------------------------------------------------------------------ replay and hashes

    /// <summary>
    /// Replay and Inspection: a fresh session with the same mode, INITIAL terrain, seed, rules and encounter snapshot re-applies every
    /// logged edit in order (refused ones are refused again) and re-runs the battle with each player side's recorded orders
    /// (ScriptedController.FromLog). A deterministic session reproduces its hashes exactly.
    /// </summary>
    public static SandboxSession Replay(SandboxSession original)
    {
        var s = original._encounter is not null
            ? new SandboxSession(SandboxMode.CampaignEncounter, original._initial, original._seed, original._rules, original._encounter)
            : new SandboxSession(original._mode, original._initial, original._seed, original._rules);
        foreach (var e in original._log)
        {
            switch ((SandboxEditKind)e.Kind)
            {
                case SandboxEditKind.TerrainEdit: s.EditTerrain(e.A, e.B, (TerrainClass)e.FamilyKey, e.C); break;
                case SandboxEditKind.AddForce:
                    if (s._encounter is not null && e.Index < original._encounter!.Forces.Length) s.AddEncounterForce(e.Side, e.FamilyKey, e.IdentityKey, e.A, new Vec2L(e.B, e.C));
                    else s.AddForce(e.Side, e.FamilyKey, e.IdentityKey, e.A, new Vec2L(e.B, e.C), out _);
                    break;
                case SandboxEditKind.OpenDeployment: s.OpenDeployment(); break;
                case SandboxEditKind.Deploy: s.Deploy(e.Side, e.FormationId, new Vec2L(e.B, e.C)); break;
                case SandboxEditKind.CloseDeployment: s.CloseDeployment(); break;
                case SandboxEditKind.StartBattle:
                    var c0 = (SandboxCommand)e.A; var c1 = (SandboxCommand)e.B;
                    s.StartBattle(c0, c1,
                        c0 == SandboxCommand.Player ? ScriptedController.FromLog(original._battleOrders, 0) : null,
                        c1 == SandboxCommand.Player ? ScriptedController.FromLog(original._battleOrders, 1) : null);
                    break;
            }
        }
        return s;
    }

    /// <summary>Canonical hash of the session: the edit log, the casualty rows, every ledger balance and the battle's result hash.</summary>
    public string HashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData(SandboxCodec.EncodeEdits(_log));
        h.AppendData(SandboxCodec.EncodeCasualties(_casualties));
        _ledger.AppendTo(h);
        if (_result is not null) h.AppendData(Convert.FromHexString(_result.ResultHash()));
        return Convert.ToHexString(h.GetHashAndReset());
    }

    private static TerrainGrid Copy(TerrainGrid g)
    {
        var c = new TerrainGrid(g.Width, g.Height, g.CellSizeMm, TerrainClass.Open);
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                c.Set(x, y, g.ClassAt(x, y), g.HeightAt(x, y));
        return c;
    }

    /// <summary>A copy of the session's current terrain (the same as <see cref="Terrain"/>). The session's grid is never handed out
    /// for writing; that is true since verification round 1 (D-R1-10), not before.</summary>
    public TerrainGrid TerrainCopy() => Copy(_grid);
}
