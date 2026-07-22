using Sim.Core.Systems;
using Sim.Core.Worldgen;

// sim-ui (T1.7): found the canonical world, open the window. Worldgen runs
// before the window so the first frame already has terrain (~2 s at 1024²).
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

using var game = new Sim.Ui.SimUiGame(WorldFounding.Found(worldgenCfg, simCfg, seed));
game.Run();
