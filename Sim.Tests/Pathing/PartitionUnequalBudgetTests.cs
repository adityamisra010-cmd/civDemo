using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Tests.Pathing;

/// <summary>
/// T3.8 — the UNEQUAL-BUDGET semantics pin for Partition (the transit ruling).
/// The T2.3 witness — per-source capped Dijkstra fields merged by the
/// composite (cost, index) key — is the SPEC of the partition. Under equal
/// budgets the single-label multi-source pass equals it exactly (a refused
/// tentative is refused for every source alike); under UNEQUAL budgets (the
/// T3.8 size bonus) it does not: a nearer small-budget source holds a node's
/// only label, exhausts at its budget, and a larger-budget source never
/// propagates through — its claim zone beyond the rival is silently lost.
/// This rig answers BOTH verify-stage questions (ADR-015 §7.2): the shipped
/// Partition equals the witness on every node (the property), AND the
/// single-label formulation, run inline as a mutant on the same inputs,
/// visibly diverges from the witness (the teeth — the case is really
/// exercised, not vacuously green). If no probed configuration diverges, the
/// rig FAILS as vacuous rather than passing.
/// </summary>
public class PartitionUnequalBudgetTests
{
    private static WorldgenConfig Dev()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream) with { SizePx = 256 };
    }

    private static (TraversalLattice Lattice, WorldState World) DevWorld(ulong seed)
    {
        var terrain = Sim.Core.Worldgen.Worldgen.Generate(Dev(), seed);
        var world = new WorldState(seed) { Terrain = terrain };
        return (TraversalLattice.Build(terrain, Sim.Tests.TestUtil.TestConfigs.RiverCostFactor()), world);
    }

    /// <summary>Largest 8-connected passable component, ascending node id —
    /// the same connectivity the lattice's step expansion uses.</summary>
    private static int[] LargestComponent(TraversalLattice lattice)
    {
        int n = lattice.NodeCount;
        var component = new int[n];
        Array.Fill(component, -1);
        var sizes = new List<int>();
        var queue = new Queue<int>();
        for (int start = 0; start < n; start++)
        {
            if (!lattice.IsPassable(start) || component[start] >= 0) continue;
            int id = sizes.Count;
            component[start] = id;
            int size = 0;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                size++;
                (int x, int y) = lattice.Coords(i);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                        int nb = ny * lattice.Size + nx;
                        if (!lattice.IsPassable(nb) || component[nb] >= 0) continue;
                        component[nb] = id;
                        queue.Enqueue(nb);
                    }
                }
            }
            sizes.Add(size);
        }
        int biggest = 0;
        for (int c = 1; c < sizes.Count; c++) if (sizes[c] > sizes[biggest]) biggest = c;
        var nodes = new List<int>();
        for (int i = 0; i < n; i++) if (component[i] == biggest) nodes.Add(i);
        return [.. nodes];
    }

    /// <summary>Full (uncapped) single-source field over raw terrain — the
    /// witness's per-source primitive, one call per origin.</summary>
    private static double[] FullField(TraversalLattice lattice, int origin)
    {
        var field = new double[lattice.NodeCount];
        Array.Fill(field, double.PositiveInfinity);
        Pathfinder.RelaxCappedFrom(lattice, origin, double.MaxValue, field);
        return field;
    }

    private static double Quantile(double[] field, double q)
    {
        var finite = new List<double>();
        for (int i = 0; i < field.Length; i++)
            if (!double.IsPositiveInfinity(field[i])) finite.Add(field[i]);
        double[] sorted = [.. finite];
        Array.Sort(sorted);
        return sorted[(int)(q * (sorted.Length - 1))];
    }

    /// <summary>The OLD single-label multi-source formulation, verbatim
    /// semantics (relax-time budget refusal on the label's owner, one label
    /// per node, unclosed nodes normalized out) — run as an INLINE MUTANT so
    /// the divergence it causes under unequal budgets stays measured, not
    /// argued. Raw terrain (the test world carries no network overlay).</summary>
    private static (int[] Owner, double[] Cost) SingleLabelMutant(
        TraversalLattice lattice, int[] origins, double[] budgets)
    {
        int n = lattice.NodeCount;
        var g = new double[n];
        var owner = new int[n];
        var closed = new bool[n];
        Array.Fill(g, double.MaxValue);
        Array.Fill(owner, -1);

        var open = new PriorityQueue<int, (double Cost, int Node)>();
        for (int s = 0; s < origins.Length; s++)
        {
            int origin = origins[s];
            if (!lattice.IsPassable(origin)) continue;
            if (g[origin] == 0.0 && owner[origin] >= 0) continue;
            g[origin] = 0.0;
            owner[origin] = s;
            open.Enqueue(origin, (0.0, origin));
        }
        double maxBudget = 0.0;
        for (int s = 0; s < budgets.Length; s++) if (budgets[s] > maxBudget) maxBudget = budgets[s];

        while (open.TryDequeue(out int current, out (double Cost, int Node) key))
        {
            if (closed[current]) continue;
            if (key.Cost > maxBudget) break;
            closed[current] = true;
            (int x, int y) = lattice.Coords(current);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;
                    int nb = ny * lattice.Size + nx;
                    if (closed[nb] || !lattice.IsPassable(nb)) continue;
                    double tentative = g[current] + lattice.StepCost(current, nb);
                    if (tentative > budgets[owner[current]]) continue;
                    if (tentative < g[nb] || (tentative == g[nb] && owner[current] < owner[nb]))
                    {
                        g[nb] = tentative;
                        owner[nb] = owner[current];
                        open.Enqueue(nb, (tentative, nb));
                    }
                }
            }
        }
        for (int i = 0; i < n; i++)
            if (!closed[i]) { owner[i] = -1; g[i] = double.MaxValue; }
        return (owner, g);
    }

    [Fact]
    public void Partition_UnequalBudgets_MatchesPerSourceWitness_WhereSingleLabelProvablyDiverges()
    {
        (TraversalLattice lattice, WorldState world) = DevWorld(42);
        int n = lattice.NodeCount;
        int[] component = LargestComponent(lattice);
        Assert.True(component.Length >= 200, "dev world too small for the probe");

        // Deterministic probe: pair each candidate with the component node
        // three columns east; small budget (25th percentile of A's own field)
        // against large (75th of B's). The steal zone is the annulus beyond
        // A's budget on A's far side, inside B's reach — probed pairs are
        // close enough that B's geodesics there run through A-cheaper ground.
        var inComponent = new HashSet<int>(component);
        int probed = 0, divergentPairs = 0;
        for (int idx = 0; idx < component.Length && probed < 25; idx += 97)
        {
            int a = component[idx];
            (int ax, int ay) = lattice.Coords(a);
            if (ax + 3 >= lattice.Size) continue;
            int b = lattice.NodeId(ax + 3, ay);
            if (!inComponent.Contains(b)) continue;
            probed++;

            double[] fieldA = FullField(lattice, a);
            double[] fieldB = FullField(lattice, b);
            double budgetA = Quantile(fieldA, 0.25);
            double budgetB = Quantile(fieldB, 0.75);
            if (!(budgetB > budgetA)) continue;
            int[] origins = [a, b];
            double[] budgets = [budgetA, budgetB];

            // The witness: per-source fields + composite (cost, index) merge.
            var witnessOwner = new int[n];
            var witnessCost = new double[n];
            for (int i = 0; i < n; i++)
            {
                int best = -1;
                double bestCost = double.MaxValue;
                if (fieldA[i] <= budgetA) { best = 0; bestCost = fieldA[i]; }
                if (fieldB[i] <= budgetB && fieldB[i] < bestCost) { best = 1; bestCost = fieldB[i]; }
                witnessOwner[i] = best;
                witnessCost[i] = best < 0 ? double.MaxValue : bestCost;
            }

            // Property: the shipped Partition equals the witness node-for-node
            // (owner AND bit-exact cost) on every probed configuration.
            Pathfinder.PartitionResult part = Pathfinder.Partition(lattice, world, origins, budgets);
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(witnessOwner[i], part.Owner[i]);
                Assert.Equal(BitConverter.DoubleToInt64Bits(witnessCost[i]),
                    BitConverter.DoubleToInt64Bits(part.Cost[i]));
            }

            // Teeth: the single-label mutant must diverge somewhere on at
            // least one probed pair, or the probe never left equal-budget
            // territory and proves nothing about per-budget semantics.
            (int[] mutantOwner, _) = SingleLabelMutant(lattice, origins, budgets);
            for (int i = 0; i < n; i++)
            {
                if (mutantOwner[i] != witnessOwner[i]) { divergentPairs++; break; }
            }
        }

        Assert.True(probed >= 5, $"only {probed} pairs probed — probe too sparse to mean anything");
        Assert.True(divergentPairs > 0,
            $"VACUOUS: {probed} unequal-budget pairs probed and the single-label formulation " +
            "never diverged from the per-source witness — the steal zone was not exercised");
        Console.WriteLine(
            $"partition transit pin: {probed} pairs probed, {divergentPairs} exhibit single-label divergence");
    }
}
