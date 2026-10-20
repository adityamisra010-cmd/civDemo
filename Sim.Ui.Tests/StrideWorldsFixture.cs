using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.Tests;

/// <summary>
/// The worlds the projection stride pin (<see cref="WorldProjectionStrideTests"/>) runs over: three seeds at the
/// canonical 1024-px size at several turns (turn 0 has no path network yet; turn 1 has the first one; turn 4 the
/// catchments and paths after a few years), a 250-px world (not a multiple of the lattice stride 4: the last two
/// terrain columns and rows belong to no lattice block — founded FLAT, explicitly: batch 3 WS3 refuses a wrapped world
/// whose size the stride does not divide, and orphan columns are exactly what this case exists to exercise), and the road world of <see cref="RoadWorldFixture"/> (built
/// routes of every class, so the corridor ownership — which reads the stride — is exercised). Every world is founded
/// and stepped by the real session; nothing is constructed.
/// </summary>
public sealed class StrideWorldsFixture
{
    public sealed record Case(string Label, IReadOnlyWorldState World, SimConfig Config, Func<int, string> Name);

    public IReadOnlyList<Case> Cases { get; }

    public StrideWorldsFixture()
    {
        var cases = new List<Case>();
        void Step(string name, UiSession s, params int[] turns)
        {
            foreach (int t in turns)
            {
                while (s.World.Clock.Turn < t) s.EndTurn();
                cases.Add(new Case(name + "-t" + s.World.Clock.Turn, s.World.Clone(), s.Config, s.Names.Name));
            }
        }
        Step("seed42", UiSession.Start(42), 0, 1, 4);
        Step("seed7", UiSession.Start(7), 1, 4);
        Step("seed3", UiSession.Start(3), 1);
        Step("seed42-250px", UiSession.Start(42, sizeOverridePx: 250, settlementsOverride: 6, wrapXOverride: false), 1, 4);
        UiSession roads = new RoadWorldFixture().Session;
        cases.Add(new Case("roads-256px-t" + roads.World.Clock.Turn, roads.World.Clone(), roads.Config, roads.Names.Name));
        Cases = cases;
    }
}
