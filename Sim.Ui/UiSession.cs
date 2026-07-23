using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
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

    /// <summary>Settlement id → name (deterministic from world seed; ADR-001
    /// registry, never sim rows).</summary>
    public Sim.Core.Chronicle.NameRegistry Names { get; }

    /// <summary>The annals, oldest first (the panel renders newest LAST).</summary>
    public IReadOnlyList<string> AnnalLines => _annals;

    private UiSession(WorldState world, TurnExecutor executor, OrderLog orders)
    {
        World = world;
        _executor = executor;
        Orders = orders;
        using (var stream = Sim.Data.DataFiles.OpenChronicle())
        {
            _chronicleCfg = Sim.Core.Chronicle.ChronicleConfigLoader.Load(stream);
        }
        Names = Sim.Core.Chronicle.NameRegistry.Build(_chronicleCfg, world.Seed, world);
        _chronicle = new Sim.Core.Chronicle.ChronicleCollector(_chronicleCfg);
        ObserveChronicle(); // founding events fire on first sight
    }

    private void ObserveChronicle()
    {
        _chronicle.Observe(World);
        for (; _renderedEvents < _chronicle.Events.Count; _renderedEvents++)
            _annals.Add(Sim.Core.Chronicle.ChronicleProse.Render(
                _chronicle.Events[_renderedEvents], _chronicleCfg, Names));
    }

    /// <summary>Founds the world and builds the PRODUCTION executor + a fresh log.</summary>
    public static UiSession Start(
        ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null)
    {
        var orders = new OrderLog();
        return new UiSession(
            UiFounding.Found(seed, sizeOverridePx, settlementsOverride),
            BuildProductionExecutor(orders), orders);
    }

    /// <summary>
    /// The UI's executor recipe — canonical era + PRODUCTION pipeline preset +
    /// full system catalog. Public so the replay-equivalence test pins it; any
    /// UI-only preset/era drift breaks that test, not a played session.
    /// </summary>
    public static TurnExecutor BuildProductionExecutor(OrderLog orders)
    {
        EraTable era;
        using (var stream = Sim.Data.DataFiles.OpenEraPacing())
        {
            era = EraTableLoader.Load(stream);
        }
        SimConfig simCfg;
        using (var stream = Sim.Data.DataFiles.OpenSim())
        using (var needs = Sim.Data.DataFiles.OpenNeeds())
        {
            simCfg = SimConfigLoader.Load(stream, needs);
        }
        SystemRegistration[] pipeline;
        using (var stream = Sim.Data.DataFiles.OpenPipeline())
        {
            pipeline = PipelineLoader.Load(stream, SystemCatalog.All(simCfg));
        }
        return new TurnExecutor(era, pipeline, orders);
    }

    /// <summary>The HUD slider's release handler: ONE order, stamped with the
    /// CURRENT turn (§3.9: delivered to the very next End Turn step), targeting
    /// the FIRST settlement — the pre-selection shorthand the T1.9 replay
    /// tests pin; the selection HUD calls the targeted overload below.</summary>
    public void EmitLaborOrder(int farmPct)
    {
        if (World.Settlements.Count == 0) return;
        EmitLaborOrder(farmPct, World.Settlements[0].Id.Value);
    }

    /// <summary>T2.4: the targeted form — the slider orders the SELECTED
    /// settlement. An id not present in the world emits NOTHING (an order for
    /// a ghost settlement would poison the log at replay validation).</summary>
    public void EmitLaborOrder(int farmPct, int settlementId)
    {
        for (int i = 0; i < World.Settlements.Count; i++)
        {
            if (World.Settlements[i].Id.Value == settlementId)
            {
                Orders.Append(LaborOrderFactory.Create(
                    World.Clock.Turn, World.Settlements[i].Id, farmPct));
                return;
            }
        }
    }

    /// <summary>End Turn: the executor steps synchronously (m1 spec §3);
    /// the chronicle observes the new state (detection is read-only).</summary>
    public void EndTurn()
    {
        World = _executor.Step(World);
        ObserveChronicle();
    }

    /// <summary>The chronicle.txt path twinned with a session log path:
    /// same stamp, `chronicle-` prefix, `.txt`.</summary>
    public static string ChroniclePath(string sessionLogPath) =>
        Path.Combine(Path.GetDirectoryName(sessionLogPath) ?? "",
            Path.GetFileNameWithoutExtension(sessionLogPath)
                .Replace("orders-", "chronicle-") + ".txt");

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
        DateTime now, int? sizeOverridePx = null, int? settlementsOverride = null) =>
        Path.Combine("runs",
            "orders-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + (sizeOverridePx is { } sz
                ? "-s" + sz.ToString(CultureInfo.InvariantCulture)
                : "")
            + (settlementsOverride is { } n
                ? "-n" + n.ToString(CultureInfo.InvariantCulture)
                : "")
            + ".bin");

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        Orders.Save(stream);
    }
}
