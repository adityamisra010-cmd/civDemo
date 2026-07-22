using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

// sim-ui (T1.7/T1.8): found the canonical world, build the production executor
// and a fresh session order log, open the window. Worldgen runs before the
// window so the first frame already has terrain (~2 s at 1024²).
// Args: [--seed N] [--size PX] (size is the D-015 dev-preview escape hatch).
ulong seed = 42;
int? sizeOverride = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--seed" && ulong.TryParse(args[i + 1],
        System.Globalization.NumberStyles.Integer,
        System.Globalization.CultureInfo.InvariantCulture, out ulong s)) seed = s;
    if (args[i] == "--size" && int.TryParse(args[i + 1],
        System.Globalization.NumberStyles.Integer,
        System.Globalization.CultureInfo.InvariantCulture, out int px)) sizeOverride = px;
}

WorldgenConfig worldgenCfg;
using (var stream = Sim.Data.DataFiles.OpenWorldgen())
{
    worldgenCfg = WorldgenConfigLoader.Load(stream);
}
if (sizeOverride is { } sz) worldgenCfg = worldgenCfg with { SizePx = sz };

SimConfig simCfg;
using (var stream = Sim.Data.DataFiles.OpenSim())
{
    simCfg = SimConfigLoader.Load(stream);
}

EraTable era;
using (var stream = Sim.Data.DataFiles.OpenEraPacing())
{
    era = EraTableLoader.Load(stream);
}
SystemRegistration[] pipeline;
using (var stream = Sim.Data.DataFiles.OpenPipeline())
{
    pipeline = PipelineLoader.Load(stream, SystemCatalog.All(simCfg));
}

// Session recording (T1.8): every UI session autosaves its order log here.
// Wall-clock folder names are legal in Sim.Ui (outside the determinism
// surface); the log CONTENT records sim turns only.
string runDirectory = Path.Combine(
    "runs", DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));

var orders = new OrderLog();
var executor = new TurnExecutor(era, pipeline, orders);
using var game = new Sim.Ui.SimUiGame(
    WorldFounding.Found(worldgenCfg, simCfg, seed), executor, orders, runDirectory);
game.Run();
