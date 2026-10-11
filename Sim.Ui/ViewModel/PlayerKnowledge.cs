using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M5 map workstream P7 — WHAT THE PLAYER KNOWS, as the UI reads it: one view per turn, BUILT FROM the Sim.Core
/// knowledge seam (<see cref="PolityKnowledge"/>) and never a second source of truth. Every answer is the seam's; this
/// type only fixes the polity (the player's Empire), caches the per-settlement and per-polity answers once per turn
/// (the readers ask them per row, per frame) and adds the one UI question the seam has no need of
/// (<see cref="IsHiddenSettlement"/>).
///
/// <para>EVERY player-facing reader goes through it: the world lens projection (<c>WorldProjection.Build</c>'s observer),
/// settlement picking and Tab (the projection draws only what is known, and <c>SettlementSelection.CycleNext</c> skips the
/// rest), the HUD's world totals, the annals (filtered by subject in <see cref="UiSession"/>), the player views, the market
/// and needs panels, the unit card and the formation info card, the Trends world scope and the history buffer's world
/// series. The source guard (Sim.Ui.Tests KnowledgeLeakTests) pins the raw table reads outside it to an allowlist with
/// reasons; the metamorphic tests perturb what a stub hides and require every output identical.</para>
///
/// <para>ALL-KNOWN TODAY: <see cref="Of(IReadOnlyWorldState)"/> wraps <see cref="PolityKnowledge.Of"/>, whose only future
/// input is S2a's stored known extent, so every reader is byte-identical to before. A stub (<see cref="From"/> over
/// <see cref="PolityKnowledge.Restricted"/>) is how the tests and the headless gate's hidden-rival rig hide a polity.</para>
/// </summary>
public sealed class PlayerKnowledge
{
    private readonly IReadOnlyWorldState _world;
    private readonly bool[] _settlement;   // by settlement id: KnowsSettlement
    private readonly bool[] _exists;       // by settlement id: a settlement row has it
    private readonly bool[] _polity;       // by polity id: KnowsPolity

    /// <summary>The seam this view was built from.</summary>
    public PolityKnowledge Core { get; }

    /// <summary>The player's polity.</summary>
    public PolityId Player => Core.Polity;

    /// <summary>True while nothing is hidden (every production view until S2a).</summary>
    public bool AllKnown => Core.AllKnown;

    private PlayerKnowledge(IReadOnlyWorldState world, PolityKnowledge core)
    {
        _world = world;
        Core = core;
        int maxId = -1;
        for (int i = 0; i < world.Settlements.Count; i++) maxId = Math.Max(maxId, world.Settlements[i].Id.Value);
        _settlement = new bool[maxId + 1];
        _exists = new bool[maxId + 1];
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementId id = world.Settlements[i].Id;
            if (id.Value < 0) continue;
            _exists[id.Value] = true;
            _settlement[id.Value] = core.KnowsSettlement(id);
        }
        int maxPolity = core.Polity.Value;
        for (int i = 0; i < world.Polities.Count; i++) maxPolity = Math.Max(maxPolity, world.Polities[i].Id.Value);
        for (int i = 0; i < world.Controls.Count; i++) maxPolity = Math.Max(maxPolity, world.Controls[i].Polity.Value);
        _polity = new bool[Math.Max(0, maxPolity) + 1];
        for (int p = 0; p < _polity.Length; p++) _polity[p] = core.KnowsPolity(new PolityId(p));
    }

    /// <summary>What the player's Empire knows of <paramref name="world"/> — THE production view.</summary>
    public static PlayerKnowledge Of(IReadOnlyWorldState world) => Of(world, UiPlayer.Empire);

    /// <summary>What <paramref name="player"/> knows of <paramref name="world"/> (the seam's production entry).</summary>
    public static PlayerKnowledge Of(IReadOnlyWorldState world, PolityId player)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new PlayerKnowledge(world, PolityKnowledge.Of(world, player));
    }

    /// <summary>The view of an explicit seam instance over <paramref name="world"/> — the entry a knowledge STUB
    /// (<see cref="PolityKnowledge.Restricted"/>) comes in by. <paramref name="core"/> must answer for the same world.</summary>
    public static PlayerKnowledge From(IReadOnlyWorldState world, PolityKnowledge core)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(core);
        return new PlayerKnowledge(world, core);
    }

    /// <summary>Whether lattice node <paramref name="node"/> is in the player's known extent.</summary>
    public bool Known(int node) => Core.Known(node);

    /// <summary>Whether lattice node <paramref name="node"/> is in the player's sight now.</summary>
    public bool Visible(int node) => Core.Visible(node);

    /// <summary>Whether the player knows settlement <paramref name="id"/> (false for an id no settlement has).</summary>
    public bool KnowsSettlement(int id) => (uint)id < (uint)_settlement.Length && _settlement[id];

    /// <summary>
    /// Whether settlement <paramref name="id"/> EXISTS and the player does not know it — what a reader must leave out. An
    /// id no settlement row has is not hidden (a stale or extinct id stays the reader's own concern, as before P7).
    /// </summary>
    public bool IsHiddenSettlement(int id) => (uint)id < (uint)_exists.Length && _exists[id] && !_settlement[id];

    /// <summary>Whether the player knows formation <paramref name="unit"/>.</summary>
    public bool KnowsUnit(in MilitaryUnitRow unit) => Core.KnowsUnit(unit);

    /// <summary>Whether the player knows polity <paramref name="polity"/> (itself, or contact through a known settlement).</summary>
    public bool KnowsPolity(int polity) =>
        (uint)polity < (uint)_polity.Length ? _polity[polity] : Core.KnowsPolity(new PolityId(polity));

    /// <summary>Whether the player knows the lattice node holding terrain cell <paramref name="cell"/>.</summary>
    public bool KnowsCell(int cell) => Core.KnowsCell(cell);

    /// <summary>The world this view answers for.</summary>
    public IReadOnlyWorldState World => _world;
}
